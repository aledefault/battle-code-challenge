using Battle.Cli;
using Battle.Cli.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

builder.Services.AddHttpClient(Constants.Http2HttpclientName, client =>
{
    client.DefaultRequestVersion = new Version(2, 0);
});
    
builder.Services.AddSingleton<CurrentUserService>();
builder.Services.AddSingleton<TokenProvider>();
builder.Services
    .AddGrpcClient<Player.PlayerClient>(o =>
    {
        o.Address = new Uri(builder.Configuration["Endpoints:GrpcApiUrl"]);
    });

builder.Services
    .AddGrpcClient<Battle.Cli.Battle.BattleClient>(o =>
    {
        o.Address = new Uri(builder.Configuration["Endpoints:GrpcApiUrl"]);
    })
    .AddCallCredentials(async (context, metadata, serviceProvider) =>
    {
        var provider = serviceProvider.GetRequiredService<TokenProvider>();
        var token = await provider.GetTokenAsync(context.CancellationToken);
        metadata.Add("Authorization", $"Bearer {token}");
        
        var currentUser = serviceProvider.GetRequiredService<CurrentUserService>();
        currentUser.SetCurrentUser(token);
    });

builder.Services
    .AddGrpcClient<Leaderboard.LeaderboardClient>(o =>
    {
        o.Address = new Uri(builder.Configuration["Endpoints:GrpcApiUrl"]);
    })
    .AddCallCredentials(async (context, metadata, serviceProvider) =>
    {
        var provider = serviceProvider.GetRequiredService<TokenProvider>();
        var token = await provider.GetTokenAsync(context.CancellationToken);
        metadata.Add("Authorization", $"Bearer {token}");
        
        var currentUser = serviceProvider.GetRequiredService<CurrentUserService>();
        currentUser.SetCurrentUser(token);
    });

builder.Services.AddHostedService<ConsoleHostedService>();

await builder.Build().RunAsync();