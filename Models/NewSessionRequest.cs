namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

public record NewSessionRequest(
    [property: JsonPropertyName("avatarId")] string? AvatarId,
    [property: JsonPropertyName("isSandbox")] bool? IsSandbox);