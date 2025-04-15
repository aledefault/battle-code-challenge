namespace Battle.API.Features.Battle.BattleScenario;

public interface IBattleRuler
{
    bool IsHit(BattlePlayerCurrentStatus receiver);
    int CalculateDamage(BattlePlayerCurrentStatus attacker);
    int CalculateNewAttack(BattlePlayerCurrentStatus receiver, BattlePlayerCurrentStatus attacker);
}