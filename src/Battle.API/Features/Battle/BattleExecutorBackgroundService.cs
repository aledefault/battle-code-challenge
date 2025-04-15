using System.Collections.Concurrent;
using Battle.API.Features.Battle.BattleScenario;
using Battle.API.Options;
using MessagePack;
using Microsoft.Extensions.Options;
using Redis.OM;
using Redis.OM.Searching;
using RedLockNet.SERedis;
using StackExchange.Redis;

namespace Battle.API.Features.Battle;

/// <summary>
/// BattleExecutorBackgroundService execute Battles and pass the BattleResult to another stream (fdr processing the BattleReport)
/// </summary>
public class BattleExecutorBackgroundService : BackgroundService
{
    private readonly RedisOptions _redisOptions;
    private readonly ILogger<BattleExecutorBackgroundService> _logger;
    private readonly BattleExecutor _battleExecutor;
    private readonly IDatabase _redisDatabase;
    private readonly RedLockFactory _redLockFactory;
    private readonly TimeSpan _lockExpiry;
    private readonly TimeSpan _lockWait;
    private readonly TimeSpan _lockRetry;
    private readonly int _numberOfEntriesToReadEachIteration;
    private readonly IRedisCollection<Domain.Player> _playerCollection;

    public BattleExecutorBackgroundService(
        BattleExecutor battleExecutor,
        ConnectionMultiplexer connectionMultiplexer,
        RedisConnectionProvider redisConnectionProvider,
        IOptions<SystemOptions> systemOptions,
        IOptions<RedisOptions> redisOptions,
        ILogger<BattleExecutorBackgroundService> logger)
    {
        _battleExecutor = battleExecutor;
        _redisOptions = redisOptions.Value;
        _logger = logger;
        _redisDatabase = connectionMultiplexer.GetDatabase();
        _playerCollection = redisConnectionProvider.RedisCollection<Domain.Player>();
        _redLockFactory = RedLockFactory.Create([connectionMultiplexer]);

        _lockExpiry = TimeSpan.FromMilliseconds(systemOptions.Value.PlayerLockTimeoutInMilliseconds);
        _lockWait = TimeSpan.FromMilliseconds(systemOptions.Value.PlayerLockWaitInMilliseconds);
        _lockRetry = TimeSpan.FromMilliseconds(systemOptions.Value.PlayerLockRetryInMilliseconds);

        _numberOfEntriesToReadEachIteration = systemOptions.Value.NumberOfBattleEntriesToReadEachIteration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var lastIdsConsumed = new List<RedisValue>();
            while (!stoppingToken.IsCancellationRequested)
            {
                if ((lastIdsConsumed?.Count ?? 0) > 0)
                {
                    await _redisDatabase.StreamAcknowledgeAsync(
                        _redisOptions.BattleExecutorStreamName, 
                        _redisOptions.BattleExecutorGroupName, 
                        lastIdsConsumed?.ToArray() ?? []);    
                    
                    lastIdsConsumed = [];
                }

                var result = await _redisDatabase.StreamReadGroupAsync(
                    _redisOptions.BattleExecutorStreamName,
                    _redisOptions.BattleExecutorGroupName,
                    nameof(BattleExecutorBackgroundService),
                    ">",
                    _numberOfEntriesToReadEachIteration);
                
                lastIdsConsumed = await ExecuteConcurrentBattlesAsync(stoppingToken, result);

                await Task.Delay(10, stoppingToken); // TODO: Avoid exhaustion. Profile this. It can be remove but I need to test that first
            }
        }
        catch (Exception e)
        {
            _logger.LogError("There was an error: {ErrorMessage}. Waiting some time before trying it again. ", e.Message);
            await Task.Delay(500, stoppingToken); // This can be improved using something like Polly. Give it sometime to recover, if not, some alarms should be raised.
        }
    }

    private async Task<List<RedisValue>?> ExecuteConcurrentBattlesAsync(CancellationToken stoppingToken, StreamEntry[] results)
    {
        ConcurrentBag<RedisValue> lastIdsConsumed = [];
        if (results.Length <= 0) return [];
        
        var tasks = new List<Task>(results.Select(async result =>
        {
            var players = ParseBattlePlayers(result);
            
            var requester = await _playerCollection.FindByIdAsync(players.RequesterId);
            var opponent = await _playerCollection.FindByIdAsync(players.OpponentId);
            
            if (requester.Id.Equals(opponent.Id))
            {
                _logger.LogWarning("Be careful, we don't allow Fight Club battles yet. Discarding {BattleId}...", result.Id);
                return;
            }

            var lockKeyRequester = $"lock:player:{requester.Id}";
            var lockKeyOpponent = $"lock:player:{opponent.Id}";
            await using var redLock1 = await _redLockFactory.CreateLockAsync(
                lockKeyRequester,
                _lockExpiry,
                _lockRetry,
                _lockWait,
                stoppingToken);

            if (redLock1.IsAcquired)
            {
                await using var redLock2 = await _redLockFactory.CreateLockAsync(
                    lockKeyOpponent,
                    _lockExpiry,
                    _lockRetry,
                    _lockWait,
                    stoppingToken);

                if (redLock2.IsAcquired)
                {
                    var battleResult = _battleExecutor.Execute(requester, opponent);

                    await _redisDatabase.StreamAddAsync(_redisOptions.BattleReporterStreamName,
                    [
                        new NameValueEntry("data", new RedisValue(Convert.ToBase64String(MessagePackSerializer.Serialize(battleResult))))
                    ]);

                    lastIdsConsumed.Add(result.Id);
                }
            }
        }));

        await Task.WhenAll(tasks);
        return lastIdsConsumed.ToList();
    }

    private BattlePlayers ParseBattlePlayers(StreamEntry result)
    {
        var entry = result.Values.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        return MessagePackSerializer.Deserialize<BattlePlayers>(Convert.FromBase64String(entry["data"]));
    }
}