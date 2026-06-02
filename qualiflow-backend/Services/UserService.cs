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

            if (request.BirthDate.HasValue && request.BirthDate.Value.Date > DateTime.UtcNow.Date)
                throw new ServiceException("Birth date cannot be in the future");

            var normalizedPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            if (normalizedPhone != null && normalizedPhone.Length > 30)
                throw new ServiceException("Phone is too long");

            var normalizedCity = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
            if (normalizedCity != null && normalizedCity.Length > 120)
                throw new ServiceException("City is too long");

            var normalizedNationality = string.IsNullOrWhiteSpace(request.Nationality) ? null : request.Nationality.Trim();
            if (normalizedNationality != null && normalizedNationality.Length > 100)
                throw new ServiceException("Nationality is too long");

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
                Phone = normalizedPhone,
                City = normalizedCity,
                Nationality = normalizedNationality,
                BirthDate = request.BirthDate?.Date,
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

        public async Task<bool> UpdateAsync(int id, UpdateUserRequest request, int? requestingUserId = null)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException("User not found");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (existingUser != null && existingUser.Id != id)
                throw new ServiceException("Email already exists");

            if (request.BirthDate.HasValue && request.BirthDate.Value.Date > DateTime.UtcNow.Date)
                throw new ServiceException("Birth date cannot be in the future");

            var normalizedPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            if (normalizedPhone != null && normalizedPhone.Length > 30)
                throw new ServiceException("Phone is too long");

            var normalizedCity = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
            if (normalizedCity != null && normalizedCity.Length > 120)
                throw new ServiceException("City is too long");

            var normalizedNationality = string.IsNullOrWhiteSpace(request.Nationality) ? null : request.Nationality.Trim();
            if (normalizedNationality != null && normalizedNationality.Length > 100)
                throw new ServiceException("Nationality is too long");

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.Email = normalizedEmail;
            user.Username = normalizedEmail;
            user.Function = request.Function;
            user.Phone = normalizedPhone;
            user.City = normalizedCity;
            user.Nationality = normalizedNationality;
            user.BirthDate = request.BirthDate?.Date;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userRepository.UpdateAsync(user);

            if (result && user.OrganizationId.HasValue)
            {
                var actor = requestingUserId.HasValue ? await _userRepository.GetByIdAsync(requestingUserId.Value) : null;
                var actorName = actor != null ? $"{actor.FirstName} {actor.LastName}" : "Administrateur";

                await _actionLogger.LogActionAsync(
                    user.OrganizationId.Value,
                    requestingUserId ?? user.Id,
                    actorName,
                    "USER_MANAGEMENT",
                    "UPDATE",
                    $"Mise à jour utilisateur : {user.Email}",
                    $"Les informations de l'utilisateur '{user.FirstName} {user.LastName}' ont été modifiées.");
            }

            return result;
        }

        public async Task<bool> ToggleStatusAsync(int id, bool isActive, int? requestingUserId = null)
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
                            requestingUserId);
                    }

                    var actor = requestingUserId.HasValue ? await _userRepository.GetByIdAsync(requestingUserId.Value) : null;
                    var actorName = actor != null ? $"{actor.FirstName} {actor.LastName}" : "Administrateur";

                    await _actionLogger.LogActionAsync(
                        user.OrganizationId.Value,
                        requestingUserId ?? user.Id,
                        actorName,
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

        public async Task<bool> DeleteAsync(int id, int? requestingUserId = null)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException("User not found");

            var result = await _userRepository.DeleteAsync(id);

            if (result && user.OrganizationId.HasValue)
            {
                var actor = requestingUserId.HasValue ? await _userRepository.GetByIdAsync(requestingUserId.Value) : null;
                var actorName = actor != null ? $"{actor.FirstName} {actor.LastName}" : "Administrateur";

                await _actionLogger.LogActionAsync(
                    user.OrganizationId.Value,
                    requestingUserId ?? user.Id,
                    actorName,
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
                Phone = user.Phone,
                City = user.City,
                Nationality = user.Nationality,
                BirthDate = user.BirthDate,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
    }
}
