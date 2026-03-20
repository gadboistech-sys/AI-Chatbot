namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

public record AuthRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password);