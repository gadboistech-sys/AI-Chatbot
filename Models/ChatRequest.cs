namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

public record ChatRequest(
    [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
    [property: JsonPropertyName("systemPrompt")] string? SystemPrompt,
    [property: JsonPropertyName("avatarId")] string? AvatarId,
    [property: JsonPropertyName("factualMemory")] string? FactualMemory,
    [property: JsonPropertyName("relationalMemory")] string? RelationalMemory,
    [property: JsonPropertyName("voiceSignal")] VoiceSignal? VoiceSignal,
    [property: JsonPropertyName("openingLine")] string? OpeningLine,
    [property: JsonPropertyName("displayName")] string? DisplayName);