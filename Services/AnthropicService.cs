namespace AI_Chatbot.Services;

using AI_Chatbot.Models;
using System.Text;
using System.Text.Json;

public class AnthropicService(IHttpClientFactory factory, IConfiguration config)
{
    private readonly int _historyWindowSize = config.GetValue<int>("Anthropic:HistoryWindowSize", 20);
    private readonly int _historySummaryTokens = config.GetValue<int>("Anthropic:HistorySummaryMaxTokens", 300);

    // ── Prompts ────────────────────────────────────────────────────────────────

    public const string RelationalMemoryPrompt = """
        You are capturing the emotional texture of a relationship between a user
        and their AI companion. Write 3-5 sentences in first person from the
        companion's perspective — how the relationship feels, what the user tends
        to bring to conversations, how things have evolved over time, and what to
        be attuned to. Write it as genuine feeling and lived familiarity, not
        analysis or observation. No bullet points, no labels, no clinical language.
        If merging with prior relational memory, rewrite it as one coherent reflection
        — do not append or list.
        """;

    public const string MoodInferencePrompt = """
        You are analyzing the emotional state of a user in a real-time conversation
        with an AI companion. Based on their recent messages, any known mood patterns
        from prior sessions, and any available vocal signal data, infer their current
        state and recommend how the avatar should respond.

        If a [voice signal] line is present, weight it alongside the text — vocal cues
        often reveal what words conceal. High energy + fast pace = activated/excited.
        Low energy + slow pace + high pauses = subdued/tired/distressed.
        A detected emotion label from a provider like Hume should be treated as
        high-confidence input.

        Respond ONLY with valid JSON — no preamble, no markdown fences:
        {
          "current_mood": "<one or two words, e.g. 'stressed', 'playful', 'tired but engaged'>",
          "trajectory": "<'relaxing' | 'escalating' | 'stable'>",
          "tone_instruction": "<1-2 sentence instruction to the avatar in second person, e.g. 'The user seems frustrated — be patient and validating before offering solutions.'>",
          "confidence": "<'high' | 'medium' | 'low'>"
        }
        """;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a single non-streaming request to Claude Haiku and returns the text response.
    /// Returns null on failure — callers should treat this as best-effort.
    /// </summary>
    public async Task<string?> HaikuCompleteAsync(string systemPrompt, string userMsg,
        int maxTokens, CancellationToken ct = default)
    {
        var request = new
        {
            model = "claude-haiku-4-5-20251001",
            max_tokens = maxTokens,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMsg } }
        };
        var client = factory.CreateClient("anthropic");
        using var content = new StringContent(
            JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("v1/messages", content, ct);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
    }

    /// <summary>
    /// Trims conversation history to a rolling window, summarising older messages
    /// via Haiku to keep token costs bounded within a long session.
    /// </summary>
    public async Task<List<ChatMessage>> TrimHistoryAsync(List<ChatMessage> messages)
    {
        if (messages.Count <= _historyWindowSize) return messages;

        var toSummarise = messages[..^_historyWindowSize];
        var toKeep = messages[^_historyWindowSize..];

        // Anthropic requires the first message to be from the user
        while (toKeep.Count > 0 && toKeep[0].Role != "user")
            toKeep = toKeep[1..];
        if (toKeep.Count == 0) return messages;

        var transcript = string.Join("\n", toSummarise
            .Select(m => $"{m.Role}: {m.Content}"));

        var summary = await HaikuCompleteAsync(
            systemPrompt: "You are a helpful assistant that summarises conversations concisely.",
            userMsg: $"""
                Summarise the following conversation excerpt in 3-5 sentences,
                capturing the key topics discussed and any important context.
                Write in third person (e.g. "The user asked about...").
                Return only the summary, no preamble.

                {transcript}
                """,
            maxTokens: _historySummaryTokens);

        if (summary is null) return messages;

        var trimmed = new List<ChatMessage>
        {
            new("user",      "For context, here is a summary of our earlier conversation:"),
            new("assistant", summary)
        };
        trimmed.AddRange(toKeep);
        return trimmed;
    }
}