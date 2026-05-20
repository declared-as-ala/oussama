using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using DocApi.DTOs.Auth;

namespace DocApi.Services.Interfaces
{
    public interface IAuthService
    {
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress);
        Task<LoginResponse> LoginByPhoneAsync(LoginByPhoneRequest request, string ipAddress);
        Task<bool> LogoutAsync(int userId);
        Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request);
        Task<MeResponse> GetProfileAsync(int userId);
        Task<MeResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request);
        Task<ProfilePhotoResponse> UploadProfilePhotoAsync(int userId, IFormFile profilePhoto);
        Task<(Stream Stream, string ContentType, string FileName)> GetProfilePhotoAsync(int userId);
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);
        Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
        Task<bool> VerifyEmailAsync(string token);
        Task<bool> VerifyEmailByCodeAsync(VerifyEmailCodeRequest request);
        Task<bool> ResendVerificationCodeAsync(ResendVerificationCodeRequest request);
        Task<bool> VerifyResetCodeAsync(VerifyResetCodeRequest request);
        Task<bool> RequestEmailChangeCodeAsync(int userId, RequestEmailChangeCodeRequest request);
        Task<MeResponse> ConfirmEmailChangeAsync(int userId, ConfirmEmailChangeRequest request);
    }
}
