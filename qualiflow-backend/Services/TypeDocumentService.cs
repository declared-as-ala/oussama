using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.Domain.Entities;
using DocApi.DTOs;
using DocApi.Repositories.Interfaces;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace DocApi.Services
{
    // Service implementation for TypeDocument
    public class TypeDocumentService : ITypeDocumentService
    {
        private readonly ITypeDocumentRepository _repository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TypeDocumentService(ITypeDocumentRepository repository, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _httpContextAccessor = httpContextAccessor;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new ServiceException("User not authenticated");
            }
            return userId;
        }

        public async Task<int> CreateAsync(CreateTypeDocumentRequest request)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ServiceException("Name is required.");

            var currentUserId = GetCurrentUserId();

            var entity = new TypeDocument
            {
                Name = request.Name,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId
            };

            return await _repository.CreateAsync(entity);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var exists = await _repository.GetByIdAsync(id);
            if (exists == null)
                throw new NotFoundException($"TypeDocument with ID {id} not found.");

            return await _repository.DeleteAsync(id);
        }

        public async Task<IEnumerable<TypeDocumentResponse>> GetAllAsync()
        {
            var entities = await _repository.GetAllAsync();
            return entities.Select(e => new TypeDocumentResponse
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                CreatedByUserId = e.CreatedByUserId,
                CreatedByUsername = e.CreatedByUser?.Username,
                UpdatedByUserId = e.UpdatedByUserId,
                UpdatedByUsername = e.UpdatedByUser?.Username
            });
        }

        public async Task<TypeDocumentResponse> GetByIdAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
                throw new NotFoundException($"TypeDocument with ID {id} not found.");

            return new TypeDocumentResponse
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                CreatedByUserId = entity.CreatedByUserId,
                CreatedByUsername = entity.CreatedByUser?.Username,
                UpdatedByUserId = entity.UpdatedByUserId,
                UpdatedByUsername = entity.UpdatedByUser?.Username
            };
        }

        public async Task<bool> UpdateAsync(int id, UpdateTypeDocumentRequest request)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ServiceException("Name is required.");

            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
                throw new NotFoundException($"TypeDocument with ID {id} not found.");

            var currentUserId = GetCurrentUserId();

            entity.Name = request.Name;
            entity.Description = request.Description;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedByUserId = currentUserId;

            return await _repository.UpdateAsync(entity);
        }
    }
}
