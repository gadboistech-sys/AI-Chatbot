using AI_Chatbot;
using AI_Chatbot.Endpoints;
using AI_Chatbot.Services;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;

// Prevent ASP.NET Core from remapping standard JWT claim names
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// ─── Azure Key Vault ───────────────────────────────────────────────────────
// Uses DefaultAzureCredential:
//   Local dev:   az login session or Visual Studio login
//   Production:  Managed Identity (no credentials needed)
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}
else
{
    Console.WriteLine("KeyVault:Uri not configured — falling back to local config/User Secrets.");
}

// ─── EF Core + Identity ────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentityCore<IdentityUser>(o =>
{
    o.Password.RequireDigit = true;
    o.Password.RequiredLength = 8;
    o.Password.RequireUppercase = false;
    o.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<AppDbContext>();

// ─── JWT authentication ────────────────────────────────────────────────────
var jwtKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = jwtKey
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ─── Named HTTP clients ────────────────────────────────────────────────────
builder.Services.AddHttpClient("anthropic", c =>
{
    c.BaseAddress = new Uri("https://api.anthropic.com/");
    c.DefaultRequestHeaders.Add("x-api-key", builder.Configuration["Anthropic:ApiKey"]!);
    c.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    c.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("text/event-stream"));
});

builder.Services.AddHttpClient("elevenlabs", c =>
{
    c.BaseAddress = new Uri("https://api.elevenlabs.io/");
    c.DefaultRequestHeaders.Add("xi-api-key", builder.Configuration["ElevenLabs:ApiKey"]!);
    c.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("audio/mpeg"));
});

builder.Services.AddHttpClient("azure-tts", c =>
{
    var region = builder.Configuration["Azure:SpeechRegion"]!;
    c.BaseAddress = new Uri($"https://{region}.tts.speech.microsoft.com/");
    c.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key",
        builder.Configuration["Azure:SpeechApiKey"]!);
    c.DefaultRequestHeaders.Add("User-Agent", "AvatarChatbot/1.0");
});

builder.Services.AddHttpClient("liveavatar", c =>
{
    c.BaseAddress = new Uri("https://api.liveavatar.com/");
    c.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

// ─── Application services ──────────────────────────────────────────────────
builder.Services.AddSingleton<SessionMoodStore>();
builder.Services.AddScoped<AnthropicService>();
builder.Services.AddScoped<TtsService>();
builder.Services.AddScoped<LiveAvatarService>();

// Voice sentiment provider — swap to HumeSignalProvider when ready:
//   1. Set "VoiceSentiment:Provider": "Hume" in appsettings.json
//   2. Implement HumeSignalProvider and register it below instead
var voiceProvider = builder.Configuration["VoiceSentiment:Provider"] ?? "Browser";
if (voiceProvider.Equals("Hume", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<IVoiceSentimentProvider, HumeSignalProvider>();
else
    builder.Services.AddScoped<IVoiceSentimentProvider, BrowserSignalProvider>();

// ─── Build ─────────────────────────────────────────────────────────────────
var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();

// ─── Run EF migrations on startup ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ─── Ensure memories + preferences tables exist ────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.ExecuteSqlRawAsync("""
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'memories')
        CREATE TABLE memories (
            user_id           NVARCHAR(128) NOT NULL,
            avatar_id         NVARCHAR(128) NOT NULL,
            memory            NVARCHAR(MAX) NOT NULL,
            relational_memory NVARCHAR(MAX) NULL,
            mood_seed         NVARCHAR(500) NULL,
            updated_at        DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
            CONSTRAINT PK_memories PRIMARY KEY (user_id, avatar_id)
        );

        IF NOT EXISTS (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID('memories') AND name = 'relational_memory'
        )
        ALTER TABLE memories ADD relational_memory NVARCHAR(MAX) NULL;

        IF NOT EXISTS (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID('memories') AND name = 'mood_seed'
        )
        ALTER TABLE memories ADD mood_seed NVARCHAR(500) NULL;

        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'user_preferences')
        CREATE TABLE user_preferences (
            user_id   NVARCHAR(128) PRIMARY KEY,
            avatar_id NVARCHAR(128) NULL
        );

        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'user_personas')
        CREATE TABLE user_personas (
            user_id      NVARCHAR(128) NOT NULL,
            avatar_id    NVARCHAR(128) NOT NULL,
            persona_json NVARCHAR(MAX) NOT NULL,
            updated_at   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
            CONSTRAINT PK_user_personas PRIMARY KEY (user_id, avatar_id)
        );
        """);
}

// ─── Map endpoints ─────────────────────────────────────────────────────────
AuthEndpoints.Map(app, app.Configuration);
ChatEndpoints.Map(app, app.Configuration);
MemoryEndpoints.Map(app, app.Configuration);
AvatarEndpoints.Map(app);
TtsEndpoints.Map(app);
PreferencesEndpoints.Map(app, app.Configuration);
PersonaEndpoints.Map(app, app.Configuration);
ProfileEndpoints.Map(app, app.Configuration);

app.Run();