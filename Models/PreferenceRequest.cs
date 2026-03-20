namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

public record PreferencesRequest(
    [property: JsonPropertyName("avatarId")] string? AvatarId);