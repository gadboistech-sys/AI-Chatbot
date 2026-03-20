namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

public record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);