namespace Battle.API.Options;

public class RedisOptions
{
    public const string Key = "Redis";

    public string ConfigurationString { get; init; }
    public string ConnectionString { get; init; }
    
    public string BattleExecutorStreamName { get; init; }
    public string BattleExecutorGroupName { get; init; }
    public string BattleReporterStreamName { get; init; }
    public string BattleReporterGroupName { get; init; }
    public string BattleReportChannel { get; init; }
}