namespace AI_Chatbot.Models;

public record MoodState(
    string CurrentMood,
    string ToneInstruction,
    string Trajectory,
    int MessagesSinceUpdate,
    int ConsecutiveEscalations)
{
    public static readonly MoodState Neutral = new(
        CurrentMood: "neutral",
        ToneInstruction: "Maintain your warm, natural companion tone.",
        Trajectory: "stable",
        MessagesSinceUpdate: 0,
        ConsecutiveEscalations: 0);
}