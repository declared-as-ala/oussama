using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DocApi.DTOs.Organizations
{
    public class UploadOrganizationLogoRequest
    {
        [Required]
        public IFormFile? File { get; set; }
    }
}
