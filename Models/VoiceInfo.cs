namespace English_Listen_WinUI.Models
{
    public class VoiceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Culture { get; set; }
        public VoiceGender Gender { get; set; } = VoiceGender.Neutral;
        public bool IsDefault { get; set; }
        public bool IsNatural { get; set; }
        public string Description => $"{DisplayName} ({Culture ?? "en-US"}, {Gender})";
    }

    public enum VoiceGender
    {
        Male,
        Female,
        Neutral
    }
}