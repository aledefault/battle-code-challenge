using Battle.Cli.Services;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Rendering;
using FigletText = Spectre.Console.FigletText;

namespace Battle.Cli;

public class ConsoleHostedService : IHostedService
{
    private readonly TokenProvider _tokenProvider;
    private readonly CurrentUserService _currentUserService;
    private readonly Leaderboard.LeaderboardClient _leaderboardClient;
    private readonly Battle.BattleClient _battleClient;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly Player.PlayerClient _playerClient;

    public ConsoleHostedService(
        IHostApplicationLifetime lifetime,
        TokenProvider tokenProvider,
        CurrentUserService currentUserService,
        Player.PlayerClient playerClient,
        Battle.BattleClient battleClient,
        Leaderboard.LeaderboardClient leaderboardClient)
    {
        _lifetime = lifetime;
        _playerClient = playerClient;
        _battleClient = battleClient;
        _tokenProvider = tokenProvider;
        _currentUserService = currentUserService;
        _leaderboardClient = leaderboardClient;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        ShowBattleTittle();

        var (isSuccessful, panel) = await ShowRegisterMenuAsync(cancellationToken);
        if (!isSuccessful)
            return;

        if (_currentUserService.CurrentUser.Identity?.IsAuthenticated ?? false)
        {
            AnsiConsole.Clear();
            ShowBattleTittle();
            if (panel is not null)
                AnsiConsole.Write(panel);

            var registerTable = new Table()
                .AddColumn("Field")
                .AddColumn("Description")
                .AddRow("(1) Battle", "Fights against your enemieeeees!")
                .AddRow("(2) Leaderboard", "Gets the leaderboard and see if you're the stronger one!")
                .AddRow("(3) Quit", "I mean... just that.");

            AnsiConsole.Write(
                new Panel(registerTable)
                    .Header("[bold]Options[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Blue)
            );

            MainMenu choice;
            do
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _lifetime.StopApplication();
                    return;
                }

                choice = AnsiConsole.Prompt(
                    new SelectionPrompt<MainMenu>()
                        .Title("[bold]Choose an option:[/]")
                        .AddChoices(MainMenu.Battle, MainMenu.Leaderboard, MainMenu.Quit));

                switch (choice)
                {
                    case MainMenu.Battle:
                    {
                        await ShowBattleAsync(cancellationToken);
                        break;
                    }

                    case MainMenu.Leaderboard:
                    {
                        await ShowLeaderboardAsync(cancellationToken);
                        break;
                    }
                }
            } while (choice != MainMenu.Quit);

            _lifetime.StopApplication();
        }
    }

    private static void ShowBattleTittle()
    {
        AnsiConsole.MarkupLine("⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️");
        var asciiArt = new FigletText("BATTLE")
            .Color(Color.Red);

        AnsiConsole.Write(asciiArt);
        AnsiConsole.MarkupLine("[red bold]  ⚔️ Ctrl + c kill silently the process!  ⚔️[/]");
        AnsiConsole.MarkupLine("⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️️⚔️⚔️⚔️⚔️⚔️️⚔️⚔️️⚔️⚔️⚔️️⚔️⚔️⚔️⚔️⚔️⚔️⚔️⚔️");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    private async Task ShowLeaderboardAsync(CancellationToken cancellationToken)
    {
        try
        {
            AnsiConsole.Clear();
            var result = await _leaderboardClient.GetAllAsync(new LeaderboardRequest(), cancellationToken: cancellationToken);

            if (result?.Entries is { Count: > 0 })
            {
                var table = new Table()
                    .AddColumn("Position")
                    .AddColumn("Username")
                    .AddColumn("Score")
                    .AddColumn("User Id (Fight against the top players!)");

                foreach (var entry in result.Entries)
                {
                    if (entry.UserId.Equals(_currentUserService.UserId))
                    {
                        table.AddRow(
                            $"[bold yellow]{entry.Position}[/]",
                            $"[bold yellow]{entry.Username}[/]",
                            $"[bold yellow]{entry.Score}[/]",
                            $"[bold yellow]{entry.UserId}[/]");
                    }
                    else
                        table.AddRow(entry.Position.ToString(), entry.Username, entry.Score.ToString(), entry.UserId);
                }

                AnsiConsole.Write(
                    new Panel(table)
                        .Header("[bold]Leaderboard[/]")
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Gold1));
            }
            else
            {
                AnsiConsole.MarkupLine("[bold red]No one is playing the game...[/]");
            }
        }
        catch (RpcException ex)
        {
            AnsiConsole.MarkupLine($"[bold red]There was an error:[/] [bold yellow]{ex.Status.Detail}[/]");
        }
    }

    private async Task ShowBattleAsync(CancellationToken cancellationToken)
    {
        try
        {
            AnsiConsole.Clear();
            ShowBattleTittle();

            // This is just because it's funny. Not recommended!
            var leaderboardResponse = await _leaderboardClient.GetAllAsync(new LeaderboardRequest(), cancellationToken: cancellationToken);
            if (leaderboardResponse?.Entries is { Count: > 0 })
            {
                var you = leaderboardResponse.Entries.FirstOrDefault(x => x.UserId.Equals(_currentUserService.UserId));
                if (you is not null)
                    leaderboardResponse.Entries.Remove(you);

                var suggested = leaderboardResponse.Entries[new Random().Next(0, leaderboardResponse.Entries.Count)];
                AnsiConsole.MarkupLine($"[bold red]<<Suggestion>>: {suggested.UserId}[/] [bold yellow] Ranked {suggested.Position}![/]");
            }

            var opponentId = AnsiConsole.Ask<string>("Your Opponent Id:");

            var call = _battleClient.Submit(new BattleRequest { OpponentId = opponentId.Trim() }, cancellationToken: cancellationToken);
            while (await call.ResponseStream.MoveNext(cancellationToken))
            {
                AnsiConsole.WriteLine();
                var response = call.ResponseStream.Current;
                var table = new Table()
                    .AddColumn("Field")
                    .AddColumn("Description")
                    .AddRow("Username", response.Username)
                    .AddRow("Outcome", response.Outcome)
                    .AddRow("Damage Done", response.Damage.ToString())
                    .AddRow("Hits Misses", response.Misses.ToString())
                    .AddRow("Gold Gained", response.ResourcesGain.Gold.ToString())
                    .AddRow("Silver Gained", response.ResourcesGain.Silver.ToString())
                    .AddRow("Gold Lost", response.ResourcesLost.Gold.ToString())
                    .AddRow("Silver Lost", response.ResourcesLost.Silver.ToString());

                AnsiConsole.Write(
                    new Panel(table)
                        .Header("[bold]Battle report[/]")
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.DarkRed));
            }
        }
        catch (RpcException ex)
        {
            AnsiConsole.MarkupLine($"[bold red]There was an error:[/] [bold yellow]{ex.Status.Detail}[/]");
        }
    }

    private async Task<(bool, IRenderable?)> ShowRegisterMenuAsync(CancellationToken cancellationToken)
    {
        IdentityMenu choice;
        do
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _lifetime.StopApplication();
                return (false, null);
            }

            choice = AnsiConsole.Prompt(
                new SelectionPrompt<IdentityMenu>()
                    .Title("[bold]Choose an option:[/]")
                    .AddChoices(IdentityMenu.Login, IdentityMenu.Register, IdentityMenu.Quit)
            );

            switch (choice)
            {
                case IdentityMenu.Login:
                {
                    var accessToken = await _tokenProvider.GetTokenAsync(cancellationToken);
                    _currentUserService.SetCurrentUser(accessToken);
                    AnsiConsole.MarkupLine("\n[green]Login successful![/]");
                    return (true, null);
                }

                case IdentityMenu.Register:
                {
                    AnsiConsole.Clear();
                    var registerTable = new Table()
                        .AddColumn("Field")
                        .AddColumn("Description")
                        .AddRow("Username", "Choose a unique username")
                        .AddRow("Password", "Choose a strong password")
                        .AddRow("Email", "Your email address")
                        .AddRow("Description", "A short description about yourself")
                        .AddRow("Attack", "Your desired Attack. The sum of Attack and Defense can't be  greater than 150")
                        .AddRow("Defense", "Your desired Defense. The sum of Attack and Defense can't be  greater than 150");

                    AnsiConsole.Write(
                        new Panel(registerTable)
                            .Header("[bold]Register Form[/]")
                            .Border(BoxBorder.Rounded)
                            .BorderColor(Color.Green)
                    );

                    var username = AnsiConsole.Ask<string>("Enter your username:");
                    var password = AnsiConsole.Prompt(
                        new TextPrompt<string>("Enter your password:")
                            .Secret()
                    );
                    var email = AnsiConsole.Ask<string>("Enter your email:");
                    var description = AnsiConsole.Ask<string>("Enter your description:");
                    var attack = AnsiConsole.Ask<int>("Enter your attack:");
                    var defense = AnsiConsole.Ask<int>("Enter your defense:");

                    try
                    {
                        var result = await _playerClient.CreateAsync(new NewPlayerRequest
                        {
                            Username = username,
                            Password = password,
                            Email = email,
                            Description = description,
                            Attack = attack,
                            Defense = defense
                        });

                        if (result is not null)
                        {
                            AnsiConsole.MarkupLine("\n[green]Registration successful![/]");
                            var statsTable = new Table()
                                .AddColumn("Field")
                                .AddColumn("Your Stat")
                                .AddRow("Username", result.Username)
                                .AddRow("Description", result.Description)
                                .AddRow("Attack", result.Attack.ToString())
                                .AddRow("Defense", result.Defense.ToString());

                            var sheetPanel = new Panel(statsTable)
                                .Header("[bold]Your character sheet[/]")
                                .Border(BoxBorder.Rounded)
                                .BorderColor(Color.DarkRed);

                            var accessToken = await _tokenProvider.GetTokenAsync(cancellationToken);
                            _currentUserService.SetCurrentUser(accessToken);

                            AnsiConsole.MarkupLine("\n[green]Authentication successful![/]");
                            return (true, sheetPanel);
                        }

                        AnsiConsole.MarkupLine("\n[red]Something went horribly wrong...[/]");
                    }
                    catch (RpcException ex)
                    {
                        AnsiConsole.MarkupLine($"[bold red]There was an error:[/] [bold yellow]{ex.Status.Detail}[/]");
                        return (false, null);
                    }

                    break;
                }
            }
        } while (choice != IdentityMenu.Quit);

        _lifetime.StopApplication();
        return (false, null);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _ = await _tokenProvider.LogoutAsync(cancellationToken);
        AnsiConsole.MarkupLine("[blue]See you Space Cowboy...[/]");
    }

    private enum MainMenu
    {
        Battle = 0,
        Leaderboard,
        Quit
    }

    private enum IdentityMenu
    {
        Login = 0,
        Register,
        Quit
    }
}