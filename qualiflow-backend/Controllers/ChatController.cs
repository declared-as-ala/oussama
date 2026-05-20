using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Chat;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN_ORG,RESPONSABLE_QUALITE,CHEF_SERVICE,UTILISATEUR")]
    public class ChatController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;

        public ChatController(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        [HttpPost("ask")]
        public async Task<ActionResult<AskChatResponseDto>> Ask([FromBody] AskChatRequestDto request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _chatbotService.AskAsync(request, GetUserContext(), cancellationToken);
                return Ok(result);
            }
            catch (UnauthorizedException)
            {
                return Unauthorized();
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("conversations")]
        public async Task<ActionResult<IReadOnlyList<ChatConversationDto>>> GetConversations(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _chatbotService.GetConversationsAsync(GetUserContext(), cancellationToken);
                return Ok(result);
            }
            catch (UnauthorizedException)
            {
                return Unauthorized();
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("conversations/{id:int}")]
        public async Task<ActionResult<ChatConversationDetailsDto>> GetConversationById(int id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _chatbotService.GetConversationByIdAsync(id, GetUserContext(), cancellationToken);
                return Ok(result);
            }
            catch (UnauthorizedException)
            {
                return Unauthorized();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("conversations")]
        public async Task<ActionResult<ChatConversationDto>> CreateConversation([FromBody] CreateConversationDto request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _chatbotService.CreateConversationAsync(request, GetUserContext(), cancellationToken);
                return Ok(result);
            }
            catch (UnauthorizedException)
            {
                return Unauthorized();
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("conversations/{id:int}")]
        public async Task<IActionResult> DeleteConversation(int id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _chatbotService.DeleteConversationAsync(id, GetUserContext(), cancellationToken);
                if (!deleted)
                {
                    return NotFound(new { message = "Conversation introuvable." });
                }

                return NoContent();
            }
            catch (UnauthorizedException)
            {
                return Unauthorized();
            }
            catch (ServiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private UserContext GetUserContext()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("Utilisateur non authentifie.");
            }

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrWhiteSpace(role))
            {
                throw new UnauthorizedException("Role utilisateur introuvable.");
            }

            int? organizationId = null;
            var organizationClaim = User.FindFirst("OrganizationId")?.Value;
            if (int.TryParse(organizationClaim, out var parsedOrganizationId))
            {
                organizationId = parsedOrganizationId;
            }

            return new UserContext
            {
                UserId = userId,
                Role = role,
                OrganizationId = organizationId
            };
        }
    }
}
