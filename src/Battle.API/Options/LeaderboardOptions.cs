namespace Battle.API.Options;

public class LeaderboardOptions
{
    public const string Key = "Leaderboard";

    public int MaximumNumberOfEntriesToRetrieve { get; set; } = 20;
}