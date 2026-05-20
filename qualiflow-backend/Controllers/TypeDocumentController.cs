using System.Collections.Generic;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs;
using DocApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require authentication for all endpoints
    public class TypeDocumentController : ControllerBase
    {
        private readonly ITypeDocumentService _service;

        public TypeDocumentController(ITypeDocumentService service)
        {
            _service = service;
        }

        [HttpGet]
        [Authorize] // All authenticated users can read document types
        public async Task<ActionResult<IEnumerable<TypeDocumentResponse>>> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        [Authorize] // All authenticated users can read specific document types
        public async Task<ActionResult<TypeDocumentResponse>> GetById(int id)
        {
            try
            {
                var result = await _service.GetByIdAsync(id);
                return Ok(result);
            }
            catch (NotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")] // Only Admin can create document types
        public async Task<ActionResult<int>> Create([FromBody] CreateTypeDocumentRequest request)
        {
            try
            {
                var id = await _service.CreateAsync(request);
                return CreatedAtAction(nameof(GetById), new { id }, id);
            }
            catch (ServiceException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")] // Only Admin can update document types
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTypeDocumentRequest request)
        {
            try
            {
                await _service.UpdateAsync(id, request);
                return NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ServiceException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] // Only Admin can delete document types
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                return NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
