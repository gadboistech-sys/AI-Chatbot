namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Normalized vocal sentiment signal passed with each user message.
/// Populated by the active IVoiceSentimentProvider —
/// currently BrowserSignalProvider (lightweight), Hume AI later.
///
/// All fields are nullable so a partial signal is still useful.
/// </summary>
public record VoiceSignal
{
    /// <summary>
    /// Estimated speaking rate category derived from words-per-second.
    /// "fast" | "normal" | "slow"
    /// </summary>
    [JsonPropertyName("pace")]
    public string? Pace { get; init; }

    /// <summary>
    /// Normalised RMS audio energy: 0.0 (silent) – 1.0 (loud/energetic).
    /// Measured via Web Audio API AnalyserNode.
    /// </summary>
    [JsonPropertyName("energy")]
    public double? Energy { get; init; }

    /// <summary>
    /// Number of pauses detected (silence gaps > 500ms during speech).
    /// High pause count often signals hesitation or low energy.
    /// </summary>
    [JsonPropertyName("pauseCount")]
    public int? PauseCount { get; init; }

    /// <summary>
    /// Source of this signal — used for logging and future routing.
    /// "browser" | "hume"
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = "browser";

    /// <summary>
    /// Optional richer emotion label from a provider like Hume.
    /// Null when using browser signals.
    /// e.g. "excited", "sad", "anxious", "content"
    /// </summary>
    [JsonPropertyName("emotion")]
    public string? Emotion { get; init; }

    /// <summary>
    /// Formats the signal as a compact one-liner for injection into
    /// the mood inference prompt. Omits nulls gracefully.
    /// </summary>
    public string ToPromptHint()
    {
        var parts = new List<string>();
        if (Pace != null) parts.Add($"pace: {Pace}");
        if (Energy != null) parts.Add($"energy: {Energy:F2}");
        if (PauseCount != null) parts.Add($"pauses: {PauseCount}");
        if (Emotion != null) parts.Add($"detected emotion: {Emotion}");
        return parts.Count > 0
            ? $"[voice signal — {string.Join(", ", parts)}]"
            : string.Empty;
    }
}