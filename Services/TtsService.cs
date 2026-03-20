namespace AI_Chatbot.Services;

using AI_Chatbot.Models;
using System.Text;
using System.Text.Json;

public class TtsService(IHttpClientFactory factory, IConfiguration config)
{
    private readonly string _provider = config["Tts:Provider"] ?? "ElevenLabs";
    private readonly string _elevenLabsMaleVoiceId = config["ElevenLabs:DefaultMaleVoiceId"]!;
    private readonly string _elevenLabsFemaleVoiceId = config["ElevenLabs:DefaultFemaleVoiceId"]!;
    private readonly string _azureMaleVoice = config["Azure:DefaultMaleVoice"]!;
    private readonly string _azureFemaleVoice = config["Azure:DefaultFemaleVoice"]!;

    public bool IsAzure => _provider.Equals("Azure", StringComparison.OrdinalIgnoreCase);

    public (string male, string female) DefaultVoices => IsAzure
        ? (_azureMaleVoice, _azureFemaleVoice)
        : (_elevenLabsMaleVoiceId, _elevenLabsFemaleVoiceId);

    public object VoiceList => IsAzure
        ? new[]
          {
              new { voice_id = _azureMaleVoice,        name = "Andrew (Azure)", gender = "male"   },
              new { voice_id = _azureFemaleVoice,      name = "Ava (Azure)",    gender = "female" }
          }
        : Array.Empty<object>();

    public async Task<IResult> SynthesiseAsync(TtsRequest req)
    {
        return IsAzure
            ? await AzureTtsAsync(req)
            : await ElevenLabsTtsAsync(req);
    }

    // ── Filler phrase mapping ──────────────────────────────────────────────────
    private static readonly Dictionary<string, string[]> FillerPhrases = new()
    {
        ["reflective"] = ["Hmm, let me think about that.", "That is interesting.", "Well, let me think."],
        ["thoughtful"] = ["Hmm, let me think.", "Well now.", "Let me consider that."],
        ["stressed"] = ["Okay, okay.", "Right, right.", "Alright then."],
        ["anxious"] = ["Okay.", "Right.", "Let me think."],
        ["playful"] = ["Ooh, let me see!", "Hmm, okay!", "Right then!"],
        ["excited"] = ["Oh, interesting!", "Ooh, let me think.", "Let me see."],
        ["sad"] = ["Hmm.", "Yeah.", "Well."],
        ["melancholic"] = ["Hmm.", "Yeah, I see.", "Well."],
        ["tired"] = ["Mm, right.", "Yeah.", "Right."],
        ["warm"] = ["Well now.", "Hmm, let me think.", "Let me see."],
        ["neutral"] = ["Hmm, let me think.", "Well.", "Let me think about that."],
    };

    /// <summary>Returns a random filler phrase for the given mood.</summary>
    public static string GetFillerPhrase(string? mood)
    {
        var key = mood?.ToLowerInvariant() ?? "neutral";
        var phrases = FillerPhrases.TryGetValue(key, out var p)
            ? p : FillerPhrases["neutral"];
        return phrases[Random.Shared.Next(phrases.Length)];
    }

    // ── Prosody mapping ────────────────────────────────────────────────────────
    // Azure SSML prosody attribute rules:
    //   rate:   relative % e.g. "+10%" or keywords x-slow/slow/medium/fast/x-fast
    //   pitch:  relative % e.g. "+5%"  or keywords x-low/low/medium/high/x-high
    //   volume: keywords ONLY — x-soft/soft/medium/loud/x-loud  (NO % or dB values)
    //   Use "default" as a sentinel meaning "omit this attribute entirely"
    private static (string rate, string pitch, string volume) GetProsody(string? mood)
    {
        return mood?.ToLowerInvariant() switch
        {
            "excited" or
            "playful" or
            "energetic" => (rate: "+8%", pitch: "+6%", volume: "loud"),

            "warm" or
            "engaged" or
            "curious" => (rate: "+2%", pitch: "+2%", volume: "medium"),

            "neutral" or
            "" or
            null => (rate: "default", pitch: "default", volume: "default"),

            "tired" or
            "subdued" or
            "quiet" => (rate: "-6%", pitch: "-4%", volume: "soft"),

            "sad" or
            "melancholic" or
            "low" => (rate: "-10%", pitch: "-6%", volume: "soft"),

            "stressed" or
            "anxious" or
            "tense" => (rate: "+5%", pitch: "-3%", volume: "loud"),

            "reflective" or
            "thoughtful" => (rate: "-5%", pitch: "-2%", volume: "medium"),

            _ => (rate: "default", pitch: "default", volume: "default")
        };
    }

    // ── ElevenLabs ─────────────────────────────────────────────────────────────
    private async Task<IResult> ElevenLabsTtsAsync(TtsRequest req)
    {
        var client = factory.CreateClient("elevenlabs");
        var voiceId = !string.IsNullOrWhiteSpace(req.VoiceId) ? req.VoiceId
            : req.Gender == "male" ? _elevenLabsMaleVoiceId
            : _elevenLabsFemaleVoiceId;

        var (stability, style) = GetElevenLabsVoiceSettings(req.CurrentMood);

        var body = new
        {
            text = req.Text,
            model_id = "eleven_turbo_v2_5",
            voice_settings = new
            {
                stability,
                similarity_boost = 0.80,
                style,
                use_speaker_boost = true
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"v1/text-to-speech/{voiceId}/stream?output_format=pcm_24000");
        request.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return Results.Problem($"ElevenLabs error: {err}", statusCode: (int)response.StatusCode);
        }

        var pcmBytes = await response.Content.ReadAsByteArrayAsync();
        return Results.Ok(new { chunks = ChunkPcm(pcmBytes) });
    }

    private static (double stability, double style) GetElevenLabsVoiceSettings(string? mood)
    {
        return mood?.ToLowerInvariant() switch
        {
            "excited" or "playful" or "energetic" => (stability: 0.30, style: 0.55),
            "sad" or "melancholic" => (stability: 0.65, style: 0.20),
            "stressed" or "anxious" => (stability: 0.35, style: 0.45),
            "reflective" or "thoughtful" => (stability: 0.60, style: 0.25),
            "tired" or "subdued" => (stability: 0.65, style: 0.15),
            _ => (stability: 0.45, style: 0.35)
        };
    }

    // ── Azure ──────────────────────────────────────────────────────────────────
    private async Task<IResult> AzureTtsAsync(TtsRequest req)
    {
        try
        {
            var client = factory.CreateClient("azure-tts");
            var voice = !string.IsNullOrWhiteSpace(req.VoiceId) ? req.VoiceId
                : req.Gender == "male" ? _azureMaleVoice
                : _azureFemaleVoice;

            var (rate, pitch, volume) = GetProsody(req.CurrentMood);
            var escapedText = System.Security.SecurityElement.Escape(req.Text);
            var enhancedText = EnhanceWithSsml(escapedText);

            // Only wrap in <prosody> when at least one attribute differs from default
            string ssmlBody;
            if (rate == "default" && pitch == "default" && volume == "default")
            {
                ssmlBody = enhancedText;
            }
            else
            {
                var prosodyAttrs = new StringBuilder();
                if (rate != "default") prosodyAttrs.Append($" rate=\"{rate}\"");
                if (pitch != "default") prosodyAttrs.Append($" pitch=\"{pitch}\"");
                if (volume != "default") prosodyAttrs.Append($" volume=\"{volume}\"");
                ssmlBody = $"<prosody{prosodyAttrs}>{enhancedText}</prosody>";
            }

            var ssml = $"""
                <speak version='1.0' xml:lang='en-US'>
                  <voice name='{voice}'>{ssmlBody}</voice>
                </speak>
                """;

            var request = new HttpRequestMessage(HttpMethod.Post, "cognitiveservices/v1");
            request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
            request.Headers.Add("X-Microsoft-OutputFormat", "raw-24khz-16bit-mono-pcm");

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return Results.Problem($"Azure TTS error: {err}", statusCode: (int)response.StatusCode);
            }

            var pcmBytes = await response.Content.ReadAsByteArrayAsync();
            return Results.Ok(new { chunks = ChunkPcm(pcmBytes) });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AzureTtsAsync exception: {ex}");
            return Results.Problem($"Azure TTS exception: {ex.Message}", statusCode: 500);
        }
    }

    // ── SSML enhancement ───────────────────────────────────────────────────────
    // Adds natural breaks after reflective openers.
    // Note: <emphasis> is not supported by Azure Multilingual Neural voices.
    private static readonly string[] ReflectiveOpeners =
    [
        "well,", "well...", "hmm,", "hmm...", "hm,", "hm...",
        "you know,", "you know...", "i mean,", "i mean...",
        "honestly,", "honestly...", "actually,", "actually...",
        "look,", "look...", "so,", "so...", "ah,", "ah...",
        "oh,", "oh...", "right,", "right..."
    ];

    private static string EnhanceWithSsml(string text)
    {
        var sentences = System.Text.RegularExpressions.Regex
            .Split(text.Trim(), @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        var enhanced = new StringBuilder();
        foreach (var sentence in sentences)
        {
            var s = sentence.Trim();
            foreach (var opener in ReflectiveOpeners)
            {
                if (s.StartsWith(opener, StringComparison.OrdinalIgnoreCase))
                {
                    var rest = s[opener.Length..].TrimStart();
                    s = $"{opener} <break time=\"350ms\"/>{rest}";
                    break;
                }
            }
            enhanced.Append(s);
            if (!s.EndsWith(' ')) enhanced.Append(' ');
        }
        return enhanced.ToString().Trim();
    }

    // ── Shared ─────────────────────────────────────────────────────────────────
    private static List<string> ChunkPcm(byte[] pcmBytes)
    {
        const int chunkBytes = 24000;
        var chunks = new List<string>();
        for (int i = 0; i < pcmBytes.Length; i += chunkBytes)
        {
            var slice = pcmBytes.AsSpan(i, Math.Min(chunkBytes, pcmBytes.Length - i));
            chunks.Add(Convert.ToBase64String(slice));
        }
        return chunks;
    }
}