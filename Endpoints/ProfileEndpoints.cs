namespace AI_Chatbot.Endpoints;

using AI_Chatbot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public static class ProfileEndpoints
{
    public static void Map(WebApplication app, IConfiguration config)
    {
        // ── GET /profile ───────────────────────────────────────────────────────
        app.MapGet("/profile", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            var profile = await db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);
            return Results.Ok(new { displayName = profile?.DisplayName });
        }).RequireAuthorization();

        // ── POST /profile ──────────────────────────────────────────────────────
        // Allows updating display name after registration
        app.MapPost("/profile", async (
            UpdateProfileRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var userId = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var name = req.DisplayName?.Trim();
            if (string.IsNullOrEmpty(name))
                return Results.BadRequest("Display name cannot be empty.");

            if (profile is null)
                db.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = name });
            else
                profile.DisplayName = name;

            await db.SaveChangesAsync();
            return Results.Ok(new { displayName = name });
        }).RequireAuthorization();
    }
}

public record UpdateProfileRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("displayName")] string? DisplayName);