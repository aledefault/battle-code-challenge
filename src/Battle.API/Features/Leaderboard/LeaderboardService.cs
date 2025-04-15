using System.Security.Claims;
using Battle.API.Domain;
using Battle.API.Options;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Redis.OM;
using Redis.OM.Searching;

namespace Battle.API.Features.Leaderboard;

public class LeaderboardService : API.Leaderboard.LeaderboardBase
{
    private readonly LeaderboardOptions _leaderboardOptions;
    private readonly IRedisCollection<LeaderboardEntry> _leaderboardCollection;

    public LeaderboardService(
        RedisConnectionProvider redisConnectionProvider,
        IOptions<LeaderboardOptions> leaderboardOptions)
    {
        _leaderboardOptions = leaderboardOptions.Value;
        _leaderboardCollection = redisConnectionProvider.RedisCollection<LeaderboardEntry>();
    }

    [Authorize]
    public override async Task<LeaderboardResponse> GetAll(LeaderboardRequest request, ServerCallContext context)
    {
        var items = await _leaderboardCollection
            .OrderByDescending(x => x.Score)
            // .ThenByDescending(x => x.UserName) Not Implemented by Redis OM yet. See https://github.com/redis/redis-om-node/issues/123
            .Take(_leaderboardOptions.MaximumNumberOfEntriesToRetrieve)
            .ToListAsync();

        // This is due a limitation of Redis OM. The entries are already in memory, so this is almost free.
        var orderedEntries = items.OrderByDescending(x => x.Score).ThenByDescending(x => x.UserName).Select((x, i) => new LeaderboardItem
        {
            UserId = x.Id,
            Username = x.UserName,
            Position = i + 1,
            Score = x.Score
        });
        
        var response = new LeaderboardResponse();
        response.Entries.AddRange(orderedEntries);
        
        var currentUserId = context.GetHttpContext().User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _leaderboardCollection.FindByIdAsync(currentUserId);
        if (user is not null && !items.Any(x => x.Id.Equals(user.Id)))
        {
            var position = await _leaderboardCollection.CountAsync(x => x.Score > user.Score);
            response.Entries.Add(new LeaderboardItem
            {
                UserId = user.Id,
                Username = user.UserName,
                Position = position,
                Score = user.Score
            });
        }

        return response;
    }
}