namespace Lobby.Contract.Dtos;

public class LobbyInfo
{
    public string LobbyId { get; set; } = string.Empty;
    public long CurrentPlayers { get; set; }
    public int MaxCapacity { get; set; }
}
