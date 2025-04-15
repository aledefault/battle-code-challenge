using Battle.API;
using Battle.API.Features.Authentication;
using Battle.API.Features.Battle;
using Battle.API.Features.Battle.BattleScenario;
using Battle.API.Features.Leaderboard;
using Battle.API.Features.Player;
using Battle.API.Infrastructure;
using Battle.API.Options;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Redis.OM;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection(KeycloakOptions.Key));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.Key));
builder.Services.Configure<BattleStatsOptions>(builder.Configuration.GetSection(BattleStatsOptions.Key));
builder.Services.Configure<LeaderboardOptions>(builder.Configuration.GetSection(LeaderboardOptions.Key));
builder.Services.Configure<SystemOptions>(builder.Configuration.GetSection(SystemOptions.Key));

var keycloakOption = new KeycloakOptions();
builder.Configuration.GetSection(KeycloakOptions.Key).Bind(keycloakOption);

var redisOptions = new RedisOptions();
builder.Configuration.GetSection(RedisOptions.Key).Bind(redisOptions);


builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakOption.Authority;
        options.Audience = keycloakOption.Audience;
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Services.AddScoped<IValidator<NewPlayerRequest>, NewPlayerRequestValidator>();
builder.Services.AddSingleton(new Random(10)); // TODO: Deterministic random. Refactor this
builder.Services.AddSingleton<IBattleRuler, BattleRuler>();
builder.Services.AddSingleton<BattleExecutor>();
builder.Services.AddSingleton<RedisHelper>();

builder.Services.AddSingleton(ConnectionMultiplexer.Connect(redisOptions.ConfigurationString));
builder.Services.AddSingleton(new RedisConnectionProvider(redisOptions.ConnectionString));

builder.Services.AddHostedService<BattleExecutorBackgroundService>();
builder.Services.AddHostedService<BattleResourceManagementBackgroundService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthGroup().WithUrlEndpoint().WithTokenEndpoint().WithLogout();
app.MapGrpcService<PlayerService>();
app.MapGrpcService<BattleService>();
app.MapGrpcService<LeaderboardService>();
app.MapGrpcReflectionService(); // Should only be enabled in development.
app.InitializeRedis().MockSeedRedis(); // Part of it includes a migration that should be avoided on production
    
app.Run();