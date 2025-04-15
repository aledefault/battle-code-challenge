using Redis.OM.Modeling;

namespace Battle.API.Domain;

[Document(StorageType = StorageType.Json, Prefixes = [nameof(LeaderboardEntry)])]
public class LeaderboardEntry
{
    [RedisIdField, Indexed] 
    public string Id { get; set; }
    
    [Indexed(Sortable = true)] 
    public string UserName { get; set; }
    
    [Indexed(Sortable = true)] 
    public long Score { get; set; }
}