using Lobby.Contract.Dtos;

namespace Lobby.Contract.Interfaces
{
    public interface ILobbyService
    {
        Task<LobbyJoinResponse> JoinLobbyAsync(string playerId, string? preferredLobbyId = null);
        Task<IEnumerable<LobbyInfo>> GetActiveLobbiesAsync();

    }
}