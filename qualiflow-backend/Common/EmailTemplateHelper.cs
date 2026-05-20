using System;
using System.Net;
using System.Text;

namespace DocApi.Common
{
    public static class EmailTemplateHelper
    {
        private const string PrimaryColor = "#217346"; // Excel Modern Green
        private const string BgColor = "#F5F6F7";
        private const string CardBg = "#FFFFFF";
        private const string TextColor = "#1A1A1A";
        private const string SecondaryTextColor = "#505860";
        private const string MutedTextColor = "#8F959C";
        private const string InfoBg = "#E9F5EE";

        public static string GetVerificationCodeEmail(string userName, string code, int expiryMinutes, string verificationLink)
        {
            var title = "Vérification de votre compte";
            var content = $@"
                <div style='font-size: 18px; font-weight: 600; margin-bottom: 20px;'>Bonjour {userName},</div>
                <p>Bienvenue sur <strong>QualiFlow</strong> ! Pour finaliser la création de votre compte, veuillez utiliser le code de vérification ci-dessous :</p>
                
                <div style='background-color: {InfoBg}; border: 2px dashed {PrimaryColor}; border-radius: 8px; padding: 25px; text-align: center; margin: 30px 0;'>
                    <div style='font-size: 14px; color: {SecondaryTextColor}; margin-bottom: 10px; text-transform: uppercase; letter-spacing: 1px;'>Votre code de vérification</div>
                    <div style='font-size: 36px; font-weight: 700; color: {PrimaryColor}; letter-spacing: 5px;'>{code}</div>
                    <div style='font-size: 13px; color: {MutedTextColor}; margin-top: 10px;'>Ce code expire dans {expiryMinutes} minutes.</div>
                </div>
                
                <p>Vous pouvez également cliquer sur le bouton ci-dessous pour accéder directement à la page de vérification :</p>
                <div style='text-align: center; margin-top: 30px;'>
                    <a href='{verificationLink}' style='display: inline-block; padding: 12px 30px; background-color: {PrimaryColor}; color: #FFFFFF; text-decoration: none; border-radius: 6px; font-weight: 600; font-size: 15px;'>Vérifier mon compte</a>
                </div>
            ";

            return WrapInBaseTemplate(title, content);
        }

        public static string GetPasswordResetEmail(string userName, string code, int expiryMinutes)
        {
            var title = "Réinitialisation de votre mot de passe";
            var content = $@"
                <div style='font-size: 18px; font-weight: 600; margin-bottom: 20px;'>Bonjour {userName},</div>
                <p>Nous avons reçu une demande de réinitialisation de mot de passe pour votre compte QualiFlow.</p>
                
                <div style='background-color: {InfoBg}; border: 2px dashed {PrimaryColor}; border-radius: 8px; padding: 25px; text-align: center; margin: 30px 0;'>
                    <div style='font-size: 14px; color: {SecondaryTextColor}; margin-bottom: 10px; text-transform: uppercase; letter-spacing: 1px;'>Votre code de réinitialisation</div>
                    <div style='font-size: 36px; font-weight: 700; color: {PrimaryColor}; letter-spacing: 5px;'>{code}</div>
                    <div style='font-size: 13px; color: {MutedTextColor}; margin-top: 10px;'>Ce code expire dans {expiryMinutes} minutes.</div>
                </div>
                
                <p>Veuillez saisir ce code sur la plateforme pour définir un nouveau mot de passe. Si vous n'êtes pas à l'origine de cette demande, vous pouvez ignorer cet email en toute sécurité.</p>
            ";

            return WrapInBaseTemplate(title, content);
        }

        public static string GetAdminCreatedAccountEmail(string userName, string email, string password)
        {
            var safeUserName = string.IsNullOrWhiteSpace(userName) ? "Utilisateur" : WebUtility.HtmlEncode(userName.Trim());
            var safeEmail = WebUtility.HtmlEncode(email.Trim());
            var safePassword = WebUtility.HtmlEncode(password);

            var title = "Votre compte QualiFlow est prêt";
            var content = $@"
                <div style='font-size: 18px; font-weight: 600; margin-bottom: 20px;'>Bonjour {safeUserName},</div>
                <p>Votre compte <strong>QualiFlow</strong> a été créé et validé automatiquement par votre administrateur.</p>

                <div style='background-color: {InfoBg}; border: 1px solid #C9E7D6; border-radius: 10px; padding: 22px; margin: 28px 0;'>
                    <div style='font-size: 14px; color: {SecondaryTextColor}; margin-bottom: 14px; text-transform: uppercase; letter-spacing: 0.8px; font-weight: 700;'>Vos identifiants de connexion</div>
                    <div style='margin-bottom: 12px;'>
                        <span style='display: inline-block; width: 130px; color: {SecondaryTextColor}; font-weight: 600;'>Adresse email :</span>
                        <span style='color: {TextColor}; font-weight: 700;'>{safeEmail}</span>
                    </div>
                    <div>
                        <span style='display: inline-block; width: 130px; color: {SecondaryTextColor}; font-weight: 600;'>Mot de passe :</span>
                        <span style='color: {TextColor}; font-weight: 700;'>{safePassword}</span>
                    </div>
                </div>

                <p>Vous pouvez vous connecter dès maintenant avec ces informations.</p>
                <p style='color: {SecondaryTextColor}; font-size: 14px;'>Pour votre sécurité, nous vous recommandons de modifier votre mot de passe après votre première connexion.</p>
            ";

            return WrapInBaseTemplate(title, content);
        }

        public static string GetNotificationEmail(string fullName, string title, string message, string type, DateTime createdAt)
        {
            var safeFullName = string.IsNullOrWhiteSpace(fullName) ? "Utilisateur" : WebUtility.HtmlEncode(fullName.Trim());
            var safeTitle = string.IsNullOrWhiteSpace(title) ? "Nouvelle notification" : WebUtility.HtmlEncode(title.Trim());
            var safeMessage = string.IsNullOrWhiteSpace(message)
                ? "Vous avez reçu une nouvelle notification sur QualiFlow."
                : WebUtility.HtmlEncode(message.Trim()).Replace("\n", "<br/>");
            var safeType = string.IsNullOrWhiteSpace(type) ? "INFORMATION" : WebUtility.HtmlEncode(type.Trim());
            var formattedDate = createdAt.ToLocalTime().ToString("dd/MM/yyyy à HH:mm");

            var emailTitle = "Nouvelle notification QualiFlow";
            var content = $@"
                <div style='font-size: 18px; font-weight: 600; margin-bottom: 18px;'>Bonjour {safeFullName},</div>
                <p style='margin: 0 0 18px 0;'>Vous avez reçu une nouvelle notification sur <strong>QualiFlow</strong>.</p>

                <div style='background-color: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 10px; padding: 20px; margin: 22px 0;'>
                    <div style='margin-bottom: 12px; font-size: 17px; font-weight: 700; color: {TextColor};'>{safeTitle}</div>

                    <div style='margin-bottom: 14px; color: {TextColor};'>
                        <span style='display: inline-block; font-weight: 600; margin-right: 6px;'>Message :</span>
                        <span>{safeMessage}</span>
                    </div>

                    <div style='display: inline-block; background-color: {InfoBg}; color: {PrimaryColor}; border: 1px solid #C9E7D6; border-radius: 999px; padding: 5px 12px; font-size: 12px; font-weight: 700; letter-spacing: 0.3px; margin-bottom: 12px;'>
                        {safeType}
                    </div>

                    <div style='font-size: 13px; color: {SecondaryTextColor};'>
                        <strong>Date :</strong> {formattedDate}
                    </div>
                </div>

                <p style='margin: 0; color: {SecondaryTextColor}; font-size: 14px;'>
                    Connectez-vous à votre espace pour consulter tous les détails et agir rapidement.
                </p>
            ";

            return WrapInBaseTemplate(emailTitle, content);
        }

        private static string WrapInBaseTemplate(string title, string content)
        {
            return $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{title}</title>
</head>
<body style='font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; background-color: {BgColor}; margin: 0; padding: 0; color: {TextColor};'>
    <div style='max-width: 600px; margin: 40px auto; background-color: {CardBg}; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.08); border: 1px solid #E1E3E5;'>
        <div style='background-color: {PrimaryColor}; padding: 35px 20px; text-align: center;'>
            <div style='display: inline-block; vertical-align: middle;'>
                <svg width='40' height='40' viewBox='0 0 40 40' fill='none' xmlns='http://www.w3.org/2000/svg' style='vertical-align: middle;'>
                    <rect width='40' height='40' rx='8' fill='white'/>
                    <path d='M12 10H28V30H12V10Z' fill='{PrimaryColor}'/>
                    <path d='M16 14H24V26H16V14Z' fill='white'/>
                </svg>
            </div>
            <span style='color: #FFFFFF; font-size: 28px; font-weight: 700; margin-left: 12px; vertical-align: middle; letter-spacing: 0.5px;'>QualiFlow</span>
        </div>
        <div style='padding: 40px; line-height: 1.6;'>
            {content}
            
            <div style='margin-top: 40px; padding-top: 25px; border-top: 1px solid #E1E3E5; font-size: 14px; color: {SecondaryTextColor};'>
                Cordialement,<br>
                <strong>L'équipe QualiFlow</strong>
            </div>
        </div>
        <div style='background-color: #F8F9FA; padding: 25px; text-align: center; font-size: 12px; color: {MutedTextColor}; border-top: 1px solid #E1E3E5;'>
            <p style='margin: 0 0 8px 0;'>&copy; 2026 QualiFlow Platform. Tous droits réservés.</p>
            <p style='margin: 0;'>Ceci est un message automatique, merci de ne pas y répondre.</p>
        </div>
    </div>
    <div style='text-align: center; margin-top: 20px; font-size: 12px; color: {MutedTextColor}; padding-bottom: 40px;'>
        Vous avez reçu cet email car vous êtes inscrit sur la plateforme QualiFlow.
    </div>
</body>
</html>";
        }
    }
}
