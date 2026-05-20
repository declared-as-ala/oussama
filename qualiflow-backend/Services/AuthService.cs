using System;
using System.Collections.Generic;
using System.IO;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BCryptNet = BCrypt.Net.BCrypt;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Auth;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DocApi.Services
{
    public class AuthService : IAuthService
    {
        private const int EmailVerificationCodeExpiryMinutes = 15;
        private const int PasswordResetCodeExpiryMinutes = 15;

        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
        private readonly INotificationEventPublisher _notificationEventPublisher;
        private readonly IProfilePhotoStorageService _profilePhotoStorageService;
        private readonly IEmailService _emailService;
        private readonly JwtSettings _jwtSettings;

        public AuthService(
            IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IPasswordResetTokenRepository passwordResetTokenRepository,
            INotificationEventPublisher notificationEventPublisher,
            IProfilePhotoStorageService profilePhotoStorageService,
            IEmailService emailService,
            IOptions<JwtSettings> jwtSettings)
        {
            _userRepository = userRepository;
            _organizationRepository = organizationRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _passwordResetTokenRepository = passwordResetTokenRepository;
            _notificationEventPublisher = notificationEventPublisher;
            _profilePhotoStorageService = profilePhotoStorageService;
            _emailService = emailService;
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            if (request.Password != request.ConfirmPassword)
                throw new ServiceException("Les mots de passe ne correspondent pas");

            if (request.CaptchaAnswer != (request.CaptchaNum1 + request.CaptchaNum2))
                throw new ServiceException("Réponse de vérification anti-robot incorrecte");

            var passwordValidation = PasswordValidator.Validate(request.Password);
            if (!passwordValidation.IsValid)
                throw new ServiceException(passwordValidation.ErrorMessage!);

            if (request.BirthDate.Date > DateTime.UtcNow.Date)
                throw new ServiceException("Birth date is invalid");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var normalizedOrganizationCode = request.OrganizationCode.Trim().ToUpperInvariant();

            var organization = await _organizationRepository.GetByCodeAsync(normalizedOrganizationCode);
            if (organization == null)
                throw new ServiceException("Organization code is invalid");

            if (!string.Equals(organization.Status, "ACTIF", StringComparison.OrdinalIgnoreCase))
                throw new ServiceException("Organization is suspended");

            if (await _userRepository.ExistsAsync(normalizedEmail))
                throw new ServiceException("Email already exists");

            var user = new User
            {
                OrganizationId = organization.Id,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = normalizedEmail,
                Username = normalizedEmail,
                PasswordHash = BCryptNet.HashPassword(request.Password),
                Role = UserRoles.UTILISATEUR,
                BirthDate = request.BirthDate.Date,
                PreferredLanguage = "fr",
                IsActive = true,
                IsEmailVerified = false,
                EmailVerificationToken = GenerateSixDigitCode(),
                EmailVerificationExpiresAt = DateTime.UtcNow.AddMinutes(EmailVerificationCodeExpiryMinutes),
                CreatedAt = DateTime.UtcNow
            };

            var userId = await _userRepository.CreateAsync(user);
            var requiresEmailVerification = true;
            var verificationEmailSent = true;

            try
            {
                var verificationPage = $"http://localhost:4200/verify-email?email={Uri.EscapeDataString(user.Email)}";
                var body = EmailTemplateHelper.GetVerificationCodeEmail(user.FirstName, user.EmailVerificationToken, EmailVerificationCodeExpiryMinutes, verificationPage);
                
                await _emailService.SendEmailAsync(
                    user.Email,
                    "Bienvenue sur QualiFlow - Code de vérification",
                    body);
            }
            catch (Exception)
            {
                verificationEmailSent = false;
            }

            return new RegisterResponse
            {
                Id = userId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                RequiresEmailVerification = requiresEmailVerification,
                VerificationEmailSent = verificationEmailSent
            };
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var accounts = await _userRepository.GetByEmailAccountsAsync(normalizedEmail);
            if (accounts.Count > 1)
            {
                throw new UnauthorizedException("Cet email est associe a plusieurs organisations. La connexion multi-organisation est desactivee. Veuillez contacter l'administrateur.");
            }

            var user = accounts.Count == 1 ? accounts[0] : null;
            if (user == null || !BCryptNet.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedException("Invalid email or password");
            }

            if (!user.IsActive)
            {
                throw new ForbiddenException("User account is disabled");
            }

            if (!user.IsEmailVerified)
            {
                throw new UnauthorizedException("Veuillez vérifier votre email avant de vous connecter.");
            }

            // Check organization status if user belongs to one
            if (user.OrganizationId.HasValue)
            {
                var organization = await _organizationRepository.GetByIdAsync(user.OrganizationId.Value);
                if (organization == null)
                {
                    throw new UnauthorizedException("Organization not found");
                }

                if (!string.Equals(organization.Status, "ACTIF", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ForbiddenException("Organization is suspended");
                }
            }

            // Update last login
            await _userRepository.UpdateLastLoginAsync(user.Id);

            // Generate tokens
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            // Save refresh token to DB
            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            await _refreshTokenRepository.CreateAsync(refreshTokenEntity);

            // Log successful login

            // Send push/in-app notification on successful login when organization context is available.
            if (user.OrganizationId.HasValue)
            {
                try
                {
                    await _notificationEventPublisher.PublishToUserAsync(
                        user.OrganizationId.Value,
                        user.Id,
                        NotificationConstants.TypeSystemAlert,
                        NotificationConstants.CategorySuccess,
                        "Connexion reussie",
                        "Votre connexion au compte a reussi.",
                        NotificationConstants.PriorityLow,
                        "AUTH_LOGIN_SUCCESS",
                        user.Id.ToString(),
                        "/profile",
                        user.Id);
                }
                catch
                {
                }
            }


            var expirationInMinutes = _jwtSettings.ExpirationInMinutes > 0 ? _jwtSettings.ExpirationInMinutes : 15;
            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expirationInMinutes),
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                OrganizationId = user.OrganizationId
            };
        }

        public async Task<LoginResponse> LoginByPhoneAsync(LoginByPhoneRequest request, string ipAddress)
        {
            var accounts = await _userRepository.GetByPhoneAccountsAsync(request.PhoneNumber);
            if (accounts.Count > 1)
            {
                throw new UnauthorizedException("Ce numéro de téléphone est associé à plusieurs organisations. La connexion multi-organisation est désactivée. Veuillez contacter l'administrateur.");
            }

            var user = accounts.Count == 1 ? accounts[0] : null;
            if (user == null || !BCryptNet.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedException("Invalid phone number or password");
            }

            if (!user.IsActive)
            {
                throw new ForbiddenException("User account is disabled");
            }

            if (!user.IsEmailVerified)
            {
                throw new UnauthorizedException("Veuillez vérifier votre email avant de vous connecter.");
            }

            // Check organization status if user belongs to one
            if (user.OrganizationId.HasValue)
            {
                var organization = await _organizationRepository.GetByIdAsync(user.OrganizationId.Value);
                if (organization == null)
                {
                    throw new UnauthorizedException("Organization not found");
                }

                if (!string.Equals(organization.Status, "ACTIF", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ForbiddenException("Organization is suspended");
                }
            }

            // Update last login
            await _userRepository.UpdateLastLoginAsync(user.Id);

            // Generate tokens
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            // Save refresh token to DB
            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            await _refreshTokenRepository.CreateAsync(refreshTokenEntity);

            // Send push/in-app notification on successful login when organization context is available.
            if (user.OrganizationId.HasValue)
            {
                try
                {
                    await _notificationEventPublisher.PublishToUserAsync(
                        user.OrganizationId.Value,
                        user.Id,
                        NotificationConstants.TypeSystemAlert,
                        NotificationConstants.CategorySuccess,
                        "Connexion reussie",
                        "Votre connexion au compte a reussi.",
                        NotificationConstants.PriorityLow,
                        "AUTH_LOGIN_SUCCESS",
                        user.Id.ToString(),
                        "/profile",
                        user.Id);
                }
                catch
                {
                }
            }

            var expirationInMinutes = _jwtSettings.ExpirationInMinutes > 0 ? _jwtSettings.ExpirationInMinutes : 15;
            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expirationInMinutes),
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                OrganizationId = user.OrganizationId
            };
        }

        public async Task<bool> LogoutAsync(int userId)
        {
            // Revoke all refresh tokens for user
            await _refreshTokenRepository.RevokeByUserIdAsync(userId);
            return true;
        }

        public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request)
        {
            var refreshToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

            if (refreshToken == null || refreshToken.IsRevoked || refreshToken.ExpiresAt < DateTime.UtcNow)
            {
                throw new UnauthorizedException("Invalid or expired refresh token");
            }

            var user = await _userRepository.GetByIdAsync(refreshToken.UserId);
            if (user == null || !user.IsActive)
            {
                throw new UnauthorizedException("User not found or disabled");
            }

            // Generate new access token
            var accessToken = GenerateAccessToken(user);

            // Generate new refresh token
            var newRefreshToken = GenerateRefreshToken();

            // Mark old refresh token as replaced
            refreshToken.ReplacedByToken = newRefreshToken;
            await _refreshTokenRepository.RevokeAsync(refreshToken.Id);

            // Save new refresh token
            var newRefreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            await _refreshTokenRepository.CreateAsync(newRefreshTokenEntity);

            // Log refresh token action

            var expirationInMinutes = _jwtSettings.ExpirationInMinutes > 0 ? _jwtSettings.ExpirationInMinutes : 15;
            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expirationInMinutes),
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                OrganizationId = user.OrganizationId
            };
        }

        public async Task<MeResponse> GetProfileAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
                throw new NotFoundException("User not found");

            string? organizationName = null;
            if (user.OrganizationId.HasValue)
            {
                var organization = await _organizationRepository.GetByIdAsync(user.OrganizationId.Value);
                organizationName = organization?.Name;
            }

            return new MeResponse
            {
                Id = user.Id,
                OrganizationId = user.OrganizationId,
                OrganizationName = organizationName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                Function = user.Function,
                Phone = user.Phone,
                City = user.City,
                BirthDate = user.BirthDate,
                PreferredLanguage = NormalizeLanguage(user.PreferredLanguage),
                ProfilePhotoPath = user.ProfilePhotoPath,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<MeResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Trim().Length < 2)
            {
                throw new ServiceException("First name is required");
            }

            if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Trim().Length < 2)
            {
                throw new ServiceException("Last name is required");
            }

            if (request.BirthDate.HasValue && request.BirthDate.Value.Date > DateTime.UtcNow.Date)
            {
                throw new ServiceException("Birth date is invalid");
            }

            var normalizedPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            if (normalizedPhone != null && normalizedPhone.Length > 30)
            {
                throw new ServiceException("Phone is too long");
            }

            var normalizedCity = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
            if (normalizedCity != null && normalizedCity.Length > 120)
            {
                throw new ServiceException("City is too long");
            }

            var preferredLanguage = NormalizeLanguage(request.PreferredLanguage);
            var updated = await _userRepository.UpdateProfileAsync(
                userId,
                request.FirstName.Trim(),
                request.LastName.Trim(),
                request.BirthDate?.Date,
                normalizedPhone,
                normalizedCity,
                preferredLanguage,
                DateTime.UtcNow);

            if (!updated)
            {
                throw new ServiceException("Unable to update profile");
            }
            return await GetProfileAsync(userId);
        }

        public async Task<ProfilePhotoResponse> UploadProfilePhotoAsync(int userId, IFormFile profilePhoto)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            var profilePhotoPath = await _profilePhotoStorageService.SaveAsync(profilePhoto, userId);
            var updatedAt = DateTime.UtcNow;
            var updated = await _userRepository.UpdateProfilePhotoPathAsync(userId, profilePhotoPath, updatedAt);
            if (!updated)
            {
                throw new ServiceException("Unable to update profile photo");
            }
            return new ProfilePhotoResponse
            {
                UserId = userId,
                ProfilePhotoPath = profilePhotoPath,
                UpdatedAt = updatedAt
            };
        }

        public async Task<(Stream Stream, string ContentType, string FileName)> GetProfilePhotoAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (string.IsNullOrWhiteSpace(user.ProfilePhotoPath))
            {
                throw new NotFoundException("Profile photo not found");
            }

            return await _profilePhotoStorageService.OpenReadAsync(user.ProfilePhotoPath);
        }

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
        {
            if (request.NewPassword != request.ConfirmPassword)
                throw new ServiceException("Les mots de passe ne correspondent pas");

            var passwordValidation = PasswordValidator.Validate(request.NewPassword);
            if (!passwordValidation.IsValid)
                throw new ServiceException(passwordValidation.ErrorMessage!);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            if (!BCryptNet.Verify(request.CurrentPassword, user.PasswordHash))
                throw new UnauthorizedException("Current password is incorrect");

            var newPasswordHash = BCryptNet.HashPassword(request.NewPassword);
            var result = await _userRepository.UpdatePasswordAsync(userId, newPasswordHash);

            if (result)
            {
                // Revoke all refresh tokens when password changes
                await _refreshTokenRepository.RevokeByUserIdAsync(userId);
            }

            return result;
        }

        public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (user == null)
            {
                // Don't reveal if user exists
                return true;
            }

            var code = GeneratePasswordResetCode();
            await _passwordResetTokenRepository.RevokeActiveByUserIdAsync(user.Id);

            var passwordResetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(PasswordResetCodeExpiryMinutes),
                Used = false,
                CreatedAt = DateTime.UtcNow
            };

            await _passwordResetTokenRepository.CreateAsync(passwordResetToken);

            try
            {
                var body = EmailTemplateHelper.GetPasswordResetEmail(user.FirstName, code, PasswordResetCodeExpiryMinutes);
                await _emailService.SendEmailAsync(
                    user.Email,
                    "Réinitialisation de votre mot de passe QualiFlow",
                    body);
            }
            catch (Exception)
            {
                throw new ServiceException("Impossible d'envoyer le code de reinitialisation pour le moment. Veuillez reessayer.");
            }

            return true;
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
        {
            if (request.NewPassword != request.ConfirmPassword)
                throw new ServiceException("Les mots de passe ne correspondent pas");

            var passwordValidation = PasswordValidator.Validate(request.NewPassword);
            if (!passwordValidation.IsValid)
                throw new ServiceException(passwordValidation.ErrorMessage!);

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var normalizedCode = request.Code.Trim();

            var user = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (user == null)
                throw new UnauthorizedException("Code de reinitialisation invalide ou expire.");

            var resetToken = await _passwordResetTokenRepository.GetByUserAndTokenAsync(user.Id, normalizedCode);
            if (resetToken == null || resetToken.Used || resetToken.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedException("Code de reinitialisation invalide ou expire.");

            var passwordHash = BCryptNet.HashPassword(request.NewPassword);
            var result = await _userRepository.UpdatePasswordAsync(user.Id, passwordHash);

            if (result)
            {
                await _passwordResetTokenRepository.MarkAsUsedAsync(resetToken.Id);
                // Revoke all refresh tokens
                await _refreshTokenRepository.RevokeByUserIdAsync(user.Id);
            }

            return result;
        }

        public async Task<bool> VerifyResetCodeAsync(VerifyResetCodeRequest request)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var normalizedCode = request.Code.Trim();

            // Note: Global lookup.
            var user = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (user == null) return false;

            var resetToken = await _passwordResetTokenRepository.GetByUserAndTokenAsync(user.Id, normalizedCode);
            if (resetToken == null || resetToken.Used || resetToken.ExpiresAt < DateTime.UtcNow)
                return false;

            return true;
        }

        private string GenerateAccessToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FirstName", user.FirstName),
                new Claim("LastName", user.LastName)
            };

            if (user.OrganizationId.HasValue)
                claims.Add(new Claim("OrganizationId", user.OrganizationId.Value.ToString()));

            var expirationInMinutes = _jwtSettings.ExpirationInMinutes > 0 ? _jwtSettings.ExpirationInMinutes : 15;
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expirationInMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private static string GeneratePasswordResetCode() => GenerateSixDigitCode();

        public async Task<bool> VerifyEmailAsync(string token)
        {
            var user = await _userRepository.GetByVerificationTokenAsync(token);
            if (user == null || user.EmailVerificationExpiresAt < DateTime.UtcNow)
            {
                return false;
            }

            return await _userRepository.VerifyEmailAsync(user.Id);
        }

        public async Task<bool> VerifyEmailByCodeAsync(VerifyEmailCodeRequest request)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var normalizedCode = request.Code.Trim();

            // Note: Global lookup.
            var user = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (user == null)
            {
                return false;
            }

            if (user.IsEmailVerified)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(user.EmailVerificationToken) ||
                !user.EmailVerificationExpiresAt.HasValue ||
                user.EmailVerificationExpiresAt.Value < DateTime.UtcNow)
            {
                return false;
            }

            if (!string.Equals(user.EmailVerificationToken, normalizedCode, StringComparison.Ordinal))
            {
                return false;
            }

            var verified = await _userRepository.VerifyEmailAsync(user.Id);
            if (verified)
            {
            }

            return verified;
        }

        public async Task<bool> ResendVerificationCodeAsync(ResendVerificationCodeRequest request)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            // Note: Global lookup.
            var user = await _userRepository.GetByEmailAsync(normalizedEmail);

            if (user == null || user.IsEmailVerified)
            {
                // Keep a generic successful response to avoid user enumeration.
                return true;
            }

            var verificationCode = GenerateSixDigitCode();
            var expiresAt = DateTime.UtcNow.AddMinutes(EmailVerificationCodeExpiryMinutes);
            var previousCode = user.EmailVerificationToken;
            var previousExpiry = user.EmailVerificationExpiresAt;

            var updated = await _userRepository.UpdateEmailVerificationTokenAsync(user.Id, verificationCode, expiresAt);
            if (!updated)
            {
                throw new ServiceException("Impossible de generer un nouveau code de verification.");
            }

            try
            {
                var verificationPage = $"http://localhost:4200/verify-email?email={Uri.EscapeDataString(user.Email)}";
                var body = EmailTemplateHelper.GetVerificationCodeEmail(user.FirstName, verificationCode, EmailVerificationCodeExpiryMinutes, verificationPage);

                await _emailService.SendEmailAsync(
                    user.Email,
                    "QualiFlow - Nouveau code de vérification",
                    body);
            }
            catch (Exception)
            {
                // Keep the previous code valid when email delivery fails.
                await _userRepository.UpdateEmailVerificationTokenAsync(user.Id, previousCode, previousExpiry);
                throw new ServiceException("Impossible d'envoyer le code pour le moment. Veuillez reessayer.");
            }
            return true;
        }

        public async Task<bool> RequestEmailChangeCodeAsync(int userId, RequestEmailChangeCodeRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            var normalizedNewEmail = request.NewEmail.Trim().ToLowerInvariant();
            if (!normalizedNewEmail.Contains('@'))
            {
                throw new ServiceException("Email invalide.");
            }

            if (string.Equals(user.Email, normalizedNewEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new ServiceException("Le nouvel email doit etre different de l'email actuel.");
            }

            var existingUser = await _userRepository.GetByEmailAsync(normalizedNewEmail);
            if (existingUser != null && existingUser.Id != userId)
            {
                throw new ServiceException("Cet email est deja utilise.");
            }

            var verificationCode = GenerateSixDigitCode();
            var expiresAt = DateTime.UtcNow.AddMinutes(EmailVerificationCodeExpiryMinutes);

            var updated = await _userRepository.UpdatePendingEmailChangeAsync(userId, normalizedNewEmail, verificationCode, expiresAt);
            if (!updated)
            {
                throw new ServiceException("Impossible de preparer le changement d'email.");
            }

            try
            {
                var body = EmailTemplateHelper.GetVerificationCodeEmail(user.FirstName, verificationCode, EmailVerificationCodeExpiryMinutes, "#");
                await _emailService.SendEmailAsync(
                    normalizedNewEmail,
                    "QualiFlow - Verification de changement d'email",
                    body);
            }
            catch (Exception)
            {
                await _userRepository.UpdatePendingEmailChangeAsync(userId, null, null, null);
                throw new ServiceException("Impossible d'envoyer le code de verification sur le nouvel email.");
            }
            return true;
        }

        public async Task<MeResponse> ConfirmEmailChangeAsync(int userId, ConfirmEmailChangeRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            var normalizedNewEmail = request.NewEmail.Trim().ToLowerInvariant();
            var normalizedCode = request.Code.Trim();

            if (string.IsNullOrWhiteSpace(user.PendingEmail) ||
                !string.Equals(user.PendingEmail, normalizedNewEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedException("Demande de changement d'email invalide.");
            }

            if (string.IsNullOrWhiteSpace(user.EmailChangeVerificationToken) ||
                !user.EmailChangeVerificationExpiresAt.HasValue ||
                user.EmailChangeVerificationExpiresAt.Value < DateTime.UtcNow)
            {
                throw new UnauthorizedException("Code de verification invalide ou expire.");
            }

            if (!string.Equals(user.EmailChangeVerificationToken, normalizedCode, StringComparison.Ordinal))
            {
                throw new UnauthorizedException("Code de verification invalide ou expire.");
            }

            var existingUser = await _userRepository.GetByEmailAsync(normalizedNewEmail);
            if (existingUser != null && existingUser.Id != userId)
            {
                throw new ServiceException("Cet email est deja utilise.");
            }

            var changed = await _userRepository.ConfirmEmailChangeAsync(userId, normalizedNewEmail);
            if (!changed)
            {
                throw new ServiceException("Impossible de confirmer le changement d'email.");
            }

            await _refreshTokenRepository.RevokeByUserIdAsync(userId);
            return await GetProfileAsync(userId);
        }

        private static string GenerateSixDigitCode()
        {
            var randomNumber = new byte[4];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);

            var value = BitConverter.ToUInt32(randomNumber, 0) % 900000 + 100000;
            return value.ToString();
        }

        private static string NormalizeLanguage(string? language)
        {
            var normalized = string.IsNullOrWhiteSpace(language)
                ? "fr"
                : language.Trim().ToLowerInvariant();

            return normalized switch
            {
                "fr" => "fr",
                "en" => "en",
                "ar" => "ar",
                _ => "fr"
            };
        }
    }
}

