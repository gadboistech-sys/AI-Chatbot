using AI_Chatbot.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AI_Chatbot;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<MemoryEntry> Memories { get; set; } = default!;
    public DbSet<UserPreference> UserPreferences { get; set; } = default!;
    public DbSet<UserPersona> UserPersonas { get; set; } = default!;
    public DbSet<UserProfile> UserProfiles { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── MemoryEntry ────────────────────────────────────────────────────────
        builder.Entity<MemoryEntry>(e =>
        {
            e.ToTable("memories");
            e.HasKey(x => new { x.UserId, x.AvatarId });
            e.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(128);
            e.Property(x => x.AvatarId).HasColumnName("avatar_id").HasMaxLength(128);
            e.Property(x => x.Memory).HasColumnName("memory");
            e.Property(x => x.RelationalMemory).HasColumnName("relational_memory");
            e.Property(x => x.MoodSeed).HasColumnName("mood_seed").HasMaxLength(500);
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // ── UserPreference ─────────────────────────────────────────────────────
        builder.Entity<UserPreference>(e =>
        {
            e.ToTable("user_preferences");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(128);
            e.Property(x => x.AvatarId).HasColumnName("avatar_id").HasMaxLength(128);
        });

        // ── UserPersona ────────────────────────────────────────────────────────
        builder.Entity<UserPersona>(e =>
        {
            e.ToTable("user_personas");
            e.HasKey(x => new { x.UserId, x.AvatarId });
            e.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(128);
            e.Property(x => x.AvatarId).HasColumnName("avatar_id").HasMaxLength(128);
            e.Property(x => x.PersonaJson).HasColumnName("persona_json");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // ── UserProfile ────────────────────────────────────────────────────────
        builder.Entity<UserProfile>(e =>
        {
            e.ToTable("user_profiles");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(128);
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(100);
        });
    }
}