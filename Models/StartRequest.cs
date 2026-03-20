namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

public record StartRequest(
    [property: JsonPropertyName("sessionToken")] string SessionToken);