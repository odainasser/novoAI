using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// A registered client application that integrates WITH ByteAI: the app exposes a
/// tool-provider surface (GET tools / POST execute / optional branch context) and
/// ByteAI serves its users' assistant questions against those tools. Registration
/// is data, not deployment — onboarding a new system is a row in this table, never
/// a ByteAI code change. The app's own users call /api/assistant/ask with their own
/// bearer token plus the app's <see cref="Code"/>; permission enforcement always
/// happens inside the app itself at execution time.
/// </summary>
public class App : BaseAuditableEntity
{
    /// <summary>Stable slug the client sends with every assistant request (e.g. "bytemart").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name used in the assistant's persona and the admin UI (e.g. "ByteMart").</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Base URL of the app's tool-provider API (its /api/assistant-data surface).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional persona line injected into the system prompt
    /// (e.g. "retail business assistant"). Defaults to a generic assistant persona.
    /// </summary>
    public string? PersonaPrompt { get; set; }

    /// <summary>ISO/display currency the app's money values are expressed in.</summary>
    public string Currency { get; set; } = "AED";

    /// <summary>Inactive apps are refused service without being deleted.</summary>
    public bool IsActive { get; set; } = true;
}
