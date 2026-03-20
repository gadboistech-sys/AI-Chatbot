namespace AI_Chatbot.Models;

/// <summary>
/// Contextual data loaded at session start and passed to SystemPromptBuilder.
/// Carries time awareness and mood continuity across sessions.
/// </summary>
public record SessionContext(
    /// <summary>UTC timestamp of the previous session end. Null on first ever session.</summary>
    DateTime? LastSessionAt,

    /// <summary>
    /// One-liner mood seed saved at the end of the last session.
    /// e.g. "ended in a reflective, slightly melancholic mood after discussing a difficult work situation"
    /// Null if no prior session or no notable mood was recorded.
    /// </summary>
    string? MoodSeed
);