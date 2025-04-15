using Battle.API.Options;
using Microsoft.Extensions.Options;

namespace Battle.API.Features.Battle.BattleScenario;

public class BattleRuler(IOptions<BattleStatsOptions> battleStatsConfig, Random random) : IBattleRuler
{
    public bool IsHit(BattlePlayerCurrentStatus receiver)
    {
        var dodgeProbability = ((battleStatsConfig.Value.MaxDefense + 1) - (double)receiver.OriginalDefense) / battleStatsConfig.Value.MaxDefense;
        return random.NextDouble() <= dodgeProbability;
    }
    
    public int CalculateDamage(BattlePlayerCurrentStatus attacker) => attacker.AttackLeft;
    
    public int CalculateNewAttack(BattlePlayerCurrentStatus receiver, BattlePlayerCurrentStatus attacker)
    {
        var cap = receiver.OriginalAttack * .5f;
        var percentageTaken = attacker.AttackLeft / (double)receiver.HitPoints;
        var newAttack = receiver.AttackLeft - (receiver.AttackLeft * percentageTaken);

        return Convert.ToInt32(newAttack > cap ? newAttack : cap);
    }
}