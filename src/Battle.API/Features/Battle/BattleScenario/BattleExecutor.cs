using Battle.API.Options;
using Microsoft.Extensions.Options;

namespace Battle.API.Features.Battle.BattleScenario;

public class BattleExecutor
{
    private readonly IBattleRuler _battleRuler;
    private readonly int _maximumTurns;

    public BattleExecutor(IOptions<BattleStatsOptions> options, IBattleRuler battleRuler)
    {
        _battleRuler = battleRuler;
        _maximumTurns = options.Value.MaximumTurnsBattleDuration;
    }
    
    public BattleResult Execute(Domain.Player requester, Domain.Player opponent)
    {
        var currentRequester = BattlePlayerCurrentStatus.FromPlayer(requester);
        var currentOpponent = BattlePlayerCurrentStatus.FromPlayer(opponent);
        
        var actions = new List<BattleAction>();

        var turn = 0; 
        do
        {
            switch (turn % 2)
            {
                case 0:
                {
                    var (action, hitPointsLeft, attackLeft) = ExecuteTurn(currentRequester, currentOpponent);
                    actions.Add(action);

                    if (action.Action is BattleAction.ActionType.Hit)
                    {
                        currentOpponent.HitPointsLeft = hitPointsLeft;
                        currentOpponent.AttackLeft = attackLeft;
                    }
                    
                    turn++;
                    break;   
                }
                
                case 1:
                {
                    var (action, hitPointsLeft, attackLeft) = ExecuteTurn(currentOpponent, currentRequester);
                    actions.Add(action);
                    
                    if (action.Action is BattleAction.ActionType.Hit)
                    {
                        currentRequester.HitPointsLeft = hitPointsLeft;
                        currentRequester.AttackLeft = attackLeft;
                    }
                    
                    turn++;
                    break;   
                }
            }
        }
        while (currentRequester.HitPointsLeft > 0 && currentOpponent.HitPointsLeft > 0 && turn < _maximumTurns);

        var requesterReport = new BattleResultPlayer
        {
            UserId = requester.Id,
            Outcome = GetOutcome(currentRequester.HitPointsLeft, currentOpponent.HitPointsLeft),
            Actions = actions
        };
        
        var opponentReport = new BattleResultPlayer
        {
            UserId = opponent.Id,
            Outcome = GetOutcome(currentOpponent.HitPointsLeft, currentRequester.HitPointsLeft),
            Actions = actions
        };
        
        return new BattleResult { Requester = requesterReport, Opponent = opponentReport };
    }

    private (BattleAction action, int hitPointsLeft, int attackLeft) ExecuteTurn(
        BattlePlayerCurrentStatus attacker, 
        BattlePlayerCurrentStatus receiver)
    {
        if (_battleRuler.IsHit(receiver))
        {
            var action = new BattleAction
            {
                From = attacker.Id,
                To = receiver.Id,
                Action = BattleAction.ActionType.Hit,
                Damage = attacker.AttackLeft
            };

            var hitPointsLeft = receiver.HitPointsLeft - _battleRuler.CalculateDamage(attacker);
            var attackLeft = _battleRuler.CalculateNewAttack(receiver, attacker);

            return (action, hitPointsLeft, attackLeft);
        }

        var actionMiss = new BattleAction
        {
            From = attacker.Id,
            To = receiver.Id,
            Action = BattleAction.ActionType.Miss
        };

        return (actionMiss, receiver.HitPointsLeft, receiver.AttackLeft);
    }

    private static BattleOutcome GetOutcome(int requesterHitPoints, int opponentHitPoints) =>
        requesterHitPoints switch
        {
            > 0 when opponentHitPoints > 0 => BattleOutcome.Draw,
            <= 0 when opponentHitPoints <= 0 => BattleOutcome.Draw,
            <= 0 => BattleOutcome.Defeat,
            _ => BattleOutcome.Victory
        };
}