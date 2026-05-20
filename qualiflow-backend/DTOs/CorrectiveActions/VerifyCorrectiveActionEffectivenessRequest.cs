namespace DocApi.DTOs.CorrectiveActions
{
    public class VerifyCorrectiveActionEffectivenessRequest
    {
        public bool EffectivenessVerified { get; set; }
        public required string EffectivenessComment { get; set; }
    }
}
