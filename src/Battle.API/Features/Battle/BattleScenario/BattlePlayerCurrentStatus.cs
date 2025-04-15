namespace Battle.API.Features.Battle.BattleScenario;

public class BattlePlayerCurrentStatus
{
    public string Id { get; set; }
    public int OriginalAttack { get; set; }
    public int AttackLeft { get; set; }
    public int OriginalDefense { get; set; }
    public int HitPoints { get; set; }
    public int HitPointsLeft { get; set; }

    public static BattlePlayerCurrentStatus FromPlayer(Domain.Player requester) =>
        new()
        {
            Id = requester.Id,
            OriginalAttack = requester.Attack,
            AttackLeft = requester.Attack,
            OriginalDefense = requester.Defense,
            HitPoints = requester.HitPoints,
            HitPointsLeft = requester.HitPoints
        };
}