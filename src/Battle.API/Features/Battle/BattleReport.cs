using MessagePack;

namespace Battle.API.Features.Battle;

[MessagePackObject]
public class BattleReport
{
    [Key(0)]
    public string UserId { get; set; }
    [Key(1)]
    public BattleResultPlayer BattleResult { get; set; }
    [Key(2)]
    public int GainedGold { get; set; }
    [Key(3)]
    public int LostGold{ get; set; }
    [Key(4)]
    public int GainedSilver{ get; set; }
    [Key(5)]
    public int LostSilver{ get; set; }
    
}