namespace Lobby.Contract.Dtos
{
    public class LobbyJoinRequest
    {
        public string PlayerId { get; set; } = string.Empty;
        public string? PreferredLobbyId { get; set; }
    }
}