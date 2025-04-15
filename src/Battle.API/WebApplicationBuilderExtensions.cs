using System.Reflection;
using Battle.API.Domain;
using Battle.API.Options;
using Microsoft.Extensions.Options;
using Redis.OM;
using Redis.OM.Modeling;
using StackExchange.Redis;

namespace Battle.API;

public static class WebApplicationBuilderExtensions
{
    public static WebApplication InitializeRedis(this WebApplication app)
    {
        Task.Run(async () =>
        {
            var provider = app.Services.GetService<RedisConnectionProvider>() ?? throw new NullReferenceException(nameof(RedisConnectionProvider));
            var connectionMultiplexer = app.Services.GetService<ConnectionMultiplexer>() ?? throw new NullReferenceException(nameof(RedisConnectionProvider));
            
            // This will do a dirty migration. Avoid in production. But since this is for a challenge I'll leave like this to
            // avoid any kind of errors.
            var currentApp = typeof(Program).Assembly;
            foreach (var type in currentApp.GetTypes().Where(x => x.GetCustomAttributes<DocumentAttribute>().Any()))
            {
                if (!await provider.Connection.IsIndexCurrentAsync(type))
                {
                    await provider.Connection.DropIndexAsync(type);
                    await provider.Connection.CreateIndexAsync(type);
                }
            }

            var redisOptions = app.Services.GetService<IOptions<RedisOptions>>().Value;
            var db = connectionMultiplexer.GetDatabase();
            
            if (!await db.KeyExistsAsync(redisOptions.BattleExecutorStreamName) ||
                (await db.StreamGroupInfoAsync(redisOptions.BattleExecutorStreamName)).All(x => x.Name != redisOptions.BattleExecutorGroupName))
            {
                await db.StreamCreateConsumerGroupAsync(
                    redisOptions.BattleExecutorStreamName,
                    redisOptions.BattleExecutorGroupName,
                    position: "0-0",
                    createStream: true);
            }

            if (!await db.KeyExistsAsync(redisOptions.BattleReporterStreamName) ||
                (await db.StreamGroupInfoAsync(redisOptions.BattleReporterStreamName)).All(x => x.Name != redisOptions.BattleReporterGroupName))
            {
                await db.StreamCreateConsumerGroupAsync(
                    redisOptions.BattleReporterStreamName,
                    redisOptions.BattleReporterGroupName,
                    position: "0-0",
                    createStream: true);
            }
        }).Wait();
        
        return app;
    }

    /// <summary>
    /// Insert 200 mock Players and LeaderboardEntries.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    public static WebApplication MockSeedRedis(this WebApplication app)
    {
        Task.Run(async () =>
        {
            var connectionMultiplexer = app.Services.GetService<ConnectionMultiplexer>() ?? throw new NullReferenceException(nameof(RedisConnectionProvider));
            var result = await connectionMultiplexer.GetDatabase().StringGetAsync("Seeded");
            if (result == 1)
                return;

            var provider = app.Services.GetService<RedisConnectionProvider>() ?? throw new NullReferenceException(nameof(RedisConnectionProvider));
            var playerCollection = provider.RedisCollection<Domain.Player>();
            var leaderboardCollection = provider.RedisCollection<LeaderboardEntry>();

            var insertTasks = new List<Task>();

            for (var i = 1; i <= 200; i++)
            {
                var id = PseudoUuid(i);
                var gold = (i * 73) % 4501 + 500;
                var silver = (i * 29) % 19001 + 1000;
                var attack = (i * 37) % 151;
                var defense = 150 - attack; // ensures Attack + Defense == 150
                var hitPoints = (i * 53) % 401 + 100;

                var player = new Domain.Player
                {
                    Id = id,
                    UserName = $"player{i}",
                    Description = $"Random description {i}",
                    Gold = gold,
                    Silver = silver,
                    Attack = attack,
                    Defense = defense,
                    HitPoints = hitPoints
                };
                
                var leaderboardEntry = new LeaderboardEntry
                {
                    Id = id,
                    UserName = $"player{i}",
                    Score = (i * 97) % 10000 + 1000
                };
                
                insertTasks.AddRange([playerCollection.InsertAsync(player), leaderboardCollection.InsertAsync(leaderboardEntry)]);
            }

            await Task.WhenAll(insertTasks);
            await connectionMultiplexer.GetDatabase().StringSetAsync("Seeded", true);
        }).Wait();

        return app;

        static string PseudoUuid(int i)
        {
            var part5 = (i + 4000) % 281474976710656;
            return $"{i:x8}-{((i + 1000) % 65536):x4}-{((i + 2000) % 65536):x4}-{((i + 3000) % 65536):x4}-{part5:x12}";
        }
    }
}