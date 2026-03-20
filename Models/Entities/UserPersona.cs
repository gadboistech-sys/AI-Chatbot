namespace AI_Chatbot.Models.Entities;

public class UserPersona
{
    public string UserId { get; set; } = default!;
    public string AvatarId { get; set; } = default!;
    public string PersonaJson { get; set; } = default!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}