using System.Threading;
using System.Threading.Tasks;
using DocApi.DTOs.Public;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/public")]
    [AllowAnonymous]
    public class PublicController : ControllerBase
    {
        private readonly IPublicService _publicService;

        public PublicController(IPublicService publicService)
        {
            _publicService = publicService;
        }

        [HttpPost("send-verification-code")]
        public async Task<ActionResult<SubmitOrganizationRequestResponse>> SendVerificationCode(
            [FromBody] SendVerificationCodeRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.Email))
            {
                return BadRequest(new { message = "L'adresse email est requise." });
            }

            var result = await _publicService.SendVerificationCodeAsync(request.Email, cancellationToken);
            return Ok(result);
        }

        [HttpPost("verify-code")]
        public async Task<ActionResult<SubmitOrganizationRequestResponse>> VerifyCode(
            [FromBody] VerifyCodeRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request?.Code))
            {
                return BadRequest(new { message = "L'adresse email et le code de validation sont requis." });
            }

            var result = await _publicService.VerifyCodeAsync(request.Email, request.Code, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        [HttpPost("organization-request")]
        public async Task<ActionResult<SubmitOrganizationRequestResponse>> SubmitOrganizationRequest(
            [FromBody] SubmitOrganizationRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _publicService.SubmitOrganizationRequestAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
    }
}
