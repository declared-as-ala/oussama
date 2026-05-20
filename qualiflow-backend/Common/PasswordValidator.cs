using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocApi.Common
{
    public static class PasswordValidator
    {
        public const int MinimumLength = 8;

        public static (bool IsValid, string? ErrorMessage) Validate(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "Le mot de passe est obligatoire.");
            }

            if (password.Length < MinimumLength)
            {
                return (false, $"Le mot de passe doit contenir au moins {MinimumLength} caractères.");
            }

            if (!password.Any(char.IsUpper))
            {
                return (false, "Le mot de passe doit contenir au moins une lettre majuscule.");
            }

            if (!password.Any(char.IsLower))
            {
                return (false, "Le mot de passe doit contenir au moins une lettre minuscule.");
            }

            if (!password.Any(char.IsDigit))
            {
                return (false, "Le mot de passe doit contenir au moins un chiffre.");
            }

            // Special characters check
            if (!Regex.IsMatch(password, @"[!@#$%^&*(),.""?|:{}|<>]"))
            {
                return (false, "Le mot de passe doit contenir au moins un caractère spécial (ex: !@#$%^&*).");
            }

            return (true, null);
        }
    }
}
