namespace Battle.API.Options;

public class BattleStatsOptions
{
    public const string Key = "BattleStats";

    public int MinAttack { get; set; } = 1;
    public int MinDefense { get; set; } = 1;
    public int MaxAttack { get; set; } = 100;
    public int MaxDefense { get; set; } = 100;
    public int MaxPointsToDistribute { get; set; } = 150;
    public int BaseHitPoints { get; set; } = 100;
    public int MultiplicativeHitPoints { get; set; } = 2;
    public int InitialGold { get; set; } = 1_000;
    public int InitialSilver { get; set; } = 10_000;
    public int MaximumTurnsBattleDuration { get; set; } = 200;
}