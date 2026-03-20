namespace AI_Chatbot.Endpoints;

using AI_Chatbot.Models;
using AI_Chatbot.Models.Entities;
using AI_Chatbot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

public static class PersonaEndpoints
{
    public static void Map(WebApplication app, IConfiguration config)
    {
        // ── GET /persona?avatarId=... ──────────────────────────────────────────
        app.MapGet("/persona", async (string avatarId, HttpContext ctx, AppDbContext db) =>
        {
            var userId = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            var entity = await db.UserPersonas
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId && p.AvatarId == avatarId);

            if (entity is null)
                return Results.Ok(new { definition = (PersonaDefinition?)null, compiled = (string?)null });

            var definition = JsonSerializer.Deserialize<PersonaDefinition>(entity.PersonaJson);
            var compiled = definition is not null ? PersonaCompiler.Compile(definition) : null;
            return Results.Ok(new { definition, compiled });
        }).RequireAuthorization();

        // ── POST /persona ──────────────────────────────────────────────────────
        app.MapPost("/persona", async (
            PersonaSaveRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var userId = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            var json = JsonSerializer.Serialize(req.Definition);
            var compiled = PersonaCompiler.Compile(req.Definition);

            var entity = await db.UserPersonas
                .FirstOrDefaultAsync(p => p.UserId == userId && p.AvatarId == req.AvatarId);

            if (entity is null)
                db.UserPersonas.Add(new UserPersona
                {
                    UserId = userId,
                    AvatarId = req.AvatarId,
                    PersonaJson = json,
                    UpdatedAt = DateTime.UtcNow
                });
            else
            {
                entity.PersonaJson = json;
                entity.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { compiled });
        }).RequireAuthorization();
    }
}

public record PersonaSaveRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("avatarId")] string AvatarId,
    [property: System.Text.Json.Serialization.JsonPropertyName("definition")] PersonaDefinition Definition);