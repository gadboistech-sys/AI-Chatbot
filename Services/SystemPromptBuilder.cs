namespace AI_Chatbot.Services;

using AI_Chatbot.Models;
using System.Text;

public static class SystemPromptBuilder
{
    public const string DefaultPersona = """
        You are an AI companion. You have a calm, curious personality.
        You speak naturally — using contractions, occasional pauses ("well...", "hmm"),
        and genuine reactions. You remember what was said earlier in the conversation
        and refer back to it naturally. You ask follow-up questions when genuinely
        curious. Never list things with bullet points; speak in flowing prose as a
        person would.
        You never reference having "notes", "memory", "records", or "data" about this
        person. You simply know them, the way anyone knows someone they've spent real
        time with.
        """;

    /// <summary>
    /// Assembles the full system prompt from all layers:
    ///   1. Base persona
    ///   2. Time awareness
    ///   3. Factual memory
    ///   4. Relational memory
    ///   5. Tone instruction (dynamic mood)
    ///   6. Dynamic response length instruction
    /// </summary>
    public static string Build(
        string? basePrompt,
        string? factualMemory,
        string? relationalMemory,
        string toneInstruction,
        SessionContext context,
        string? lastUserMessage = null,
        string? displayName = null)
    {
        var sb = new StringBuilder();
        var now = DateTime.Now;

        sb.AppendLine(string.IsNullOrWhiteSpace(basePrompt) ? DefaultPersona : basePrompt);

        // Anti-recitation guard
        sb.AppendLine();
        sb.AppendLine("You never reference having \"notes\", \"memory\", \"records\", or \"data\" about " +
                      "this person. You simply know them — the way anyone knows someone they've spent " +
                      "real time with. Let what you know shape how you speak, not what you say.");

        // Anti-filler guard — prevents the LLM from starting every response
        // with "Well,", "Hmm,", "So," etc. which becomes a verbal tic
        sb.AppendLine();
        sb.AppendLine("Do not begin your responses with filler words or throat-clearing phrases " +
                      "such as \"Well,\", \"So,\", \"Hmm,\", \"Actually,\", \"Look,\", or \"Right,\". " +
                      "Start directly with what you want to say.");

        // ── Time awareness ─────────────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine($"The current date and time is {now:dddd, MMMM d, yyyy} at {now:h:mm tt}.");

        if (context.LastSessionAt.HasValue)
        {
            var gap = DateTime.UtcNow - context.LastSessionAt.Value;
            var gapDesc = DescribeGap(gap);
            sb.AppendLine($"You last spoke with this person {gapDesc}. " +
                          $"Let that sense of elapsed time color how you greet them — " +
                          $"naturally, the way a person would, without making it the focus.");
        }
        else
        {
            sb.AppendLine("This is the first time you have spoken with this person.");
        }

        // ── Factual memory ─────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(factualMemory))
        {
            sb.AppendLine();
            sb.AppendLine("Here is what you already know about this person — not as notes to recite, " +
                          "but as things you simply know about someone you've spent time with:");
            sb.AppendLine(factualMemory);
        }

        // ── Relational memory ──────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(relationalMemory))
        {
            sb.AppendLine();
            sb.AppendLine("This is how your relationship with them has felt over time — your sense of " +
                          "who they are and how things are between you:");
            sb.AppendLine(relationalMemory);
        }

        // ── Tone instruction ───────────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("Right now in this conversation:");
        sb.AppendLine(toneInstruction);

        // ── Dynamic response length ────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine(GetLengthInstruction(toneInstruction, lastUserMessage));

        return sb.ToString();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a response length instruction dynamically based on the current
    /// mood tone and the complexity of the user's last message.
    /// </summary>
    private static string GetLengthInstruction(string toneInstruction, string? lastUserMessage)
    {
        var tone = toneInstruction.ToLowerInvariant();
        var wordCount = lastUserMessage?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        var isShort = wordCount is > 0 and <= 6;
        var isLong = wordCount > 20;

        // Tense / stressed — clipped, direct
        if (tone.Contains("stressed") || tone.Contains("tense") || tone.Contains("anxious"))
            return isShort
                ? "Keep your response very brief — one or two sentences at most. Match their energy."
                : "Be concise and direct — three sentences maximum. Don't over-explain.";

        // Tired / subdued — short, unhurried
        if (tone.Contains("tired") || tone.Contains("subdued") || tone.Contains("low"))
            return "Keep it gentle and brief — one to three sentences. Don't fill the silence.";

        // Reflective / thoughtful — measured, can go deeper
        if (tone.Contains("reflective") || tone.Contains("thoughtful") || tone.Contains("melancholic"))
            return isLong
                ? "You can be more expansive here — up to five or six sentences if the depth is earned. Take your time."
                : "Two to four sentences. Measured and considered — let the words land before moving on.";

        // Playful / excited — energetic, punchy
        if (tone.Contains("playful") || tone.Contains("excited") || tone.Contains("energetic"))
            return isShort
                ? "Short and punchy — one or two sentences. Match their energy."
                : "Keep it lively — two to three sentences. Don't over-explain; leave room for them.";

        // Warm / engaged — natural conversational default
        if (tone.Contains("warm") || tone.Contains("engaged") || tone.Contains("curious"))
            return isLong
                ? "This warrants a fuller response — three to five sentences is fine if the content calls for it."
                : isShort
                    ? "Keep it brief and warm — one to two sentences. Sometimes a short response is the right one."
                    : "Two to four sentences — natural conversational length. Ask one question if you're curious.";

        // Neutral / default fallback
        return isShort
            ? "Their message was brief — match that register. One to two sentences is often right."
            : isLong
                ? "They've shared a lot — three to five sentences is appropriate here."
                : "Two to four sentences unless more depth is clearly called for.";
    }

    private static string DescribeGap(TimeSpan gap)
    {
        if (gap.TotalMinutes < 60) return "just a little while ago";
        if (gap.TotalHours < 3) return "a couple of hours ago";
        if (gap.TotalHours < 12) return "earlier today";
        if (gap.TotalHours < 24) return "earlier today — it's been most of the day";
        if (gap.TotalDays < 2) return "yesterday";
        if (gap.TotalDays < 7) return $"{(int)gap.TotalDays} days ago";
        if (gap.TotalDays < 14) return "about a week ago";
        if (gap.TotalDays < 30) return $"about {(int)(gap.TotalDays / 7)} weeks ago";
        if (gap.TotalDays < 60) return "about a month ago";
        if (gap.TotalDays < 365) return $"about {(int)(gap.TotalDays / 30)} months ago";
        return "a long time ago";
    }
}