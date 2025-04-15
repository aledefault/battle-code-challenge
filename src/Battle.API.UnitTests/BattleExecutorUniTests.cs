using Battle.API.Features.Battle;
using Battle.API.Features.Battle.BattleScenario;
using Battle.API.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Battle.API.UnitTests;

public class BattleExecutorUniTests
{
    private readonly IOptions<BattleStatsOptions> _defaultBattleStats;

    public BattleExecutorUniTests()
    {
        _defaultBattleStats = Microsoft.Extensions.Options.Options.Create(new BattleStatsOptions());
    }
    
    [Fact]
    public void Should_execute_a_battle_that_requester_win_in_one_hit()
    {
        var player1 = new Domain.Player
        {
            Id = "1",
            HitPoints = 100,
            Attack = 70,
            Defense = 50
        };
        
        var player2 = new Domain.Player
        {
            Id = "2",
            HitPoints = 100,
            Attack = 60,
            Defense = 30
        };
        
        var player1Status = BattlePlayerCurrentStatus.FromPlayer(player1);
        var player2Status = BattlePlayerCurrentStatus.FromPlayer(player2);
        
        // 1 round requester winning
        var fakeBattleLogic = Substitute.For<IBattleRuler>();
        fakeBattleLogic.IsHit(IsPlayerStatus(player2Status)).Returns(true);
        fakeBattleLogic.CalculateDamage(IsPlayerStatus(player1Status)).Returns(player2Status.HitPoints);
        fakeBattleLogic
            .CalculateNewAttack(Arg.Any<BattlePlayerCurrentStatus>(), Arg.Any<BattlePlayerCurrentStatus>())
            .Returns(0);
        
        var result = new BattleExecutor(_defaultBattleStats, fakeBattleLogic);
        var battleResult = result.Execute(player1, player2);

        battleResult.Requester.Outcome.Should().Be(BattleOutcome.Victory);
        battleResult.Opponent.Outcome.Should().Be(BattleOutcome.Defeat);

        battleResult.Requester.Actions.Should().BeEquivalentTo(battleResult.Opponent.Actions);
        battleResult.Opponent.Actions.Should().BeEquivalentTo(new List<BattleAction>
        {
            new() { Action = BattleAction.ActionType.Hit, From = player1.Id, To = player2.Id, Damage = player1.Attack }
        });
    }
    
    [Fact]
    public void Should_execute_a_battle_that_requester_lose_in_one_hit()
    {
        var player1 = new Domain.Player
        {
            Id = "1",
            HitPoints = 100,
            Attack = 70,
            Defense = 50
        };
        
        var player2 = new Domain.Player
        {
            Id = "2",
            HitPoints = 100,
            Attack = 60,
            Defense = 30
        };
        
        var player1Status = BattlePlayerCurrentStatus.FromPlayer(player1);
        var player2Status = BattlePlayerCurrentStatus.FromPlayer(player2);
        
        // 1 round opponent winning
        var fakeBattleLogic = Substitute.For<IBattleRuler>();
        fakeBattleLogic.IsHit(IsPlayerStatus(player2Status)).Returns(false);
        fakeBattleLogic.IsHit(IsPlayerStatus(player1Status)).Returns(true);
        fakeBattleLogic.CalculateDamage(IsPlayerStatus(player2Status)).Returns(player1Status.HitPoints);
        fakeBattleLogic
            .CalculateNewAttack(Arg.Any<BattlePlayerCurrentStatus>(), Arg.Any<BattlePlayerCurrentStatus>())
            .Returns(0);
        
        var result = new BattleExecutor(_defaultBattleStats, fakeBattleLogic);
        var battleResult = result.Execute(player1, player2);

        battleResult.Requester.Outcome.Should().Be(BattleOutcome.Defeat);
        battleResult.Opponent.Outcome.Should().Be(BattleOutcome.Victory);

        battleResult.Requester.Actions.Should().BeEquivalentTo(battleResult.Opponent.Actions);
        battleResult.Requester.Actions.Should().BeEquivalentTo(new List<BattleAction>
        {
            new() { Action = BattleAction.ActionType.Miss, From = player1.Id, To = player2.Id, Damage = 0 },
            new() { Action = BattleAction.ActionType.Hit, From = player2.Id, To = player1.Id, Damage = player2.Attack }
        });
    }

    private static BattlePlayerCurrentStatus IsPlayerStatus(BattlePlayerCurrentStatus player2) => 
        Arg.Is<BattlePlayerCurrentStatus>(x => x.Id == player2.Id);

    [Fact]
    public void Should_end_an_infinite_battle_in_draw()
    {
        var player1 = new Domain.Player { UserName = "Gordon Freeman", HitPoints = 1 };
        var player2 = new Domain.Player { UserName = "GMan", HitPoints = 1 };
        
        // Infinity War
        var fakeBattleLogic = Substitute.For<IBattleRuler>();
        
        var result = new BattleExecutor(_defaultBattleStats, fakeBattleLogic);
        var battleResult = result.Execute(player1, player2);

        battleResult.Requester.Outcome.Should().Be(BattleOutcome.Draw);
        battleResult.Opponent.Outcome.Should().Be(BattleOutcome.Draw);

        battleResult.Requester.Actions.Should().HaveCount(_defaultBattleStats.Value.MaximumTurnsBattleDuration);
        battleResult.Requester.Actions.Should().BeEquivalentTo(battleResult.Opponent.Actions);
    }
}