namespace Battle.API.Options;

public class SystemOptions
{
    public const string Key = "System";

    public int WaitingBattleResulBeforeTimeoutInSeconds { get; set; } = 10;
    public int NumberOfBattleEntriesToReadEachIteration { get; set; } = 50;
    public int NumberOfBattleResultsEntriesToReadEachIteration { get; set; } = 50;

    public int PercentageGainedAfterVictoryLowerLimitInclusive { get; set; } = 5;
    public int PercentageGainedAfterVictoryUpperLimitInclusive { get; set; } = 10;
    public int PlayerLockTimeoutInMilliseconds { get; set; } = 5000;
    public int PlayerLockWaitInMilliseconds { get; set; } = 3000;
    public int PlayerLockRetryInMilliseconds { get; set; } = 500;
    public int LeaderboardGoldScoreRatio { get; set; } = 10;
}