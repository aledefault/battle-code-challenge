using System.Security.Claims;
using Battle.API.Infrastructure;
using Battle.API.Options;
using Grpc.Core;
using MessagePack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Redis.OM;
using Redis.OM.Searching;
using StackExchange.Redis;

namespace Battle.API.Features.Battle;

public class BattleService : API.Battle.BattleBase
{
    private readonly ILogger<BattleService> _logger;
    private readonly RedisOptions _redisOptions;
    private readonly ConnectionMultiplexer _mux;
    private readonly IDatabase _redisDatabase;
    private readonly IRedisCollection<Domain.Player> _playerCollection;
    private readonly TimeSpan _waitingTimeout;
    private readonly RedisHelper _redisHelper;

    public BattleService(
        ConnectionMultiplexer connectionMultiplexer,
        RedisConnectionProvider redisConnectionProvider,
        RedisHelper redisHelper,
        IOptions<SystemOptions> systemOptions,
        IOptions<RedisOptions> redisOptions,
        ILogger<BattleService> logger)
    {
        _redisOptions = redisOptions.Value;
        _logger = logger;
        _mux = connectionMultiplexer;
        _redisDatabase = connectionMultiplexer.GetDatabase();
        _playerCollection = redisConnectionProvider.RedisCollection<Domain.Player>();
        _waitingTimeout = TimeSpan.FromSeconds(systemOptions.Value.WaitingBattleResulBeforeTimeoutInSeconds);
        _redisHelper = redisHelper;
    }

    /// <summary>
    /// Enqueue a Battle and wait x second for the BattleReport before timeout. 
    /// </summary>
    [Authorize]
    public override async Task Submit(
        BattleRequest request,
        IServerStreamWriter<BattleResponse> responseStream,
        ServerCallContext context)
    {
        var (requesterUser, opponentUser) = await ValidateRequestAsync(request, context);
        var tcs = new TaskCompletionSource<BattleReport>();
        var (channel, subscriber) = await SubscribeToReports(tcs, requesterUser, opponentUser);

        await EnqueueBattlePlayersAsync(requesterUser, opponentUser);

        // Wait for result or timeout
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(_waitingTimeout, context.CancellationToken));
        await subscriber.UnsubscribeAsync(channel);

        if (completed == tcs.Task)
        {
            var report = await tcs.Task;
            var response = new BattleResponse
            {
                Username = requesterUser.UserName,
                Damage = report.BattleResult.Actions.Where(x => x.Action == BattleAction.ActionType.Hit).Sum(x => x.Damage),
                Misses = report.BattleResult.Actions.Count(x => x.Action == BattleAction.ActionType.Miss),
                Outcome = report.BattleResult.Outcome.ToString(),
                ResourcesGain = new BattleResources
                {
                    Gold = report.GainedGold,
                    Silver = report.GainedSilver
                },
                ResourcesLost = new BattleResources
                {
                    Gold = report.LostGold,
                    Silver = report.LostSilver
                }
            };

            await responseStream.WriteAsync(response, context.CancellationToken);
        }
        else
        {
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Battle processing timed out."));
        }
    }

    private async Task<(Domain.Player requesterUser, Domain.Player opponentUser)> ValidateRequestAsync(BattleRequest request, ServerCallContext context)
    {
        var currentUserId = context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Unauthenticated"));

        if (currentUserId.Equals(request.OpponentId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Fight Club is currently not allowed"));

        var opponentUser = await _playerCollection.FindByIdAsync(request.OpponentId);
        if (opponentUser == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Opponent not found."));

        if (opponentUser is { Gold: <= 0, Silver: <= 0 })
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "The opponent is broken. Search for another one."));

        var requestedUser = await _playerCollection.FindByIdAsync(currentUserId);
        if (requestedUser == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Current user not found."));

        return (requestedUser, opponentUser);
    }

    private async Task EnqueueBattlePlayersAsync(Domain.Player requestedUser, Domain.Player opponentUser)
    {
        var serializedBattlePlayers = MessagePackSerializer.Serialize(new BattlePlayers
        {
            RequesterId = requestedUser.Id,
            OpponentId = opponentUser.Id
        });

        await _redisDatabase.StreamAddAsync(_redisOptions.BattleExecutorStreamName,
        [
            new NameValueEntry("data", Convert.ToBase64String(serializedBattlePlayers))
        ]);
    }

    private async Task<(RedisChannel channel, ISubscriber subscriber)> SubscribeToReports(
        TaskCompletionSource<BattleReport> tcs,
        Domain.Player requested,
        Domain.Player opponent)
    {
        var channel = _redisHelper.GetReportChannelFor(requested.Id, opponent.Id);
        var subscriber = _mux.GetSubscriber();

        await subscriber.SubscribeAsync(channel, (_, value) =>
        {
            try
            {
                tcs.TrySetResult(MessagePackSerializer.Deserialize<BattleReport>(Convert.FromBase64String(value)));
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception while processing message from channel: {channel}': {ex.Message}", channel, ex.Message);
                tcs.TrySetException(ex);
            }
        });

        return (channel, subscriber);
    }
}