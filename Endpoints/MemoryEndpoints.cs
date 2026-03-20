namespace AI_Chatbot.Endpoints;

using AI_Chatbot.Models;
using AI_Chatbot.Models.Entities;
using AI_Chatbot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public static class MemoryEndpoints
{
    private const string MoodSeedPrompt = """
        In one sentence, describe the emotional note this conversation ended on,
        written from the AI companion's first-person perspective.
        Focus on the felt quality — not facts discussed, but how things felt at the close.
        Example: "ended in a warm, easy mood — we were laughing about something small and it felt light."
        Example: "closed on a quieter, more reflective note after they mentioned feeling overwhelmed at work."
        Return only the sentence. No preamble, no quotes.
        """;

    public static void Map(WebApplication app, IConfiguration config)
    {
        var memorySummaryTokens = config.GetValue<int>("Anthropic:MemorySummaryMaxTokens", 600);

        // ── GET /memory ────────────────────────────────────────────────────────
        app.MapGet("/memory", async (string avatarId, HttpContext ctx, AppDbContext db) =>
        {
            var userId = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            var entry = await db.Memories
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.UserId == userId && m.AvatarId == avatarId);

            if (entry is null)
                return Results.Ok(new
                {
                    factual = (string?)null,
                    relational = (string?)null,
                    moodSeed = (string?)null,
                    lastSessionAt = (DateTime?)null
                });

            return Results.Ok(new
            {
                factual = string.IsNullOrEmpty(entry.Memory) ? null : entry.Memory,
                relational = entry.RelationalMemory,
                moodSeed = entry.MoodSeed,
                lastSessionAt = (DateTime?)entry.UpdatedAt
            });
        }).RequireAuthorization();

        // ── POST /memory ───────────────────────────────────────────────────────
        app.MapPost("/memory", async (
            MemorySaveRequest req, AnthropicService anthropic,
            SessionMoodStore moodStore, AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            var avId = req.AvatarId ?? "default";
            var sessionKey = $"{userId}:{avId}";

            if (req.Messages.Count < 2) return Results.Ok();

            var transcript = string.Join("\n", req.Messages
                .Select(m => $"{m.Role}: {m.Content}"));

            // Load existing memory
            var existing = await db.Memories
                .FirstOrDefaultAsync(m => m.UserId == userId && m.AvatarId == avId);

            var existingFactual = existing?.Memory;
            var existingRelational = existing?.RelationalMemory;

            // ── Escalation events ─────────────────────────────────────────────
            var sessionEvents = moodStore.GetEvents(sessionKey);
            var eventNotes = sessionEvents.Count > 0
                ? "\n\nNotable emotional events this session:\n" +
                  string.Join("\n", sessionEvents.Select(e => $"- {e}"))
                : "";

            // ── Build prompts ─────────────────────────────────────────────────
            var existingSection = existingFactual != null
                ? $"\n\nExisting memory from previous sessions:\n{existingFactual}\n\n" +
                  "Merge the above with anything new from the conversation below."
                : "";

            var factualUserMsg = $"""
                Here is a conversation between a user and an AI companion named {req.AvatarName}.
                Extract a concise memory of 10-20 bullet points covering:
                - Key facts the user shared about themselves
                - Topics they showed interest in
                - Their communication style or preferences
                - Anything the avatar should remember for next time

                Return only the bullet points, no preamble. Drop any points that are
                no longer relevant or contradicted by new information.{existingSection}

                Latest conversation:
                {transcript}
                """;

            var relationalUserMsg = string.IsNullOrEmpty(existingRelational)
                ? $"Conversation:\n{transcript}{eventNotes}"
                : $"Prior relational memory:\n{existingRelational}\n\n" +
                  $"New conversation:\n{transcript}{eventNotes}";

            var tailTranscript = string.Join("\n", req.Messages
                .TakeLast(10)
                .Select(m => $"{m.Role}: {m.Content}"));

            // ── Run all three Haiku calls in parallel ─────────────────────────
            var factualTask = anthropic.HaikuCompleteAsync(
                "You are a memory extraction assistant. Follow the instructions in the user message exactly.",
                factualUserMsg, memorySummaryTokens);

            var relationalTask = anthropic.HaikuCompleteAsync(
                AnthropicService.RelationalMemoryPrompt,
                relationalUserMsg, memorySummaryTokens);

            var moodSeedTask = anthropic.HaikuCompleteAsync(
                MoodSeedPrompt,
                $"Conversation ending:\n{tailTranscript}", 80);

            await Task.WhenAll(factualTask, relationalTask, moodSeedTask);

            var newFactual = factualTask.Result ?? existingFactual ?? "";
            var newRelational = relationalTask.Result ?? existingRelational ?? "";
            var newMoodSeed = moodSeedTask.Result;

            // ── Upsert ────────────────────────────────────────────────────────
            if (existing is null)
            {
                db.Memories.Add(new MemoryEntry
                {
                    UserId = userId,
                    AvatarId = avId,
                    Memory = newFactual,
                    RelationalMemory = newRelational,
                    MoodSeed = newMoodSeed,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Memory = newFactual;
                existing.RelationalMemory = newRelational;
                existing.MoodSeed = newMoodSeed;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            moodStore.ClearSession(sessionKey);

            Console.WriteLine($"Memory saved for {userId} / {avId}" +
                (sessionEvents.Count > 0 ? $" ({sessionEvents.Count} escalation event(s))" : "") +
                (newMoodSeed != null ? $" | mood seed: \"{newMoodSeed}\"" : ""));

            return Results.Ok(new { factual = newFactual, relational = newRelational });
        }).RequireAuthorization();
    }
}