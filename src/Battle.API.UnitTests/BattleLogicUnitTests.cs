using Battle.API.Features.Battle;
using Battle.API.Features.Battle.BattleScenario;
using Battle.API.Options;
using FluentAssertions;
using NSubstitute;

namespace Battle.API.UnitTests;

public class BattleLogicUnitTests
{
    private readonly BattleRuler _battleRuler;

    private readonly BattleStatsOptions _battleStatsConfig = new()
    {
        MinAttack = 1,
        MinDefense = 1,
        MaxAttack = 100,
        MaxDefense = 100
    };

    public BattleLogicUnitTests()
    {
        var fakeRandom = Substitute.For<Random>();
        fakeRandom.NextDouble().Returns(0.5);
        _battleRuler = new BattleRuler(Microsoft.Extensions.Options.Options.Create(_battleStatsConfig), fakeRandom);
    }

    [Theory, MemberData(nameof(GetDatForHitOrMiss))]
    public void Should_hit_or_miss(BattlePlayerCurrentStatus player, bool isHit) =>
        _battleRuler.IsHit(player).Should().Be(isHit);

    public static TheoryData<BattlePlayerCurrentStatus, bool> GetDatForHitOrMiss() =>
        new()
        {
            { new BattlePlayerCurrentStatus { OriginalDefense = 100 }, false },
            { new BattlePlayerCurrentStatus { OriginalDefense = 80 }, false },
            { new BattlePlayerCurrentStatus { OriginalDefense = 52 }, false },
            { new BattlePlayerCurrentStatus { OriginalDefense = 51 }, true },
            { new BattlePlayerCurrentStatus { OriginalDefense = 49 }, true },
            { new BattlePlayerCurrentStatus { OriginalDefense = 20 }, true },
            { new BattlePlayerCurrentStatus { OriginalDefense = 1 }, true },
            { new BattlePlayerCurrentStatus { OriginalDefense = 0 }, true },
        };

    [Theory, MemberData(nameof(GetDatForCalculateDamage))]
    public void Should_calculate_the_correct_damage(BattlePlayerCurrentStatus attacker) =>
        _battleRuler.CalculateDamage(attacker).Should().Be(attacker.AttackLeft);

    public static TheoryData<BattlePlayerCurrentStatus> GetDatForCalculateDamage() =>
    [
        new() { AttackLeft = 100 },
        new() { AttackLeft = 50 },
        new() { AttackLeft = 1 },
        new() { AttackLeft = 0 }
    ];

    [Theory, MemberData(nameof(GetDatForNewAttack))]
    public void Should_calculate_the_new_attack(
        BattlePlayerCurrentStatus receiver,
        BattlePlayerCurrentStatus attacker,
        int newAttack) =>
        _battleRuler.CalculateNewAttack(receiver, attacker).Should().Be(newAttack);

    public static TheoryData<BattlePlayerCurrentStatus, BattlePlayerCurrentStatus, int> GetDatForNewAttack() =>
        new()
        {
            {
                new BattlePlayerCurrentStatus { HitPoints = 100, OriginalAttack = 70, AttackLeft = 70 },
                new BattlePlayerCurrentStatus { AttackLeft = 10 },
                63
            },
            { // We rounded to the biggest int: 56.7 will be 57 
                new BattlePlayerCurrentStatus { HitPoints = 100, OriginalAttack = 70, AttackLeft = 63 },
                new BattlePlayerCurrentStatus { AttackLeft = 10 },
                57
            },
            {
                new BattlePlayerCurrentStatus { HitPoints = 100, OriginalAttack = 70, AttackLeft = 36 },
                new BattlePlayerCurrentStatus { AttackLeft = 10 },
                35
            },
            {
                new BattlePlayerCurrentStatus { HitPoints = 100, OriginalAttack = 70, AttackLeft = 22 },
                new BattlePlayerCurrentStatus { AttackLeft = 10 },
                35
            }
        };
}