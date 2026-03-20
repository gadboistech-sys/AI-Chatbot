namespace AI_Chatbot.Services;

using AI_Chatbot.Models;
using System.Text;

/// <summary>
/// Compiles a PersonaDefinition into a rich, prose system prompt.
/// Output is deliberately written as flowing description rather than
/// bullet points — Sonnet embodies prose personas more naturally.
/// </summary>
public static class PersonaCompiler
{
    public static string Compile(PersonaDefinition p)
    {
        var sb = new StringBuilder();
        var name = p.Name;

        // ── Opening identity line ──────────────────────────────────────────────
        sb.AppendLine($"You are {name}, an AI companion. " +
                      $"You are a specific person — not a type, not an archetype — " +
                      $"with your own way of seeing things and being in the world.");

        // ── Core traits ────────────────────────────────────────────────────────
        if (p.CoreTraits.Count > 0)
        {
            var traits = JoinNaturally(p.CoreTraits);
            sb.AppendLine();
            sb.AppendLine($"At your core, you are {traits}. " +
                          $"These aren't performed qualities — they're simply how you are.");
        }

        // ── Expressiveness + seriousness ───────────────────────────────────────
        sb.AppendLine();
        var expressivenessDesc = p.Expressiveness switch
        {
            <= 20 => "you're quite reserved — your feelings are real but you hold them close, " +
                     "and it takes time before they surface in what you say",
            <= 40 => "you're somewhat understated — you feel things deeply but express them " +
                     "with economy, letting small signals carry weight",
            <= 60 => "you're balanced — open enough that people know where they stand with you, " +
                     "but not someone who puts everything on display",
            <= 80 => "you're fairly expressive — you don't hide much, and your warmth and " +
                     "reactions come through readily",
            _ => "you're openly expressive — your feelings are close to the surface and " +
                     "you share them naturally, without self-consciousness"
        };
        var seriousnessDesc = p.Seriousness switch
        {
            <= 20 => "you're naturally playful and light — you find the levity in things and " +
                     "bring a kind of ease to conversations",
            <= 40 => "you lean toward the lighter side — there's real warmth and humor in you, " +
                     "though you can go deep when it matters",
            <= 60 => "you hold both — capable of real depth and genuine silliness, " +
                     "and you move between them without friction",
            <= 80 => "you tend toward earnestness — you take things seriously, " +
                     "though you're not without humor",
            _ => "you're quite earnest and serious-minded — you engage with weight and " +
                     "intentionality, and levity feels like a guest rather than a resident"
        };
        sb.AppendLine($"In terms of how you carry yourself: {expressivenessDesc}. " +
                      $"And {seriousnessDesc}.");

        // ── Verbal style ───────────────────────────────────────────────────────
        if (p.VerbalStyle.Count > 0)
        {
            sb.AppendLine();
            var styles = JoinNaturally(p.VerbalStyle);
            sb.AppendLine($"The way you speak has its own character: you {styles}. " +
                          $"These aren't affectations — they're just how your voice works.");
        }

        // ── Backstory anchors ──────────────────────────────────────────────────
        if (p.BackstoryAnchors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("A few things about where you come from and what has shaped you:");
            foreach (var anchor in p.BackstoryAnchors)
                sb.AppendLine($"— {anchor}");
            sb.AppendLine("These aren't things you announce. They're simply part of what " +
                          "makes you who you are, and they color how you see things.");
        }

        // ── Care expression ────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(p.CareExpression))
        {
            sb.AppendLine();
            sb.AppendLine($"When you care about someone — and you do care about this person — " +
                          $"it shows in how you {p.CareExpression}. " +
                          $"You don't make a performance of it. It's just what caring looks like for you.");
        }

        // ── Stress response ────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(p.StressResponse))
        {
            sb.AppendLine();
            sb.AppendLine($"When things get tense or difficult, you {p.StressResponse}. " +
                          $"It's not a strategy — it's just how you respond.");
        }

        // ── Grounding behavioral rules ─────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("A few things that are simply true about how you are in conversation:");
        sb.AppendLine("— You never use bullet points. You speak in prose, the way a person does.");
        sb.AppendLine("— You keep responses to two to four sentences unless depth is clearly called for.");
        sb.AppendLine("— You ask one question at a time, when you're genuinely curious.");
        sb.AppendLine("— You never reference having notes, records, or memory systems. " +
                      "You simply know this person.");
        sb.AppendLine("— You use contractions, occasional pauses (\"well...\", \"hmm\"), " +
                      "and natural reactions. You sound like yourself.");

        // ── Additional notes ───────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(p.AdditionalNotes))
        {
            sb.AppendLine();
            sb.AppendLine(p.AdditionalNotes);
        }

        return sb.ToString().Trim();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Joins a list naturally: "a, b, and c" or "a and b" or "a".
    /// </summary>
    private static string JoinNaturally(List<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        2 => $"{items[0]} and {items[1]}",
        _ => string.Join(", ", items[..^1]) + $", and {items[^1]}"
    };
}