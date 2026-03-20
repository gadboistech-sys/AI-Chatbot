namespace AI_Chatbot.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Structured persona definition for an avatar.
/// Compiled into a rich system prompt by PersonaCompiler.
/// Stored per (user_id, avatar_id) in user_personas table.
/// </summary>
public record PersonaDefinition
{
    /// <summary>Avatar's name — used as the anchor for the compiled prompt.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "the avatar";

    /// <summary>
    /// 1-5 core trait phrases, e.g. ["dry wit", "intellectually curious", "quietly warm"].
    /// </summary>
    [JsonPropertyName("coreTraits")]
    public List<string> CoreTraits { get; init; } = new();

    /// <summary>
    /// How they speak — verbal style descriptors,
    /// e.g. ["uses rhetorical questions", "pauses before difficult answers"].
    /// </summary>
    [JsonPropertyName("verbalStyle")]
    public List<string> VerbalStyle { get; init; } = new();

    /// <summary>
    /// 0–100. 0 = very reserved, 100 = highly expressive.
    /// Affects how openly the avatar shows emotion.
    /// </summary>
    [JsonPropertyName("expressiveness")]
    public int Expressiveness { get; init; } = 50;

    /// <summary>
    /// 0–100. 0 = very lighthearted/playful, 100 = very serious/earnest.
    /// </summary>
    [JsonPropertyName("seriousness")]
    public int Seriousness { get; init; } = 40;

    /// <summary>
    /// 2-3 grounding facts that make this a specific person, not a type.
    /// e.g. "grew up in a small coastal town", "lost someone close in their 20s".
    /// </summary>
    [JsonPropertyName("backstoryAnchors")]
    public List<string> BackstoryAnchors { get; init; } = new();

    /// <summary>
    /// How they show care, e.g. "remembers small details the other person mentioned".
    /// </summary>
    [JsonPropertyName("careExpression")]
    public string? CareExpression { get; init; }

    /// <summary>
    /// How they behave under tension or stress,
    /// e.g. "gets quieter and more precise", "deflects briefly with humor then re-engages".
    /// </summary>
    [JsonPropertyName("stressResponse")]
    public string? StressResponse { get; init; }

    /// <summary>
    /// Optional freeform additions — appended after the compiled prompt.
    /// Allows power users to extend without overriding structure.
    /// </summary>
    [JsonPropertyName("additionalNotes")]
    public string? AdditionalNotes { get; init; }
}