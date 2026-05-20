using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Support;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/support")]
    [Authorize]
    public class SupportController : ControllerBase
    {
        private readonly ISupportService _supportService;

        public SupportController(ISupportService supportService)
        {
            _supportService = supportService;
        }

        [HttpGet("contact-info")]
        public async Task<ActionResult<SupportContactInfoResponse>> GetContactInfo(CancellationToken cancellationToken)
        {
            var result = await _supportService.GetContactInfoAsync(cancellationToken);
            return Ok(result);
        }

        [HttpPost("ticket")]
        public async Task<ActionResult<SubmitSupportTicketResponse>> SubmitTicket(
            [FromBody] SubmitSupportTicketRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _supportService.SubmitTicketAsync(request, GetUserContext(), cancellationToken);
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
