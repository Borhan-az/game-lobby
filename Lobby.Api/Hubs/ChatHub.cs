using Lobby.Contract.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Lobby.Api.Hubs;

public class ChatHub : Hub
{
    private readonly IRedisService _redisService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IRedisService redisService, ILogger<ChatHub> logger)
    {
        _redisService = redisService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}. Exception: {Exception}", Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinLobbyChat(string lobbyId, string playerId)
    {
        if (string.IsNullOrWhiteSpace(lobbyId) || string.IsNullOrWhiteSpace(playerId))
        {
            _logger.LogWarning("JoinLobbyChat: Invalid lobbyId or playerId. LobbyId: {LobbyId}, PlayerId: {PlayerId}", lobbyId, playerId);
            return;
        }

        var playerLobby = await _redisService.GetPlayerLobbyAsync(playerId);
        if (playerLobby != lobbyId)
        {
            _logger.LogWarning("Player {PlayerId} attempted to join chat for lobby {LobbyId} but is actually in lobby {ActualLobbyId}.", playerId, lobbyId, playerLobby ?? "None");
            await Clients.Caller.SendAsync("ReceiveMessage", "System", $"You are not authorized to join chat for lobby {lobbyId}.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
        _logger.LogInformation("Player {PlayerId} ({ConnectionId}) joined chat group for lobby {LobbyId}.", playerId, Context.ConnectionId, lobbyId);

        await _redisService.PublishAsync($"lobby_chat:{lobbyId}", System.Text.Json.JsonSerializer.Serialize(new { Sender = "System", Content = $"{playerId} has joined the chat." }));
    }

    public async Task LeaveLobbyChat(string lobbyId, string playerId)
    {
        if (string.IsNullOrWhiteSpace(lobbyId) || string.IsNullOrWhiteSpace(playerId))
        {
            _logger.LogWarning("LeaveLobbyChat: Invalid lobbyId or playerId. LobbyId: {LobbyId}, PlayerId: {PlayerId}", lobbyId, playerId);
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyId);
        _logger.LogInformation("Player {PlayerId} ({ConnectionId}) left chat group for lobby {LobbyId}.", playerId, Context.ConnectionId, lobbyId);

        await _redisService.PublishAsync($"lobby_chat:{lobbyId}", System.Text.Json.JsonSerializer.Serialize(new { Sender = "System", Content = $"{playerId} has left the chat." }));
    }
    
    public async Task SendMessageToLobby(string lobbyId, string senderId, string message)
    {
        if (string.IsNullOrWhiteSpace(lobbyId) || string.IsNullOrWhiteSpace(senderId) || string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("SendMessageToLobby: Invalid input. LobbyId: {LobbyId}, SenderId: {SenderId}, Message: {Message}", lobbyId, senderId, message);
            return;
        }

        var playerLobby = await _redisService.GetPlayerLobbyAsync(senderId);
        if (playerLobby != lobbyId)
        {
            _logger.LogWarning("Player {SenderId} attempted to send message to lobby {LobbyId} but is actually in lobby {ActualLobbyId}.", senderId, lobbyId, playerLobby ?? "None");
            await Clients.Caller.SendAsync("ReceiveMessage", "System", $"You are not authorized to send messages to lobby {lobbyId}.");
            return;
        }

        _logger.LogInformation("Player {SenderId} sending message to lobby {LobbyId}: {Message}", senderId, lobbyId, message);

        var chatMessage = new { Sender = senderId, Content = message };
        await _redisService.PublishAsync($"lobby_chat:{lobbyId}", System.Text.Json.JsonSerializer.Serialize(chatMessage));
    }
}
