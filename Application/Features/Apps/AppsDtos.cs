namespace Application.Features.Apps;

/// <summary>A registered client application (the Apps integration module).</summary>
public class AppDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string? PersonaPrompt { get; set; }
    public string? JwtAuthority { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Create/update payload for a registered app.</summary>
public class SaveAppRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string? PersonaPrompt { get; set; }

    /// <summary>Optional OIDC issuer whose tokens the assistant accepts for this app.</summary>
    public string? JwtAuthority { get; set; }

    public bool IsActive { get; set; } = true;
}
