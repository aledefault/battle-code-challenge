using MessagePack;
using Redis.OM.Modeling;

namespace Battle.API.Domain;

// TODO: Bad practice - Leak infrastructure into the domain mode : I'm keeping this for simplicity's sake
[MessagePackObject]
[Document(StorageType = StorageType.Json, Prefixes = [nameof(Player)])]
public class Player
{
   [Key(0)]
   [RedisIdField, Indexed] 
   public string Id { get; init; }
   
   [Key(1)]
   [Indexed] 
   public string UserName { get; init; }
   
   [Key(2)]
   [Indexed] 
   public string Description { get; init; }
   
   [Key(3)]
   [Indexed] 
   public int Gold { get; set; }
   
   [Key(4)]
   [Indexed] 
   public int Silver { get; set; }
   
   [Key(5)]
   [Indexed] 
   public int Attack { get; init; }
   
   [Key(6)]
   [Indexed] 
   public int Defense { get; init; }
   
   [Key(7)]
   [Indexed] 
   public int HitPoints { get; init; }
}