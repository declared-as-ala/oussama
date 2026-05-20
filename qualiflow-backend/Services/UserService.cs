using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCryptNet = BCrypt.Net.BCrypt;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Users;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly INotificationEventPublisher _notificationEventPublisher;
        private readonly IEmailService _emailService;
        private readonly IActionLogger _actionLogger;

        public UserService(
            IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            INotificationEventPublisher notificationEventPublisher,
            IEmailService emailService,
            IActionLogger actionLogger)
        {
            _userRepository = userRepository;
            _organizationRepository = organizationRepository;
            _notificationEventPublisher = notificationEventPublisher;
            _emailService = emailService;
            _actionLogger = actionLogger;
        }

        public async Task<UserResponse> GetByIdAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException("User not found");

            return await MapToResponseAsync(user);
        }

        public async Task<UserListResponse> GetAllAsync(int organizationId, int page = 1, int pageSize = 10)
        {
            var users = await _userRepository.GetByOrganizationIdAsync(organizationId, page, pageSize);
            var total = await _userRepository.GetCountByOrganizationAsync(organizationId);

            return new UserListResponse
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = (await Task.WhenAll(users.Select(MapToResponseAsync))).ToList()
            };
        }

        public async Task<UserListResponse> SearchAsync(string? searchTerm, int? organizationId, int page = 1, int pageSize = 10)
        {
            var users = await _userRepository.SearchAsync(searchTerm, organizationId, page, pageSize);
            var total = await _userRepository.GetSearchCountAsync(searchTerm, organizationId);

            return new UserListResponse
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = (await Task.WhenAll(users.Select(MapToResponseAsync))).ToList()
            };
        }

        public async Task<int> CreateAsync(CreateUserRequest request, int? requestingUserId = null)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            if (await _userRepository.ExistsAsync(normalizedEmail))
                throw new ServiceException("Email already exists");

            // Validate role
            if (!UserRoles.AllRoles.Contains(request.Role))
                throw new ServiceException("Invalid role");

            // Check organization exists if provided
            if (request.OrganizationId.HasValue)
            {
                var org = await _organizationRepository.GetByIdAsync(request.OrganizationId.Value);
                if (org == null)
                    throw new NotFoundException("Organization not found");
            }

            var passwordHash = BCryptNet.HashPassword(request.Password);

            var user = new User
            {
                OrganizationId = request.OrganizationId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = normalizedEmail,
                Username = normalizedEmail, // Use email as username
                PasswordHash = passwordHash,
                Role = request.Role,
                Function = request.Function,
                IsActive = true,
                IsEmailVerified = true,
                EmailVerificationToken = null,
                EmailVerificationExpiresAt = null,
                CreatedAt = DateTime.UtcNow
            };

            var id = await _userRepository.CreateAsync(user);

            try
            {
                var emailBody = EmailTemplateHelper.GetAdminCreatedAccountEmail(
                    user.FirstName,
                    user.Email,
                    request.Password);

                await _emailService.SendEmailAsync(
                    user.Email,
                    "Votre compte QualiFlow est prêt",
                    emailBody);
            }
            catch (Exception)
            {
            }

            if (request.OrganizationId.HasValue)
            {
                await _notificationEventPublisher.PublishToRolesAsync(
                    request.OrganizationId.Value,
                    new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE },
                    NotificationConstants.TypeUserCreated,
                    NotificationConstants.CategorySuccess,
                    "Nouvel utilisateur cree",
                    $"{request.FirstName} {request.LastName} ({normalizedEmail}) a ete cree.",
                    NotificationConstants.PriorityMedium,
                    "USER",
                    id.ToString(),
                    $"/users/{id}",
                    requestingUserId);

                // Audit Log
                var actor = requestingUserId.HasValue ? await _userRepository.GetByIdAsync(requestingUserId.Value) : null;
                var actorName = actor != null ? $"{actor.FirstName} {actor.LastName}" : "Système";

                await _actionLogger.LogActionAsync(
                    request.OrganizationId.Value,
                    requestingUserId ?? 0,
                    actorName,
                    "USER_MANAGEMENT",
                    "CREATE",
                    $"Création utilisateur : {user.Email}",
                    $"L'utilisateur '{user.FirstName} {user.LastName}' a été créé avec le rôle {user.Role}.");
            }

            return id;
        }

        public async Task<bool> UpdateAsync(int id, UpdateUserRequest request)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException("User not found");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (existingUser != null && existingUser.Id != id)
                throw new ServiceException("Email already exists");

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.Email = normalizedEmail;
            user.Username = normalizedEmail;
            user.Function = request.Function;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userRepository.UpdateAsync(user);

            if (result && user.OrganizationId.HasValue)
            {
                await _actionLogger.LogActionAsync(
                    user.OrganizationId.Value,
                    0, 
                    "Administrateur", 
                    "USER_MANAGEMENT",
                    "UPDATE",
                    $"Mise à jour utilisateur : {user.Email}",
                    $"Les informations de l'utilisateur '{user.FirstName} {user.LastName}' ont été modifiées.");
            }

            return result;
        }

        public async Task<bool> ToggleStatusAsync(int id, bool isActive)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException("User not found");

            var result = await _userRepository.ToggleStatusAsync(id, isActive);
            if (result)
            {
                if (user.OrganizationId.HasValue)
                {
                    if (!isActive)
                    {
                        await _notificationEventPublisher.PublishToRolesAsync(
                            user.OrganizationId.Value,
                            new[] { UserRoles.ADMIN_ORG, UserRoles.RESPONSABLE_QUALITE },
                            NotificationConstants.TypeUserDisabled,
                            NotificationConstants.CategoryWarning,
                            "Utilisateur desactive",
                            $"{user.FirstName} {user.LastName} ({user.Email}) a ete desactive.",
                            NotificationConstants.PriorityHigh,
                            "USER",
                            user.Id.ToString(),
                            $"/users/{user.Id}",
                            null);
                    }

                    await _actionLogger.LogActionAsync(
                        user.OrganizationId.Value,
                        0,
                        "Administrateur",
                        "USER_MANAGEMENT",
                        isActive ? "ACTIVATE" : "DEACTIVATE",
                        $"{(isActive ? "Activation" : "Désactivation")} utilisateur : {user.Email}",
                        $"L'utilisateur '{user.FirstName} {user.LastName}' a été {(isActive ? "activé" : "désactivé")}.");
                }
            }

            return result;
        }

        public async Task<bool> ChangeRoleAsync(int id, ChangeUserRoleRequest request, int? requestingUserId = null)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException("User not found");

            if (!UserRoles.AllRoles.Contains(request.Role))
                throw new ServiceException("Invalid role");

            var oldRole = user.Role;
            user.Role = request.Role;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userRepository.UpdateAsync(user);

            if (result && user.OrganizationId.HasValue)
            {
                var actor = requestingUserId.HasValue ? await _userRepository.GetByIdAsync(requestingUserId.Value) : null;
                var actorName = actor != null ? $"{actor.FirstName} {actor.LastName}" : "Administrateur";

                await _actionLogger.LogActionAsync(
                    user.OrganizationId.Value,
                    requestingUserId ?? 0,
                    actorName,
                    "USER_MANAGEMENT",
                    "ROLE_CHANGE",
                    $"Changement rôle : {user.Email}",
                    $"Le rôle de '{user.FirstName} {user.LastName}' a été changé de {oldRole} à {request.Role}.");
            }

            return result;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException("User not found");

            var result = await _userRepository.DeleteAsync(id);

            if (result && user.OrganizationId.HasValue)
            {
                await _actionLogger.LogActionAsync(
                    user.OrganizationId.Value,
                    0,
                    "Administrateur",
                    "USER_MANAGEMENT",
                    "DELETE",
                    $"Suppression utilisateur : {user.Email}",
                    $"L'utilisateur '{user.FirstName} {user.LastName}' a été supprimé.");
            }

            return result;
        }

        public async Task<bool> HardDeleteAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException("User not found");

            var result = await _userRepository.HardDeleteAsync(id);

            return result;
        }

        private async Task<UserResponse> MapToResponseAsync(User user)
        {
            string? orgName = null;
            if (user.OrganizationId.HasValue)
            {
                var org = await _organizationRepository.GetByIdAsync(user.OrganizationId.Value);
                orgName = org?.Name;
            }

            return new UserResponse
            {
                Id = user.Id,
                OrganizationId = user.OrganizationId,
                OrganizationName = orgName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                Function = user.Function,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
    }
}
