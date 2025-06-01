using Lobby.Contract.Dtos;
using Lobby.Contract.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lobby.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LobbiesController : ControllerBase
    {
        private readonly ILobbyService _lobbyService;
        private readonly ILogger<LobbiesController> _logger;

        public LobbiesController(ILobbyService lobbyService, ILogger<LobbiesController> logger)
        {
            _lobbyService = lobbyService;
            _logger = logger;
        }

        [HttpPost("join")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LobbyJoinResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(LobbyJoinResponse))]
        public async Task<IActionResult> JoinLobby(LobbyJoinRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PlayerId))
            {
                _logger.LogWarning("JoinLobby request received with empty or null PlayerId.");
                return BadRequest(new LobbyJoinResponse { Success = false, Message = "PlayerId cannot be empty." });
            }

            try
            {
                _logger.LogInformation(
                    "Player {PlayerId} attempting to join lobby. PreferredLobbyId: {PreferredLobbyId}",
                    request.PlayerId, request.PreferredLobbyId ?? "None");
                var result = await _lobbyService.JoinLobbyAsync(request.PlayerId, request.PreferredLobbyId);

                if (result.Success)
                {
                    _logger.LogInformation("Player {PlayerId} successfully joined lobby {LobbyId}.", request.PlayerId,
                        result.LobbyId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Player {PlayerId} failed to join lobby. Reason: {Message}", request.PlayerId,
                        result.Message);

                    return Conflict(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while player {PlayerId} attempted to join a lobby.",
                    request.PlayerId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new LobbyJoinResponse { Success = false, Message = "An unexpected error occurred." });
            }
        }
        
        [HttpGet("list")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<LobbyInfo>))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetLobbies()
        {
            try
            {
                _logger.LogInformation("Fetching list of active lobbies.");
                var lobbies = await _lobbyService.GetActiveLobbiesAsync();
                return Ok(lobbies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the list of lobbies.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while fetching lobbies.");
            }
        }
    }
}