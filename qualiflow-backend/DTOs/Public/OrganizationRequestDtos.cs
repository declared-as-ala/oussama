using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Public
{
    public class SubmitOrganizationRequest
    {
        [Required(ErrorMessage = "Le nom complet est obligatoire.")]
        public string FullName { get; set; } = default!;

        [Required(ErrorMessage = "L'adresse email est obligatoire.")]
        [EmailAddress(ErrorMessage = "L'adresse email n'est pas valide.")]
        public string Email { get; set; } = default!;

        [Required(ErrorMessage = "Le numéro de téléphone est obligatoire.")]
        public string Phone { get; set; } = default!;

        [Required(ErrorMessage = "Le pays est obligatoire.")]
        public string Country { get; set; } = default!;

        [Required(ErrorMessage = "Le poste est obligatoire.")]
        public string JobTitle { get; set; } = default!;

        [Required(ErrorMessage = "Le nom de l'organisation est obligatoire.")]
        public string OrganizationName { get; set; } = default!;

        [Required(ErrorMessage = "Le type d'organisation est obligatoire.")]
        public string OrganizationType { get; set; } = default!;

        [Required(ErrorMessage = "Le message est obligatoire.")]
        public string Message { get; set; } = default!;

        [Required(ErrorMessage = "Le code de validation est obligatoire.")]
        public string ValidationCode { get; set; } = default!;
    }

    public class SendVerificationCodeRequest
    {
        [Required(ErrorMessage = "L'adresse email est obligatoire.")]
        [EmailAddress(ErrorMessage = "L'adresse email n'est pas valide.")]
        public string Email { get; set; } = default!;
    }

    public class VerifyCodeRequest
    {
        [Required(ErrorMessage = "L'adresse email est obligatoire.")]
        [EmailAddress(ErrorMessage = "L'adresse email n'est pas valide.")]
        public string Email { get; set; } = default!;

        [Required(ErrorMessage = "Le code de validation est obligatoire.")]
        public string Code { get; set; } = default!;
    }

    public class SubmitOrganizationRequestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = default!;
    }
}
