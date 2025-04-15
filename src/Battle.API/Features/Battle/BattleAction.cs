using MessagePack;

namespace Battle.API.Features.Battle;

[MessagePackObject]
public class BattleAction
{
    public enum ActionType 
    {
        Hit = 0,
        Miss
    }

    [Key(0)]
    public string From { get; set; }
    
    [Key(1)]
    public string To { get; set; }

    [Key(2)]
    public int Damage { get; set; } = 0;

    [Key(3)]
    public ActionType Action { get; set; }
}