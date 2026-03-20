namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

public record TtsRequest(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("voiceId")] string? VoiceId,
    [property: JsonPropertyName("gender")] string? Gender,
    [property: JsonPropertyName("currentMood")] string? CurrentMood);