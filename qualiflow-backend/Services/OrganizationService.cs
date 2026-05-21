using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BCryptNet = BCrypt.Net.BCrypt;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs.Organizations;
using DocApi.Infrastructure;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;

namespace DocApi.Services
{
    public class OrganizationService : IOrganizationService
    {
        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "UNIVERSITE",
            "INSTITUT",
            "CENTRE",
            "ENTREPRISE"
        };

        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "ACTIF",
            "SUSPENDUE"
        };

        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationLogoStorageService _organizationLogoStorageService;
        private readonly INotificationEventPublisher _notificationEventPublisher;

        public OrganizationService(
            IOrganizationRepository organizationRepository,
            IUserRepository userRepository,
            IOrganizationLogoStorageService organizationLogoStorageService,
            INotificationEventPublisher notificationEventPublisher)
        {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _organizationLogoStorageService = organizationLogoStorageService;
            _notificationEventPublisher = notificationEventPublisher;
        }

        public async Task<OrganizationResponse> GetByIdAsync(int id)
        {
            var details = await _organizationRepository.GetDetailsAsync(id);
            if (details == null)
            {
                throw new NotFoundException("Organization not found");
            }

            var admins = await _organizationRepository.GetAdminsAsync(id);
            return MapDetails(details, admins);
        }

        public async Task<PagedOrganizationsResponse> GetAllAsync(OrganizationListQueryParameters query)
        {
            query.PageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            query.PageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);

            var items = await _organizationRepository.SearchAsync(query);
            var total = await _organizationRepository.CountSearchAsync(query);

            return new PagedOrganizationsResponse
            {
                Total = total,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Items = items.Select(MapListItem).ToList()
            };
        }

        public async Task<int> CreateAsync(CreateOrganizationRequest request)
        {
            ValidateCreateOrUpdate(request.Name, request.Code, request.Type, request.Status, request.Email);

            var codeExists = await _organizationRepository.GetByCodeAsync(request.Code.Trim());
            if (codeExists != null)
            {
                throw new ServiceException("Organization code already exists");
            }

            var nameExists = await _organizationRepository.GetByNameAsync(request.Name.Trim());
            if (nameExists != null)
            {
                throw new ServiceException("Organization name already exists");
            }

            var organization = new Organization
            {
                Name = request.Name.Trim(),
                Code = request.Code.Trim().ToUpperInvariant(),
                Description = NormalizeNullable(request.Description),
                Type = request.Type.Trim().ToUpperInvariant(),
                Address = NormalizeNullable(request.Address),
                Email = NormalizeNullable(request.Email),
                Phone = NormalizeNullable(request.Phone),
                Status = request.Status.Trim().ToUpperInvariant(),
                SubscriptionDaysRemaining = request.SubscriptionDaysRemaining ?? 30,
                SubscriptionMonitorEnabled = request.SubscriptionMonitorEnabled ?? true,
                LastSubscriptionDecrementAt = null,
                SubscriptionExpiryAlertSent = false,
                CreatedAt = DateTime.UtcNow
            };

            var organizationId = await _organizationRepository.CreateAsync(organization);

            if (request.FirstAdmin != null)
            {
                await CreateFirstAdminAsync(organizationId, request.FirstAdmin);
            }

            return organizationId;
        }

        public async Task<bool> UpdateAsync(int id, UpdateOrganizationRequest request, bool allowSubscriptionUpdate = true)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            if (organization == null)
            {
                throw new NotFoundException("Organization not found");
            }

            ValidateCreateOrUpdate(request.Name, organization.Code, request.Type, request.Status, request.Email);

            var nameExists = await _organizationRepository.GetByNameAsync(request.Name.Trim());
            if (nameExists != null && nameExists.Id != id)
            {
                throw new ServiceException("Organization name already exists");
            }

            organization.Name = request.Name.Trim();
            organization.Description = NormalizeNullable(request.Description);
            organization.Type = request.Type.Trim().ToUpperInvariant();
            organization.Address = NormalizeNullable(request.Address);
            organization.Email = NormalizeNullable(request.Email);
            organization.Phone = NormalizeNullable(request.Phone);
            organization.Status = request.Status.Trim().ToUpperInvariant();
            if (allowSubscriptionUpdate)
            {
                if (request.SubscriptionDaysRemaining.HasValue)
                {
                    organization.SubscriptionDaysRemaining = Math.Max(request.SubscriptionDaysRemaining.Value, 0);
                    if (organization.SubscriptionDaysRemaining > 0)
                    {
                        organization.SubscriptionExpiryAlertSent = false;
                    }
                }

                if (request.SubscriptionMonitorEnabled.HasValue)
                {
                    organization.SubscriptionMonitorEnabled = request.SubscriptionMonitorEnabled.Value;
                }
            }

            organization.UpdatedAt = DateTime.UtcNow;

            var result = await _organizationRepository.UpdateAsync(organization);
            return result;
        }

        public async Task<OrganizationResponse> ToggleStatusAsync(int id, ToggleOrganizationStatusRequest request)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            if (organization == null)
            {
                throw new NotFoundException("Organization not found");
            }

            var targetStatus = string.IsNullOrWhiteSpace(request.Status)
                ? (organization.Status.Equals("ACTIF", StringComparison.OrdinalIgnoreCase) ? "SUSPENDUE" : "ACTIF")
                : request.Status.Trim().ToUpperInvariant();

            if (!AllowedStatuses.Contains(targetStatus))
            {
                throw new ServiceException("Invalid status");
            }

            await _organizationRepository.ToggleStatusAsync(id, targetStatus);

            if (string.Equals(targetStatus, "SUSPENDUE", StringComparison.OrdinalIgnoreCase))
            {
                var users = await _userRepository.GetByOrganizationIdAsync(id, 1, 5000);
                var targetIds = users
                    .Where(user => user.IsActive)
                    .Select(user => user.Id)
                    .Distinct()
                    .ToList();

                await _notificationEventPublisher.PublishToUsersAsync(
                    id,
                    targetIds,
                    NotificationConstants.TypeOrganizationSuspended,
                    NotificationConstants.CategoryError,
                    "Organisation suspendue",
                    $"L'organisation {organization.Name} est actuellement suspendue.",
                    NotificationConstants.PriorityCritical,
                    "ORGANIZATION",
                    id.ToString(),
                    "/profile",
                    null);
            }

            return await GetByIdAsync(id);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var organization = await _organizationRepository.GetByIdAsync(id);
            if (organization == null)
            {
                throw new NotFoundException("Organization not found");
            }

            var result = await _organizationRepository.DeleteAsync(id);
            return result;
        }

        public async Task<OrganizationResponse> GetMyOrganizationAsync(UserContext userContext)
        {
            EnsureAdminOrg(userContext);
            return await GetByIdAsync(userContext.OrganizationId!.Value);
        }

        public async Task<bool> UpdateMyOrganizationAsync(UpdateOrganizationRequest request, UserContext userContext)
        {
            EnsureAdminOrg(userContext);
            return await UpdateAsync(userContext.OrganizationId!.Value, request, allowSubscriptionUpdate: false);
        }

        public async Task<OrganizationLogoResponse> UploadMyLogoAsync(UploadOrganizationLogoRequest request, UserContext userContext)
        {
            EnsureAdminOrg(userContext);

            if (request.File == null)
            {
                throw new ServiceException("Le fichier logo est obligatoire.");
            }

            var organization = await _organizationRepository.GetByIdAsync(userContext.OrganizationId!.Value);
            if (organization == null)
            {
                throw new NotFoundException("Organization not found");
            }

            var logoPath = await _organizationLogoStorageService.SaveAsync(request.File, organization.Code);
            organization.LogoPath = logoPath;
            organization.UpdatedAt = DateTime.UtcNow;

            await _organizationRepository.UpdateLogoPathAsync(organization.Id, logoPath);

            return new OrganizationLogoResponse
            {
                OrganizationId = organization.Id,
                LogoPath = logoPath,
                UpdatedAt = organization.UpdatedAt
            };
        }

        private static readonly byte[] TransparentPng = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
            0x0B, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x60, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x01, 0x48, 0xAF, 0xA4, 0x70, 0x00, 0x00, 0x00, 0x00, 0x49,
            0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        };

        public async Task<(Stream Stream, string ContentType, string FileName)> GetOrganizationLogoAsync(int id, UserContext userContext)
        {
            if (!userContext.IsSuperAdmin)
            {
                if (!userContext.OrganizationId.HasValue || userContext.OrganizationId.Value != id)
                {
                    throw new ForbiddenException("Access denied to organization logo");
                }
            }

            var organization = await _organizationRepository.GetByIdAsync(id);
            if (organization == null)
            {
                throw new NotFoundException("Organization not found");
            }

            if (string.IsNullOrWhiteSpace(organization.LogoPath))
            {
                var ms = new MemoryStream(TransparentPng);
                return (ms, "image/png", "default-logo.png");
            }

            try
            {
                return await _organizationLogoStorageService.OpenReadAsync(organization.LogoPath);
            }
            catch (Exception)
            {
                var ms = new MemoryStream(TransparentPng);
                return (ms, "image/png", "default-logo.png");
            }
        }

        public async Task<(Stream Stream, string ContentType, string FileName)> GetMyOrganizationLogoAsync(UserContext userContext)
        {
            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organization missing in user context");
            }

            return await GetOrganizationLogoAsync(userContext.OrganizationId.Value, userContext);
        }

        private async Task CreateFirstAdminAsync(int organizationId, CreateOrganizationAdminRequest firstAdmin)
        {
            if (string.IsNullOrWhiteSpace(firstAdmin.FirstName) ||
                string.IsNullOrWhiteSpace(firstAdmin.LastName) ||
                string.IsNullOrWhiteSpace(firstAdmin.Email) ||
                string.IsNullOrWhiteSpace(firstAdmin.TemporaryPassword))
            {
                throw new ServiceException("First admin information is incomplete.");
            }

            if (await _userRepository.ExistsAsync(firstAdmin.Email.Trim()))
            {
                throw new ServiceException("First admin email already exists.");
            }

            var user = new User
            {
                OrganizationId = organizationId,
                FirstName = firstAdmin.FirstName.Trim(),
                LastName = firstAdmin.LastName.Trim(),
                Email = firstAdmin.Email.Trim(),
                Username = firstAdmin.Email.Trim(),
                PasswordHash = BCryptNet.HashPassword(firstAdmin.TemporaryPassword),
                Role = UserRoles.ADMIN_ORG,
                IsActive = true,
                IsEmailVerified = true,
                EmailVerificationToken = null,
                EmailVerificationExpiresAt = null,
                CreatedAt = DateTime.UtcNow
            };

            var userId = await _userRepository.CreateAsync(user);
        }

        private static void ValidateCreateOrUpdate(string name, string code, string type, string status, string? email)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ServiceException("Organization name is required");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ServiceException("Organization code is required");
            }

            if (string.IsNullOrWhiteSpace(type) || !AllowedTypes.Contains(type.Trim().ToUpperInvariant()))
            {
                throw new ServiceException("Invalid organization type");
            }

            if (string.IsNullOrWhiteSpace(status) || !AllowedStatuses.Contains(status.Trim().ToUpperInvariant()))
            {
                throw new ServiceException("Invalid organization status");
            }

            if (!string.IsNullOrWhiteSpace(email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            {
                throw new ServiceException("Invalid organization email");
            }
        }

        private static string? NormalizeNullable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static OrganizationListItemResponse MapListItem(OrganizationListItem item)
        {
            return new OrganizationListItemResponse
            {
                Id = item.Id,
                Name = item.Name,
                Code = item.Code,
                Type = item.Type,
                Status = item.Status,
                Email = item.Email,
                Phone = item.Phone,
                LogoPath = item.LogoPath,
                SubscriptionDaysRemaining = item.SubscriptionDaysRemaining,
                SubscriptionMonitorEnabled = item.SubscriptionMonitorEnabled,
                UsersCount = item.UsersCount,
                AdminsCount = item.AdminsCount,
                CreatedAt = item.CreatedAt
            };
        }

        private static OrganizationResponse MapDetails(OrganizationDetails details, IEnumerable<OrganizationAdmin> admins)
        {
            return new OrganizationResponse
            {
                Id = details.Id,
                Name = details.Name,
                Code = details.Code,
                Description = details.Description,
                Type = details.Type,
                Address = details.Address,
                Email = details.Email,
                Phone = details.Phone,
                LogoPath = details.LogoPath,
                Status = details.Status,
                SubscriptionDaysRemaining = details.SubscriptionDaysRemaining,
                SubscriptionMonitorEnabled = details.SubscriptionMonitorEnabled,
                UsersCount = details.UsersCount,
                AdminsCount = details.AdminsCount,
                Admins = admins.Select(admin => new OrganizationAdminSummaryResponse
                {
                    Id = admin.Id,
                    FirstName = admin.FirstName,
                    LastName = admin.LastName,
                    Email = admin.Email,
                    IsActive = admin.IsActive,
                    CreatedAt = admin.CreatedAt
                }).ToList(),
                CreatedAt = details.CreatedAt,
                UpdatedAt = details.UpdatedAt
            };
        }

        private static void EnsureAdminOrg(UserContext userContext)
        {
            if (!string.Equals(userContext.Role, UserRoles.ADMIN_ORG, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("Only ADMIN_ORG can modify organization profile");
            }

            if (!userContext.OrganizationId.HasValue)
            {
                throw new ForbiddenException("Organization missing in user context");
            }
        }
    }
}

