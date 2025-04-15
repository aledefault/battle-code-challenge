using MessagePack;

namespace Battle.API.Features.Battle;

public enum BattleOutcome { Victory, Defeat, Draw }

[MessagePackObject]
public class BattleResult
{
    [Key(0)]
    public BattleResultPlayer Requester { get; set; }
    
    [Key(1)]
    public BattleResultPlayer Opponent { get; set; }
}

[MessagePackObject]
public class BattleResultPlayer
{
    [Key(0)]
    public string UserId { get; set; }
    
    [Key(1)]
    public IList<BattleAction> Actions { get; set; } = [];
    
    [Key(2)]
    public BattleOutcome Outcome { get; set; }
}