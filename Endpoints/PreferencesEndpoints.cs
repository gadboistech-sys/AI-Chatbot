namespace AI_Chatbot.Endpoints;

using AI_Chatbot.Models;
using AI_Chatbot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public static class PreferencesEndpoints
{
    public static void Map(WebApplication app, IConfiguration config)
    {
        app.MapGet("/preferences", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            var pref = await db.UserPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);
            return Results.Ok(new { avatarId = pref?.AvatarId });
        }).RequireAuthorization();

        app.MapPost("/preferences", async (
            PreferencesRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var userId = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            var pref = await db.UserPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (pref is null)
                db.UserPreferences.Add(new UserPreference
                {
                    UserId = userId,
                    AvatarId = req.AvatarId
                });
            else
                pref.AvatarId = req.AvatarId;

            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();
    }
}