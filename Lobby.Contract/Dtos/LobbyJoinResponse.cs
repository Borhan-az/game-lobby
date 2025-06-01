namespace Lobby.Contract.Dtos
{
    public class LobbyJoinResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public string? LobbyId { get; set; }
    }
}