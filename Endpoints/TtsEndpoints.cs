namespace AI_Chatbot.Endpoints;

using AI_Chatbot.Models;
using AI_Chatbot.Services;

public static class TtsEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/voices", async (TtsService tts, IHttpClientFactory factory) =>
        {
            if (tts.IsAzure)
                return Results.Ok(new { voices = tts.VoiceList });

            // ElevenLabs — fetch premade voices from API
            var client = factory.CreateClient("elevenlabs");
            using var req = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, "v1/voices");
            var res = await client.SendAsync(req);
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return Results.Problem($"ElevenLabs voices error: {text}", statusCode: (int)res.StatusCode);

            using var doc = System.Text.Json.JsonDocument.Parse(text);
            var premade = doc.RootElement.GetProperty("voices")
                .EnumerateArray()
                .Where(v => v.TryGetProperty("category", out var cat) && cat.GetString() == "premade")
                .Select(v => new
                {
                    voice_id = v.GetProperty("voice_id").GetString(),
                    name = v.GetProperty("name").GetString(),
                    gender = v.TryGetProperty("labels", out var labels) &&
                               labels.TryGetProperty("gender", out var g)
                               ? g.GetString() : "unknown"
                })
                .OrderBy(v => v.name)
                .ToList();

            return Results.Ok(new { voices = premade });
        }).RequireAuthorization();

        app.MapGet("/voices/defaults", (TtsService tts) =>
        {
            var (male, female) = tts.DefaultVoices;
            return Results.Ok(new { male, female });
        }).RequireAuthorization();

        app.MapPost("/tts", async (TtsRequest req, TtsService tts) =>
        {
            return await tts.SynthesiseAsync(req);
        }).RequireAuthorization();

        // ── GET /tts/filler?mood=...&voiceId=...&gender=... ───────────────────
        // Returns pre-chunked PCM for a short filler phrase matched to the mood.
        // Called at session connect time to pre-cache clips client-side.
        app.MapGet("/tts/filler", async (
            string? mood, string? voiceId, string? gender, TtsService tts) =>
        {
            var phrase = TtsService.GetFillerPhrase(mood);
            var effectiveVoice = string.IsNullOrEmpty(voiceId) ? null : voiceId;
            var req = new TtsRequest(phrase, effectiveVoice, gender, mood);
            return await tts.SynthesiseAsync(req);
        }).RequireAuthorization();
    }
}