namespace AI_Chatbot.Endpoints;

using AI_Chatbot.Models.Entities;
using AI_Chatbot;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public static class AuthEndpoints
{
    public static void Map(WebApplication app, IConfiguration config)
    {
        var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var jwtIssuer = config["Jwt:Issuer"]!;
        var jwtAudience = config["Jwt:Audience"]!;
        var expiryDays = config.GetValue<int>("Jwt:ExpiryDays", 30);

        string GenerateJwt(IdentityUser user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
            };
            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(expiryDays),
                signingCredentials: new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ── POST /auth/register ────────────────────────────────────────────────
        app.MapPost("/auth/register", async (
            RegisterRequest req,
            UserManager<IdentityUser> userManager,
            AppDbContext db) =>
        {
            var user = new IdentityUser { UserName = req.Email, Email = req.Email };
            var result = await userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return Results.BadRequest(result.Errors.Select(e => e.Description));

            // Save display name if provided
            var displayName = req.DisplayName?.Trim();
            if (!string.IsNullOrEmpty(displayName))
            {
                db.UserProfiles.Add(new UserProfile
                {
                    UserId = user.Id,
                    DisplayName = displayName
                });
                await db.SaveChangesAsync();
            }

            return Results.Ok(new
            {
                token = GenerateJwt(user),
                displayName = displayName
            });
        });

        // ── POST /auth/login ───────────────────────────────────────────────────
        app.MapPost("/auth/login", async (
            AuthRequest req,
            UserManager<IdentityUser> userManager,
            AppDbContext db) =>
        {
            var user = await userManager.FindByEmailAsync(req.Email);
            if (user == null || !await userManager.CheckPasswordAsync(user, req.Password))
                return Results.Unauthorized();

            // Load display name to return to frontend
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            var displayName = profile?.DisplayName;

            return Results.Ok(new
            {
                token = GenerateJwt(user),
                displayName
            });
        });
    }
}

// ── Request records ────────────────────────────────────────────────────────────
public record AuthRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("email")] string Email,
    [property: System.Text.Json.Serialization.JsonPropertyName("password")] string Password);

public record RegisterRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("email")] string Email,
    [property: System.Text.Json.Serialization.JsonPropertyName("password")] string Password,
    [property: System.Text.Json.Serialization.JsonPropertyName("displayName")] string? DisplayName);