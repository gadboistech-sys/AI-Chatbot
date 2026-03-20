namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

public record MemorySaveRequest(
    [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
    [property: JsonPropertyName("avatarName")] string AvatarName,
    [property: JsonPropertyName("avatarId")] string? AvatarId);