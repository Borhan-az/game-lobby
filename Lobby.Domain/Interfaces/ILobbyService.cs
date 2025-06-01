using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lobby.Domain.Interfaces
{
    public interface ILobbyService
    {
        /// <summary>
        /// Attempts to join a player to an existing lobby or creates a new one.
        /// </summary>
        /// <param name="playerId">The unique ID of the player.</param>
        /// <param name="preferredLobbyId">Optional preferred lobby ID to join.</param>
        /// <returns>A LobbyJoinResponse indicating success or failure.</returns>
        Task<LobbyJoinResponse> JoinLobbyAsync(string playerId, string? preferredLobbyId = null);
    }
}