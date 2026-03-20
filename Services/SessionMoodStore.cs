namespace AI_Chatbot.Services;

using AI_Chatbot.Models;
using System.Collections.Concurrent;

/// <summary>
/// Singleton in-memory store for per-session mood state and escalation events.
/// Key: "{userId}:{avatarId}" — naturally cleared on server restart.
/// </summary>
public class SessionMoodStore
{
    private readonly ConcurrentDictionary<string, MoodState> _moods = new();
    private readonly ConcurrentDictionary<string, List<string>> _events = new();

    // ── Mood state ─────────────────────────────────────────────────────────────

    public MoodState Get(string key) =>
        _moods.GetValueOrDefault(key, MoodState.Neutral);

    public void Set(string key, MoodState mood) =>
        _moods[key] = mood;

    // ── Escalation events ──────────────────────────────────────────────────────

    /// <summary>
    /// Logs a notable escalation event for the session.
    /// Called when ConsecutiveEscalations reaches the threshold.
    /// </summary>
    public void LogEscalation(string key, string lastUserMessage)
    {
        var events = _events.GetOrAdd(key, _ => new List<string>());
        var excerpt = lastUserMessage.Length > 120
            ? lastUserMessage[..120] + "…"
            : lastUserMessage;
        lock (events)
        {
            events.Add($"Emotional tension escalated — user had just said: \"{excerpt}\"");
        }
    }

    /// <summary>
    /// Returns all escalation events logged for the session, in order.
    /// </summary>
    public IReadOnlyList<string> GetEvents(string key) =>
        _events.TryGetValue(key, out var e) ? e.AsReadOnly() : Array.Empty<string>();

    // ── Cleanup ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears both mood state and escalation events for the session.
    /// Call after memory has been saved on disconnect.
    /// </summary>
    public void ClearSession(string key)
    {
        _moods.TryRemove(key, out _);
        _events.TryRemove(key, out _);
    }
}