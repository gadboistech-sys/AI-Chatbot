namespace AI_Chatbot.Endpoints;

using AI_Chatbot.Models;
using AI_Chatbot.Services;
using System.Text.Json;

public static class AvatarEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/avatar/list", async (LiveAvatarService liveAvatar) =>
        {
            var avatars = await liveAvatar.GetPublicAvatarsAsync();
            return Results.Content(
                JsonSerializer.Serialize(new { data = new { results = avatars } }),
                "application/json");
        }).RequireAuthorization();

        app.MapPost("/avatar/session", async (NewSessionRequest req, LiveAvatarService liveAvatar) =>
        {
            var body = new
            {
                avatar_id = req.AvatarId ?? liveAvatar.DefaultAvatarId,
                mode = "LITE",
                is_sandbox = req.IsSandbox ?? false
            };
            using var doc = await liveAvatar.PostAsync("v1/sessions/token", body);
            return Results.Content(doc.RootElement.GetRawText(), "application/json");
        }).RequireAuthorization();

        app.MapPost("/avatar/start", async (StartRequest req, LiveAvatarService liveAvatar) =>
        {
            using var doc = await liveAvatar.PostAsync("v1/sessions/start", new { }, req.SessionToken);
            return Results.Content(doc.RootElement.GetRawText(), "application/json");
        }).RequireAuthorization();

        app.MapPost("/avatar/stop", async (StartRequest req, LiveAvatarService liveAvatar) =>
        {
            try { await liveAvatar.PostAsync("v1/sessions/stop", new { }, req.SessionToken); }
            catch { /* best-effort */ }
            return Results.Ok();
        }).RequireAuthorization();
    }
}