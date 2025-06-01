using Lobby.Application.Models;
using Lobby.Contract.Dtos;
using Lobby.Contract.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lobby.Application.Services;

    public class LobbyService : ILobbyService
    {
        private readonly IRedisService _redisService;
        private readonly ILogger<LobbyService> _logger;
        private readonly LobbySetting _lobbySettings;

        private const string GlobalLobbyLockResource = "global_lobby_operation_lock";
        private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(10);

        public LobbyService(IRedisService redisService, ILogger<LobbyService> logger, IOptions<LobbySetting> lobbySettings)
        {
            _redisService = redisService;
            _logger = logger;
            _lobbySettings = lobbySettings.Value;
        }

        public async Task<LobbyJoinResponse> JoinLobbyAsync(string playerId, string? preferredLobbyId = null)
        {
            var existingLobbyId = await _redisService.GetPlayerLobbyAsync(playerId);
            if (!string.IsNullOrEmpty(existingLobbyId))
            {
                _logger.LogInformation("Player {PlayerId} is already in lobby {LobbyId}.", playerId, existingLobbyId);
                return new LobbyJoinResponse
                {
                    Success = true,
                    Message = $"You are already in lobby with ID {existingLobbyId}",
                    LobbyId = existingLobbyId
                };
            }

            using (var globalLock = await _redisService.AcquireDistributedLockAsync(GlobalLobbyLockResource, LockExpiry))
            {
                if (globalLock == null)
                {
                    _logger.LogWarning("Player {PlayerId}: Could not acquire global lobby lock. Concurrent request likely.", playerId);
                    return new LobbyJoinResponse { Success = false, Message = "System is busy. Please try again." };
                }

                string? targetLobbyId = null;

                if (!string.IsNullOrEmpty(preferredLobbyId))
                {
                    var currentPlayers = await _redisService.GetLobbyPlayerCountAsync(preferredLobbyId);
                    if (currentPlayers >= _lobbySettings.MaxPlayersPerLobby)
                    {
                        _logger.LogInformation("Player {PlayerId}: Preferred lobby {PreferredLobbyId} is full.", playerId, preferredLobbyId);
                        return new LobbyJoinResponse { Success = false, Message = $"Lobby {preferredLobbyId} is full." };
                    }
                    targetLobbyId = preferredLobbyId;
                }
                else
                {
                    targetLobbyId = await _redisService.FindOrCreateOpenLobbyAsync(
                        _lobbySettings.MaxPlayersPerLobby,
                        _lobbySettings.MaxTotalLobbies);

                    if (targetLobbyId == null)
                    {
                        _logger.LogInformation("Player {PlayerId}: No lobbies available and new lobby creation is blocked.", playerId);
                        return new LobbyJoinResponse { Success = false, Message = "No lobbies available and new lobby creation is blocked." };
                    }
                }

                bool playerAdded = await _redisService.AddPlayerToLobbyAsync(targetLobbyId, playerId, _lobbySettings.MaxPlayersPerLobby);

                if (playerAdded)
                {
                    await _redisService.SetPlayerLobbyAsync(playerId, targetLobbyId);
                    _logger.LogInformation("Player {PlayerId} successfully added to lobby {LobbyId}.", playerId, targetLobbyId);
                    return new LobbyJoinResponse
                    {
                        Success = true,
                        Message = $"You have joined the lobby with ID {targetLobbyId}",
                        LobbyId = targetLobbyId
                    };
                }
                else
                {
                    _logger.LogWarning("Player {PlayerId} failed to be added to lobby {LobbyId}. It might have just filled up.", playerId, targetLobbyId);
                    return new LobbyJoinResponse { Success = false, Message = "Failed to join lobby. It might have just filled up. Please try again." };
                }
            }
        }
        
        public async Task<IEnumerable<LobbyInfo>> GetActiveLobbiesAsync()
        {
            var activeLobbyIds = await _redisService.GetActiveLobbyIdsAsync();
            if (!activeLobbyIds.Any())
            {
                return Enumerable.Empty<LobbyInfo>();
            }
            var lobbies = await _redisService.GetLobbyDetailsAsync(activeLobbyIds);
            return lobbies;
        }
        
    }
