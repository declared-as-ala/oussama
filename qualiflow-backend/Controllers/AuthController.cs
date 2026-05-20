using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Auth;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        private static object ErrorMessage(string message) => new { message };

        private bool TryGetCurrentUserId(out int userId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out userId);
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var response = await _authService.RegisterAsync(request);
                return Ok(response);
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var response = await _authService.LoginAsync(request, ipAddress);
                return Ok(response);
            }
            catch (UnauthorizedException ex)
            {
                if (ex.Message.Contains("vérifier votre email", System.StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("verifier votre email", System.StringComparison.OrdinalIgnoreCase))
                {
                    return Unauthorized(new
                    {
                        message = ex.Message,
                        requiresEmailVerification = true,
                        email = request.Email
                    });
                }

                return Unauthorized(ErrorMessage(ex.Message));
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("login-by-phone")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> LoginByPhone([FromBody] LoginByPhoneRequest request)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var response = await _authService.LoginByPhoneAsync(request, ipAddress);
                return Ok(response);
            }
            catch (UnauthorizedException ex)
            {
                if (ex.Message.Contains("vérifier votre email", System.StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("verifier votre email", System.StringComparison.OrdinalIgnoreCase))
                {
                    return Unauthorized(new
                    {
                        message = ex.Message,
                        requiresEmailVerification = true,
                        phone = request.PhoneNumber
                    });
                }

                return Unauthorized(ErrorMessage(ex.Message));
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var response = await _authService.RefreshTokenAsync(request);
                return Ok(response);
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ErrorMessage(ex.Message));
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult<bool>> Logout()
        {
            try
            {
                if (!TryGetCurrentUserId(out var userId))
                {
                    return Unauthorized();
                }

                var result = await _authService.LogoutAsync(userId);
                return Ok(new { message = "Logged out successfully", success = result });
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<MeResponse>> GetProfile()
        {
            try
            {
                if (!TryGetCurrentUserId(out var userId))
                {
                    return Unauthorized();
                }

                var profile = await _authService.GetProfileAsync(userId);
                return Ok(profile);
            }
            catch (NotFoundException ex)
            {
                return NotFound(ErrorMessage(ex.Message));
            }
        }

        [HttpPut("me")]
        [Authorize]
        public async Task<ActionResult<MeResponse>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                if (!TryGetCurrentUserId(out var userId))
                {
                    return Unauthorized();
                }

                var profile = await _authService.UpdateProfileAsync(userId, request);
                return Ok(profile);
            }
            catch (NotFoundException ex)
            {
                return NotFound(ErrorMessage(ex.Message));
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("me/photo")]
        [Authorize]
        public async Task<ActionResult<ProfilePhotoResponse>> UploadProfilePhoto([FromForm] IFormFile? file)
        {
            try
            {
                if (!TryGetCurrentUserId(out var userId))
                {
                    return Unauthorized();
                }

                if (file == null)
                {
                    return BadRequest(ErrorMessage("La photo de profil est obligatoire."));
                }

                var response = await _authService.UploadProfilePhotoAsync(userId, file);
                return Ok(response);
            }
            catch (NotFoundException ex)
            {
                return NotFound(ErrorMessage(ex.Message));
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpGet("me/photo")]
        [Authorize]
        public async Task<IActionResult> DownloadProfilePhoto()
        {
            try
            {
                if (!TryGetCurrentUserId(out var userId))
                {
                    return Unauthorized();
                }

                var result = await _authService.GetProfilePhotoAsync(userId);
                return File(result.Stream, result.ContentType, result.FileName);
            }
            catch (NotFoundException)
            {
                return NoContent();
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult<bool>> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                if (!TryGetCurrentUserId(out var userId))
                {
                    return Unauthorized();
                }

                var result = await _authService.ChangePasswordAsync(userId, request);
                return Ok(new { message = "Password changed successfully", success = result });
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ErrorMessage(ex.Message));
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var result = await _authService.ForgotPasswordAsync(request);
                return Ok(new { message = "Si le compte existe, un code de reinitialisation a ete envoye.", success = result });
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }


        [HttpPost("verify-email-code")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> VerifyEmailCode([FromBody] VerifyEmailCodeRequest request)
        {
            try
            {
                var result = await _authService.VerifyEmailByCodeAsync(request);
                if (result)
                {
                    return Ok(new { message = "Email verified successfully", success = true });
                }

                return BadRequest(ErrorMessage("Code de verification invalide ou expire."));
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("resend-verification-code")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> ResendVerificationCode([FromBody] ResendVerificationCodeRequest request)
        {
            try
            {
                var result = await _authService.ResendVerificationCodeAsync(request);
                return Ok(new
                {
                    message = "Si ce compte existe, un nouveau code de verification a ete envoye.",
                    success = result
                });
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("me/email-change/request-code")]
        [Authorize]
        public async Task<ActionResult<object>> RequestEmailChangeCode([FromBody] RequestEmailChangeCodeRequest request)
        {
            try
            {
                if (!TryGetCurrentUserId(out var userId))
                {
                    return Unauthorized();
                }

                await _authService.RequestEmailChangeCodeAsync(userId, request);
                return Ok(new { message = "Code de verification envoye a la nouvelle adresse email.", success = true });
            }
            catch (NotFoundException ex)
            {
                return NotFound(ErrorMessage(ex.Message));
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("me/email-change/confirm")]
        [Authorize]
        public async Task<ActionResult<MeResponse>> ConfirmEmailChange([FromBody] ConfirmEmailChangeRequest request)
        {
            try
            {
                if (!TryGetCurrentUserId(out var userId))
                {
                    return Unauthorized();
                }

                var profile = await _authService.ConfirmEmailChangeAsync(userId, request);
                return Ok(profile);
            }
            catch (NotFoundException ex)
            {
                return NotFound(ErrorMessage(ex.Message));
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ErrorMessage(ex.Message));
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpGet("verify-email")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> VerifyEmail([FromQuery] string token)
        {
            try
            {
                var result = await _authService.VerifyEmailAsync(token);
                if (result)
                {
                    return Ok(new { message = "Email verified successfully", success = true });
                }
                return BadRequest(ErrorMessage("Invalid or expired token"));
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("verify-reset-code")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> VerifyResetCode([FromBody] VerifyResetCodeRequest request)
        {
            try
            {
                var result = await _authService.VerifyResetCodeAsync(request);
                return Ok(new { success = result, message = result ? "Code valide" : "Code invalide ou expire" });
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(request);
                return Ok(new { success = result, message = "Mot de passe réinitialisé avec succès" });
            }
            catch (ServiceException ex)
            {
                return BadRequest(ErrorMessage(ex.Message));
            }
        }
    }
}
