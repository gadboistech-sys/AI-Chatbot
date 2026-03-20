namespace AI_Chatbot.Endpoints;

using AI_Chatbot.Models;
using AI_Chatbot.Models.Entities;
using AI_Chatbot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

public static class ChatEndpoints
{
    private const string OpeningMessagePrompt = """
        Based on everything you know about this person, the time since you last spoke,
        and how the last session ended, generate ONE short, natural opening line —
        the kind of thing a real person might say when reconnecting with someone they
        genuinely like. It should feel warm and personal, acknowledging the person
        before diving into any topic. Think of it as the moment of recognition when
        someone walks in — a warm hello before anything else.
        If you know this person's name, use it naturally in the opening line.
        The line can reference how long it's been, how the last session felt, or simply
        express that it's good to see them again — but it must feel genuine and unforced,
        not formulaic. Avoid generic openers like "Great to see you!" on their own.
        Do not ask a question. Do not jump straight to a topic. One warm human moment.
        Do not begin with filler words such as "Well,", "So,", "Hmm,", "Actually,",
        "Look,", or "Right,". Start directly with what you want to say.
        Return only the line itself — no quotes, no preamble.
        """;

    public static void Map(WebApplication app, IConfiguration config)
    {
        var chatMaxTokens = config.GetValue<int>("Anthropic:ChatMaxTokens", 768);
        var openingLineMaxTokens = config.GetValue<int>("Anthropic:OpeningLineMaxTokens", 40);

        // ── POST /chat/opening ─────────────────────────────────────────────────
        // Called by the frontend on the first message of a returning session.
        // Returns the opening line synchronously so the frontend can speak it
        // and await completion before calling /chat for the main response.
        app.MapPost("/chat/opening", async (
            ChatRequest req,
            HttpContext ctx,
            AppDbContext db,
            SessionMoodStore moodStore,
            AnthropicService anthropic) =>
        {
            var userId = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            var sessionKey = $"{userId}:{req.AvatarId ?? "default"}";
            var moodState = moodStore.Get(sessionKey);

            var sessionContext = await LoadSessionContextAsync(db, userId,
                req.AvatarId ?? "default");

            // Only generate an opening if this is the first message of a returning session
            if (req.Messages.Count != 1 || !sessionContext.LastSessionAt.HasValue)
                return Results.Ok(new { openingLine = (string?)null });

            // Minimal system prompt for opening line — full persona/mood/length
            // instructions are unnecessary for a one-line greeting and add latency
            var avatarName = req.SystemPrompt?.Split('\n').FirstOrDefault()?.Replace("You are ", "").Split(',').FirstOrDefault()?.Trim() ?? "the avatar";
            var gap = DateTime.UtcNow - sessionContext.LastSessionAt.Value;
            var gapDesc = gap.TotalDays < 1 ? "earlier today"
                            : gap.TotalDays < 2 ? "yesterday"
                            : gap.TotalDays < 7 ? $"{(int)gap.TotalDays} days ago"
                            : gap.TotalDays < 30 ? $"about {(int)(gap.TotalDays / 7)} weeks ago"
                            : $"about {(int)(gap.TotalDays / 30)} months ago";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"You are {avatarName}, a warm AI companion reconnecting with someone you know well.");
            if (!string.IsNullOrWhiteSpace(req.DisplayName))
                sb.AppendLine($"Their name is {req.DisplayName}.");
            if (!string.IsNullOrWhiteSpace(req.FactualMemory))
            {
                sb.AppendLine("What you know about this person:");
                sb.AppendLine(req.FactualMemory);
            }
            if (!string.IsNullOrWhiteSpace(req.RelationalMemory))
            {
                sb.AppendLine("How the relationship feels:");
                sb.AppendLine(req.RelationalMemory);
            }
            if (sessionContext.MoodSeed != null)
                sb.AppendLine($"How the last session ended: {sessionContext.MoodSeed}");
            sb.AppendLine($"You last spoke {gapDesc}. The time is {DateTime.Now:h:mm tt}.");

            var openingSystemPrompt = sb.ToString();

            var openingLine = await anthropic.HaikuCompleteAsync(
                openingSystemPrompt, OpeningMessagePrompt, maxTokens: openingLineMaxTokens);

            return Results.Ok(new { openingLine });
        }).RequireAuthorization();

        // ── POST /chat ─────────────────────────────────────────────────────────
        app.MapPost("/chat", async (
            ChatRequest req,
            IHttpClientFactory factory,
            HttpContext ctx,
            AppDbContext db,
            SessionMoodStore moodStore,
            AnthropicService anthropic,
            IVoiceSentimentProvider voiceSentiment) =>
        {
            ctx.Response.HttpContext.Features
                .Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()
                ?.DisableBuffering();
            ctx.Request.HttpContext.Response.Headers.Remove("Connection");
            ctx.Response.ContentType = "text/event-stream; charset=utf-8";
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("X-Accel-Buffering", "no");

            var userId = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            var sessionKey = $"{userId}:{req.AvatarId ?? "default"}";

            // ── Process voice signal ───────────────────────────────────────────
            var voiceSignal = await voiceSentiment.ProcessAsync(req.VoiceSignal);

            // ── Seed mood store on first message of a new session ──────────────
            var moodState = moodStore.Get(sessionKey);
            SessionContext sessionContext;

            if (req.Messages.Count == 1 && moodState.MessagesSinceUpdate == 0)
            {
                sessionContext = await LoadSessionContextAsync(db, userId,
                    req.AvatarId ?? "default");

                if (sessionContext.MoodSeed != null)
                {
                    var seedInstruction =
                        $"You are resuming a conversation. The last session {sessionContext.MoodSeed}. " +
                        $"Let that carry forward naturally into how you begin — don't announce it, " +
                        $"just let it color your opening tone.";

                    moodStore.Set(sessionKey, MoodState.Neutral with
                    {
                        ToneInstruction = seedInstruction
                    });
                    moodState = moodStore.Get(sessionKey);
                }

                ctx.Items["SessionContext"] = sessionContext;
            }

            sessionContext = ctx.Items.TryGetValue("SessionContext", out var sc)
                ? (SessionContext)sc!
                : await LoadSessionContextAsync(db, userId, req.AvatarId ?? "default");

            var safeMessages = req.Messages
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .ToList();

            if (safeMessages.Count == 0)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("No non-empty messages provided.");
                return;
            }

            var trimmedMessages = await anthropic.TrimHistoryAsync(safeMessages);
            var userMessageText = safeMessages.LastOrDefault(m => m.Role == "user")?.Content;
            var systemPrompt = SystemPromptBuilder.Build(
                req.SystemPrompt,
                req.FactualMemory,
                req.RelationalMemory,
                moodState.ToneInstruction,
                sessionContext,
                userMessageText,
                req.DisplayName);

            // If an opening line was already spoken, tell Sonnet not to re-greet
            if (!string.IsNullOrWhiteSpace(req.OpeningLine))
            {
                systemPrompt +=
                    $"\n\nYou have already greeted this person with: \"{req.OpeningLine}\". " +
                    "Do not greet them again. Respond directly to what they said.";
            }

            var body = new
            {
                model = "claude-sonnet-4-6",
                max_tokens = chatMaxTokens,
                stream = true,
                system = systemPrompt,
                messages = trimmedMessages
            };

            var client = factory.CreateClient("anthropic");
            using var content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("v1/messages", content);

            if (!response.IsSuccessStatusCode)
            {
                var errText = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Anthropic error: {errText}");
                ctx.Response.StatusCode = (int)response.StatusCode;
                await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize("⚠ " + errText)}\n\n");
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null &&
                   !ctx.RequestAborted.IsCancellationRequested)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;
                var data = line["data: ".Length..];
                if (data == "[DONE]") break;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("type", out var type)) continue;
                    var typeStr = type.GetString();

                    if (typeStr == "content_block_delta" &&
                        root.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("text", out var text))
                    {
                        var chunk = text.GetString() ?? "";
                        await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(chunk)}\n\n");
                        await ctx.Response.Body.FlushAsync();
                    }
                    else if (typeStr == "message_delta" &&
                        root.TryGetProperty("delta", out var msgDelta) &&
                        msgDelta.TryGetProperty("stop_reason", out var stopReason) &&
                        stopReason.GetString() == "max_tokens")
                    {
                        Console.WriteLine("Warning: response truncated by max_tokens limit.");
                        await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize("…")}\n\n");
                        await ctx.Response.Body.FlushAsync();
                    }
                }
                catch { }
            }

            await ctx.Response.WriteAsync("data: [DONE]\n\n");
            await ctx.Response.Body.FlushAsync();

            // ── Background mood inference (fire-and-forget, every 5 messages) ──
            const int checkEveryN = 5;
            const int escalationThreshold = 2;
            var newCount = moodState.MessagesSinceUpdate + 1;
            if (newCount >= checkEveryN)
            {
                moodStore.Set(sessionKey, moodState with { MessagesSinceUpdate = 0 });
                var recentMessages = safeMessages.TakeLast(10).ToList();
                var relationalMemory = req.RelationalMemory;
                var lastUserMessage = safeMessages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
                var capturedSignal = voiceSignal;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var transcript = string.Join("\n",
                            recentMessages.Select(m => $"{m.Role}: {m.Content}"));

                        var signalHint = capturedSignal?.ToPromptHint() ?? string.Empty;
                        var signalLine = !string.IsNullOrEmpty(signalHint)
                            ? $"\n\nLatest voice signal: {signalHint}"
                            : string.Empty;

                        var userMsg = string.IsNullOrWhiteSpace(relationalMemory)
                            ? $"Recent messages:\n{transcript}{signalLine}"
                            : $"Known mood patterns from prior sessions:\n{relationalMemory}\n\n" +
                              $"Recent messages:\n{transcript}{signalLine}";

                        var json = await anthropic.HaikuCompleteAsync(
                            AnthropicService.MoodInferencePrompt, userMsg, 200);
                        if (json is null) return;

                        var inferred = JsonSerializer.Deserialize<InferredMood>(json);
                        if (inferred is null || inferred.Confidence == "low") return;

                        var current = moodStore.Get(sessionKey);
                        var consecutive = inferred.Trajectory == "escalating"
                            ? current.ConsecutiveEscalations + 1
                            : 0;

                        if (consecutive == escalationThreshold)
                            moodStore.LogEscalation(sessionKey, lastUserMessage);

                        moodStore.Set(sessionKey, new MoodState(
                            inferred.CurrentMood,
                            inferred.ToneInstruction,
                            inferred.Trajectory,
                            MessagesSinceUpdate: 0,
                            ConsecutiveEscalations: consecutive));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Mood inference error (non-fatal): {ex.Message}");
                    }
                });
            }
            else
            {
                moodStore.Set(sessionKey, moodState with { MessagesSinceUpdate = newCount });
            }
        }).RequireAuthorization();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<SessionContext> LoadSessionContextAsync(
        AppDbContext db, string userId, string avatarId)
    {
        try
        {
            var entry = await db.Memories
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.UserId == userId && m.AvatarId == avatarId);

            if (entry is not null)
                return new SessionContext(
                    LastSessionAt: entry.UpdatedAt,
                    MoodSeed: entry.MoodSeed);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadSessionContext error (non-fatal): {ex.Message}");
        }
        return new SessionContext(LastSessionAt: null, MoodSeed: null);
    }
}