namespace AI_Chatbot.Models.Entities;

public class MemoryEntry
{
    public string UserId { get; set; } = default!;
    public string AvatarId { get; set; } = default!;
    public string Memory { get; set; } = string.Empty;
    public string? RelationalMemory { get; set; }
    public string? MoodSeed { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}