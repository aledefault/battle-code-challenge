using Battle.API.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Battle.API.Infrastructure;

public class RedisHelper(IOptions<RedisOptions> redisOptions)
{
    public RedisChannel GetReportChannelFor(string requesterId, string opponentId) =>
        new ($"{redisOptions.Value.BattleReportChannel}:{requesterId}:{opponentId}", RedisChannel.PatternMode.Literal);
}