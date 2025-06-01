using Lobby.Contract.Dtos;
using RedLockNet;
using StackExchange.Redis;

namespace Lobby.Contract.Interfaces
{
    public interface IRedisService
    {
        Task<IRedLock?> AcquireDistributedLockAsync(string resource, TimeSpan expiry);

        Task<long> GetLobbyPlayerCountAsync(string lobbyId);

        Task<bool> AddPlayerToLobbyAsync(string lobbyId, string playerId, int maxPlayersPerLobby);

        Task<string?> FindOrCreateOpenLobbyAsync(int maxPlayersPerLobby, int maxTotalLobbies);

        Task<string?> GetPlayerLobbyAsync(string playerId);

        Task SetPlayerLobbyAsync(string playerId, string lobbyId);
        

        Task PublishAsync(string channel, string message);

        Task SubscribeAsync(string channel, Action<RedisChannel, RedisValue> handler);
        Task UnsubscribeAsync(string channel);

        Task<IEnumerable<string>> GetActiveLobbyIdsAsync(); 
        Task<IEnumerable<LobbyInfo>> GetLobbyDetailsAsync(IEnumerable<string> lobbyIds); 

    }
}