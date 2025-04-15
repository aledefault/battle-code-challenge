using System.Collections.Concurrent;
using Battle.API.Domain;
using Battle.API.Infrastructure;
using Battle.API.Options;
using MessagePack;
using Microsoft.Extensions.Options;
using Redis.OM;
using Redis.OM.Searching;
using StackExchange.Redis;

namespace Battle.API.Features.Battle;
/// <summary>
/// BattleResourceManagementBackgroundService is in charge of:
/// 1. Create the final BattleReport
/// 2. Notify the user that the BattleReport is ready.
/// 3. Update Players states and Leaderboard.
///
/// Ideally, the Leaderboard part will be in another background service, but to keep it simple, I've decided
/// to give that responsibility to this service.
/// </summary>
public class BattleResourceManagementBackgroundService : BackgroundService
{
    private readonly RedisOptions _redisOptions;
    private readonly ILogger<BattleResourceManagementBackgroundService> _logger;
    private readonly Random _random;
    private readonly int _numberOfEntriesToReadEachIteration;
    private readonly RedisConnectionProvider _redisConnectionProvider;
    private readonly RedisHelper _redisHelper;
    private readonly ConnectionMultiplexer _connectionMultiplexer;
    private readonly SystemOptions _systemOptions;
    private static int _goldScoreRatio;

    public BattleResourceManagementBackgroundService(
        ConnectionMultiplexer connectionMultiplexer,
        RedisConnectionProvider redisConnectionProvider,
        RedisHelper redisHelper,
        Random random,
        IOptions<SystemOptions> systemOptions,
        IOptions<RedisOptions> redisOptions,
        ILogger<BattleResourceManagementBackgroundService> logger)
    {
        _redisOptions = redisOptions.Value;
        _redisConnectionProvider = redisConnectionProvider;
        _connectionMultiplexer = connectionMultiplexer;
        _numberOfEntriesToReadEachIteration = systemOptions.Value.NumberOfBattleResultsEntriesToReadEachIteration;
        _redisHelper = redisHelper;
        _systemOptions = systemOptions.Value;
        _random = random;
        _logger = logger;
        _goldScoreRatio = systemOptions.Value.LeaderboardGoldScoreRatio;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var subscriber = _connectionMultiplexer.GetSubscriber();
        var playerCollection = _redisConnectionProvider.RedisCollection<Domain.Player>();
        var leaderboardCollection =  _redisConnectionProvider.RedisCollection<LeaderboardEntry>();
        
        try
        {
            var lastIdsConsumed = new ConcurrentBag<RedisValue>();
            while (!stoppingToken.IsCancellationRequested)
            {
                if ((lastIdsConsumed?.Count ?? 0) > 0)
                {
                    await database.StreamAcknowledgeAsync(
                        _redisOptions.BattleReporterStreamName,
                        _redisOptions.BattleReporterGroupName,
                        lastIdsConsumed?.ToArray() ?? []);
                    
                    lastIdsConsumed = [];
                }

                var results = await database.StreamReadGroupAsync(
                    _redisOptions.BattleReporterStreamName,
                    _redisOptions.BattleReporterGroupName,
                    consumerName: nameof(BattleResourceManagementBackgroundService),
                    position: ">",
                    count: _numberOfEntriesToReadEachIteration);

                if (results.Length > 0)
                {
                    var tasks = new List<Task>(results.Select(async result =>
                    {
                        var battleResult = ParseBattleResult(result);
                        var requesterPlayer = await playerCollection.FindByIdAsync(battleResult.Requester.UserId);
                        var opponentPlayer = await playerCollection.FindByIdAsync(battleResult.Opponent.UserId);

                        if (requesterPlayer is null || opponentPlayer is null)
                        {
                            // Discard the battle and drop it from the queue, since it's not valid. We could report the
                            // players or just wait until the API timeout them. Better to notify them but let's keep this simple for the challenge.
                            return;
                        }
                        
                        var requesterReport = await ExecuteReportGenerationAsync(
                            battleResult, 
                            requesterPlayer, 
                            opponentPlayer, 
                            playerCollection, 
                            leaderboardCollection);

                        var serializedBattleReport = Convert.ToBase64String(MessagePackSerializer.Serialize(requesterReport));
                        var channel = _redisHelper.GetReportChannelFor(requesterPlayer.Id, opponentPlayer.Id);
                        await subscriber.PublishAsync(channel, new RedisValue(serializedBattleReport));
                        
                        lastIdsConsumed?.Add(result.Id);
                    }));
                    
                    await Task.WhenAll(tasks);
                }

                await Task.Delay(10, stoppingToken); // TODO: Avoid exhaustion. It can be remove but I need to test that first
            }
        }
        catch (Exception e)
        {
            _logger.LogError("There was an error: {ErrorMessage}. Waiting some time before trying it again. ", e.Message);
            await Task.Delay(500, stoppingToken); // This can be improved using something like Polly. Give it sometime to recover, if not, some alarms should be raised.
        }
    }

    private async Task<BattleReport> ExecuteReportGenerationAsync(
        BattleResult battleResult, 
        Domain.Player requesterPlayer, 
        Domain.Player opponentPlayer, 
        IRedisCollection<Domain.Player> playerCollection, 
        IRedisCollection<LeaderboardEntry> leaderboardCollection)
    {
        BattleReport requesterReport;
        double percentageGained = _random.Next(
            _systemOptions.PercentageGainedAfterVictoryLowerLimitInclusive,
            _systemOptions.PercentageGainedAfterVictoryUpperLimitInclusive + 1);
        
        switch (battleResult.Requester.Outcome)
        {
            case BattleOutcome.Victory:
            {
                var (amountOfGold, amountOfSilver) = await CalculateAndUpdateAsync(
                    requesterPlayer,
                    opponentPlayer,
                    percentageGained,
                    playerCollection,
                    leaderboardCollection);

                requesterReport = new BattleReport
                {
                    UserId = battleResult.Requester.UserId,
                    BattleResult = battleResult.Requester,
                    GainedGold = amountOfGold,
                    GainedSilver = amountOfSilver
                };
                break;
            }

            case BattleOutcome.Defeat:
            {
                var (amountOfGold, amountOfSilver) = await CalculateAndUpdateAsync(
                    opponentPlayer,
                    requesterPlayer,
                    percentageGained,
                    playerCollection,
                    leaderboardCollection);

                requesterReport = new BattleReport
                {
                    UserId = battleResult.Requester.UserId,
                    BattleResult = battleResult.Requester,
                    LostGold = amountOfGold,
                    LostSilver = amountOfSilver
                };
                break;
            }

            default:
                requesterReport = new BattleReport
                {
                    UserId = battleResult.Requester.UserId,
                    BattleResult = battleResult.Requester
                };
                break;
        }

        return requesterReport;
    }

    // TODO: I don't like it. Too many side-effects. If I've time left, I'll refactor it for sure. 
    // At the moment Redis OM doesn't have transactions. I'll need to discard it, find an alternative or implement them myself.
    private static async Task<(int amountOfGold, int amountOfSilver)> CalculateAndUpdateAsync(
        Domain.Player winnerPlayer,
        Domain.Player loserPlayer,
        double percentageToTake, 
        IRedisCollection<Domain.Player> playerCollection,
        IRedisCollection<LeaderboardEntry> leaderboardCollection)
    {
        var amountOfGold = Convert.ToInt32((loserPlayer.Gold * percentageToTake) / 100);
        var amountOfSilver = Convert.ToInt32((loserPlayer.Silver * percentageToTake) / 100);
                        
        // TODO: Create a Report Collection if needed.
        // And make Player class immutable
        winnerPlayer.Gold += amountOfGold;
        winnerPlayer.Silver += amountOfSilver;
                        
        loserPlayer.Gold = loserPlayer.Gold - amountOfGold < 0 ? 0 : loserPlayer.Gold - amountOfGold;
        loserPlayer.Silver = loserPlayer.Silver - amountOfSilver < 0 ? 0 : loserPlayer.Silver - amountOfSilver;
        
        var score = CalculateScore(amountOfGold, amountOfSilver); // TODO: Extract this logic
        var winnerLeaderboardEntry = await leaderboardCollection.FindByIdAsync(winnerPlayer.Id);
        if (winnerLeaderboardEntry is null)
        {
            winnerLeaderboardEntry = new LeaderboardEntry
            {
                Id = winnerPlayer.Id,
                UserName = winnerPlayer.UserName,
                Score = score
            };
            
            // This if-else is due some weird behavior that Redis OM is having... I need to check on this.
            await leaderboardCollection.InsertAsync(winnerLeaderboardEntry);
        }
        else
        {
            winnerLeaderboardEntry.Score += score;
            await leaderboardCollection.SaveAsync();
        }
        
        await playerCollection.SaveAsync();
        return (amountOfGold, amountOfSilver);
    }

    private static int CalculateScore(int amountOfGold, int amountOfSilver) => (amountOfGold * _goldScoreRatio) + amountOfSilver;

    private BattleResult ParseBattleResult(StreamEntry result)
    {
        var entry = result.Values.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        return MessagePackSerializer.Deserialize<BattleResult>(Convert.FromBase64String(entry["data"]));
    }
}