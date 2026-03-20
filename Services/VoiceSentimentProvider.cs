namespace AI_Chatbot.Services;

using AI_Chatbot.Models;

/// <summary>
/// Abstraction over vocal sentiment sources.
/// Today: BrowserSignalProvider (lightweight, client-computed).
/// Future: HumeSignalProvider (full audio emotion analysis via Hume AI API).
///
/// Switch providers via appsettings.json: "VoiceSentiment:Provider": "Browser" | "Hume"
/// </summary>
public interface IVoiceSentimentProvider
{
    /// <summary>
    /// Enriches or validates an incoming VoiceSignal.
    /// For browser signals this is a pass-through with normalisation.
    /// For Hume this would make an API call with the raw audio.
    /// Returns null if no signal is available or signal is below confidence threshold.
    /// </summary>
    Task<VoiceSignal?> ProcessAsync(VoiceSignal? incomingSignal,
        CancellationToken ct = default);
}

// ── Browser implementation (current) ──────────────────────────────────────────
// Client-computed signals arrive pre-calculated. This provider normalises
// them and applies confidence filtering.

public class BrowserSignalProvider : IVoiceSentimentProvider
{
    // Minimum energy threshold — signals below this are likely background noise
    private const double MinEnergy = 0.05;

    public Task<VoiceSignal?> ProcessAsync(VoiceSignal? signal,
        CancellationToken ct = default)
    {
        if (signal is null) return Task.FromResult<VoiceSignal?>(null);

        // Filter out noise-floor readings
        if (signal.Energy.HasValue && signal.Energy.Value < MinEnergy)
            return Task.FromResult<VoiceSignal?>(null);

        // Normalise energy to 0–1 range (clamp in case of client rounding)
        var normalisedEnergy = signal.Energy.HasValue
            ? Math.Clamp(signal.Energy.Value, 0.0, 1.0)
            : (double?)null;

        return Task.FromResult<VoiceSignal?>(signal with
        {
            Energy = normalisedEnergy,
            Source = "browser"
        });
    }
}

// ── Hume implementation (placeholder — wire up when ready) ────────────────────
// To activate:
//   1. Add "VoiceSentiment:Provider": "Hume" to appsettings.json
//   2. Add "Hume:ApiKey" to appsettings.json
//   3. Implement ProcessAsync to POST raw audio to Hume Expression Measurement API
//      (Audio Only endpoint: POST https://api.hume.ai/v0/batch/jobs)
//   4. Map Hume's prosody/emotion response onto VoiceSignal.Emotion + Pace + Energy
//   5. Register HumeSignalProvider instead of BrowserSignalProvider in Program.cs

public class HumeSignalProvider : IVoiceSentimentProvider
{
    // Constructor will accept IHttpClientFactory + IConfiguration when implemented
    public Task<VoiceSignal?> ProcessAsync(VoiceSignal? signal,
        CancellationToken ct = default)
    {
        // TODO: Send raw audio bytes to Hume Expression Measurement API.
        // The VoiceSignal model already has an Emotion field ready for Hume's output.
        // See: https://dev.hume.ai/docs/expression-measurement/batch
        throw new NotImplementedException(
            "HumeSignalProvider is a future integration. " +
            "Set VoiceSentiment:Provider to 'Browser' until implemented.");
    }
}