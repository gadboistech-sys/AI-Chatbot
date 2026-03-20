namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

public record InferredMood(
    [property: JsonPropertyName("current_mood")] string CurrentMood,
    [property: JsonPropertyName("trajectory")] string Trajectory,
    [property: JsonPropertyName("tone_instruction")] string ToneInstruction,
    [property: JsonPropertyName("confidence")] string Confidence);