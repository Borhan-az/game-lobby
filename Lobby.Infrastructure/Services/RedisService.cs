using System.Collections.Concurrent;
using Lobby.Contract.Dtos;
using Lobby.Contract.Interfaces;
using Microsoft.Extensions.Logging;
using RedLockNet;
using RedLockNet.SERedis;
using StackExchange.Redis;

namespace Lobby.Infrastructure.Services;

public class RedisService : IRedisService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedLockFactory _redLockFactory;
    private readonly ILogger<RedisService> _logger;

    private readonly ConcurrentDictionary<string, Action<RedisChannel, RedisValue>> _subscriptions = new();

    private const string LobbyHashPrefix = "lobby:";
    private const string LobbyPlayersSetPrefix = "lobby_players:";
    private const string ActiveLobbiesSetKey = "active_lobbies";
    private const string GlobalLobbyCounterKey = "global_lobby_count";
    private const string PlayerLobbyKeyPrefix = "player_lobby:";



    public RedisService(IConnectionMultiplexer redis, RedLockFactory redLockFactory, ILogger<RedisService> logger)
    {
        _redis = redis;
        _redLockFactory = redLockFactory;
        _logger = logger;
    }

    private IDatabase GetDatabase() => _redis.GetDatabase();
    private ISubscriber GetSubscriber() => _redis.GetSubscriber();

    public async Task<IRedLock?> AcquireDistributedLockAsync(string resource, TimeSpan expiry)
    {
        try
        {
            return await _redLockFactory.CreateLockAsync(resource, expiry, TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire distributed lock for resource: {Resource}", resource);
            return null;
        }
    }

    public async Task<long> GetLobbyPlayerCountAsync(string lobbyId)
    {
        var db = GetDatabase();
        var playersCount = await db.HashGetAsync($"{LobbyHashPrefix}{lobbyId}", "current_players");
        return playersCount.HasValue ? (long)playersCount : 0;
    }

    public async Task<bool> AddPlayerToLobbyAsync(string lobbyId, string playerId, int maxPlayersPerLobby)
    {
        var db = GetDatabase();
        var lobbyHashKey = $"{LobbyHashPrefix}{lobbyId}";
        var lobbyPlayersSetKey = $"{LobbyPlayersSetPrefix}{lobbyId}";

        RedisResult result = await db.ScriptEvaluateAsync(
            AddPlayerToLobbyLuaScript,
            new RedisKey[] { lobbyHashKey, lobbyPlayersSetKey },
            new RedisValue[] { playerId, maxPlayersPerLobby }
        );

        int scriptResult = (int)result;

        if (scriptResult == 1)
        {
            _logger.LogInformation(
                "Player {PlayerId} successfully added to lobby {LobbyId} (Lua script result: {Result}).", playerId,
                lobbyId, scriptResult);
            return true;
        }
        else if (scriptResult == 0)
        {
            _logger.LogInformation("Player {PlayerId} was already in lobby {LobbyId} (Lua script result: {Result}).",
                playerId, lobbyId, scriptResult);
            return true;
        }
        else if (scriptResult == -1)
        {
            _logger.LogWarning(
                "Player {PlayerId} attempted to join lobby {LobbyId}, but it is full (Lua script result: {Result}).",
                playerId, lobbyId, scriptResult);
            return false;
        }
        else
        {
            _logger.LogError(
                "Unexpected result from Lua script for player {PlayerId} joining lobby {LobbyId}: {Result}", playerId,
                lobbyId, scriptResult);
            return false;
        }
    }

    public async Task<string?> FindOrCreateOpenLobbyAsync(int maxPlayersPerLobby, int maxTotalLobbies)
    {
        var db = GetDatabase();
        string newLobbyId = Guid.NewGuid().ToString("N");

        RedisResult result = await db.ScriptEvaluateAsync(
            FindOrCreateOpenLobbyLuaScript,
            new RedisKey[] { ActiveLobbiesSetKey, GlobalLobbyCounterKey },
            new RedisValue[] { maxPlayersPerLobby, maxTotalLobbies, newLobbyId, LobbyHashPrefix }
        );

        if (result.Type == ResultType.BulkString)
        {
            string? lobbyId = result.ToString();
            _logger.LogInformation("FindOrCreateOpenLobbyAsync: Successfully found/created lobby {LobbyId}.", lobbyId);
            return lobbyId;
        }
        else
        {
            int scriptResult = (int)result;
            if (scriptResult == -1)
            {
                _logger.LogWarning(
                    "FindOrCreateOpenLobbyAsync: Global lobby limit ({MaxTotalLobbies}) reached. Cannot create new lobbies.",
                    maxTotalLobbies);
                return null;
            }
            else if (scriptResult == -2)
            {
                _logger.LogWarning(
                    "FindOrCreateOpenLobbyAsync: No open lobby found and new lobby creation failed (e.g., lobby ID collision).");
                return null;
            }
            else
            {
                _logger.LogError("FindOrCreateOpenLobbyAsync: Unexpected result from Lua script: {Result}",
                    scriptResult);
                return null;
            }
        }
    }

    public async Task<string?> GetPlayerLobbyAsync(string playerId)
    {
        var db = GetDatabase();
        var lobbyId = await db.StringGetAsync($"{PlayerLobbyKeyPrefix}{playerId}");
        return lobbyId.ToString();
    }

    public Task SetPlayerLobbyAsync(string playerId, string lobbyId)
    {
        var db = GetDatabase();
        return db.StringSetAsync($"{PlayerLobbyKeyPrefix}{playerId}", lobbyId);
    }

    public async Task SubscribeAsync(string channel, Action<RedisChannel, RedisValue> handler)
    {
        if (_subscriptions.TryAdd(channel, handler))
        {
            _logger.LogInformation("Subscribing to Redis channel: {Channel}", channel);
            await GetSubscriber().SubscribeAsync(channel, (redisChannel, message) =>
            {
                _logger.LogDebug("Received message from Redis channel {RedisChannel}: {Message}", redisChannel,
                    message);
                handler(redisChannel, message);
            });
        }
        else
        {
            _logger.LogDebug("Already subscribed to Redis channel: {Channel}", channel);
        }
    }

    public async Task PublishAsync(string channel, string message)
    {
        _logger.LogInformation("Publishing message to Redis channel {Channel}: {Message}", channel, message);
        await GetSubscriber().PublishAsync(channel, message);
    }

    public async Task UnsubscribeAsync(string channel)
    {
        if (_subscriptions.TryRemove(channel, out _))
        {
            _logger.LogInformation("Unsubscribing from Redis channel: {Channel}", channel);
            await GetSubscriber().UnsubscribeAsync(channel);
        }
        else
        {
            _logger.LogDebug("Not subscribed to Redis channel: {Channel} on this instance.", channel);
        }
    }

    public async Task<IEnumerable<string>> GetActiveLobbyIdsAsync()
    {
        var db = GetDatabase();
        var lobbyIds = await db.SetMembersAsync(ActiveLobbiesSetKey);
        return lobbyIds.Select(id => id.ToString()).ToList();
    }

    public async Task<IEnumerable<LobbyInfo>> GetLobbyDetailsAsync(IEnumerable<string> lobbyIds)
    {
        var db = GetDatabase();
        var lobbies = new List<LobbyInfo>();

        // Use a batch operation to fetch all lobby details efficiently
        var batch = db.CreateBatch();
        var hashGetTasks = new Dictionary<string, Task<HashEntry[]>>();

        foreach (var lobbyId in lobbyIds)
        {
            hashGetTasks[lobbyId] = batch.HashGetAllAsync($"{LobbyHashPrefix}{lobbyId}");
        }

        batch.Execute(); // Execute all commands in the batch

        foreach (var lobbyId in lobbyIds)
        {
            var hashEntries = await hashGetTasks[lobbyId];
            if (hashEntries != null && hashEntries.Any())
            {
                var currentPlayers = hashEntries.FirstOrDefault(e => e.Name == "current_players").Value;
                var maxCapacity = hashEntries.FirstOrDefault(e => e.Name == "max_capacity").Value;

                if (currentPlayers.HasValue && maxCapacity.HasValue)
                {
                    lobbies.Add(new LobbyInfo
                    {
                        LobbyId = lobbyId,
                        CurrentPlayers = (long)currentPlayers,
                        MaxCapacity = (int)maxCapacity
                    });
                }
                else
                {
                    _logger.LogWarning(
                        "Lobby {LobbyId} found in active_lobbies but missing player count or capacity details.",
                        lobbyId);
                }
            }
            else
            {
                _logger.LogWarning("Lobby {LobbyId} found in active_lobbies but no hash data found.", lobbyId);
            }
        }

        return lobbies;
    }

    private const string AddPlayerToLobbyLuaScript = @"
            local current_players = tonumber(redis.call('HGET', KEYS[1], 'current_players'))
            local max_capacity = tonumber(ARGV[2])
            local player_id = ARGV[1]

            if current_players >= max_capacity then
                return -1
            end

            local added = redis.call('SADD', KEYS[2], player_id)
            if added == 1 then
                redis.call('HINCRBY', KEYS[1], 'current_players', 1)
                return 1
            else
                return 0
            end
        ";

    private const string FindOrCreateOpenLobbyLuaScript = @"
            local maxPlayersPerLobby = tonumber(ARGV[1])
            local maxTotalLobbies = tonumber(ARGV[2])
            local newLobbyId = ARGV[3]
            local lobbyHashPrefix = ARGV[4]

            local active_lobbies = redis.call('SMEMBERS', KEYS[1])
            for i, lobby_id in ipairs(active_lobbies) do
                local current_players = tonumber(redis.call('HGET', lobbyHashPrefix .. lobby_id, 'current_players'))
                if current_players and current_players < maxPlayersPerLobby then
                    return lobby_id
                end
            end

            local current_total_lobbies = tonumber(redis.call('GET', KEYS[2])) or 0
            if current_total_lobbies >= maxTotalLobbies then
                return -1
            end

            local lobbyHashKey = lobbyHashPrefix .. newLobbyId

            if redis.call('EXISTS', lobbyHashKey) == 1 then
                return -2
            end

            redis.call('HMSET', lobbyHashKey, 'current_players', 0, 'max_capacity', maxPlayersPerLobby)
            redis.call('SADD', KEYS[1], newLobbyId)
            redis.call('INCR', KEYS[2])

            return newLobbyId
        ";
}