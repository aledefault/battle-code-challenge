using MessagePack;

namespace Battle.API.Features.Battle;

[MessagePackObject]
public class BattlePlayers
{
    [Key(0)]
    public string RequesterId { get; set; }

    [Key(1)]
    public string OpponentId { get; set; }
}