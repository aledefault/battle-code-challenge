namespace Battle.API.Features.Player;

public static class PlayerMapper
{
    public static NewPlayerResponse ToNewPlayerResponse(Domain.Player player)
    {
        return new NewPlayerResponse
        {
            Id = player.Id,
            Username = player.UserName,
            Description = player.Description,
            Gold = player.Gold,
            Silver = player.Silver,
            Attack = player.Attack,
            Defense = player.Defense,
            HitPoints = player.HitPoints
        };
    }
}