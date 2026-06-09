namespace Application.Features.Assistant;

/// <summary>
/// One logged assistant turn as a reviewable PLAN: the question, the answer, and
/// the plan the assistant executed (tools called → derived module/domain + entities),
/// plus any reviewer-confirmed correction.
/// </summary>
public class AssistantInteractionDto
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Locale { get; set; } = "en";
    public string? Answer { get; set; }
    public bool Answered { get; set; }
    public bool IsMixing { get; set; }
    public DateTime CreatedAt { get; set; }

    // The plan the assistant executed (tools it called + derived domain/entities).
    public List<string> Tools { get; set; } = new();
    public List<string> Domains { get; set; } = new();
    public List<string> Entities { get; set; } = new();

    // The reviewer-confirmed correction, if any.
    public bool PlanConfirmed { get; set; }
    public List<string> ConfirmedTools { get; set; } = new();
    public string? ConfirmedDomain { get; set; }
    public List<string> ConfirmedEntities { get; set; } = new();
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
}

/// <summary>The code-owned vocabulary the plan-correction dropdowns read from.</summary>
public class AssistantPlanOptionsDto
{
    public List<AssistantToolInfoDto> Tools { get; set; } = new();
    public List<string> Domains { get; set; } = new();
    public List<string> Entities { get; set; } = new();
}

public class AssistantToolInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public List<string> Entities { get; set; } = new();

    /// <summary>Every parameter this tool accepts — each needs an explicit source to confirm.</summary>
    public List<ToolParamDto> Parameters { get; set; } = new();
}

public class ToolParamDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public List<string> Enum { get; set; } = new();
    public string? Description { get; set; }
    public bool Required { get; set; }
}

/// <summary>
/// A reviewer's corrected plan for a turn. Saved as a DRAFT <c>AssistantPlan</c>
/// (the start of a governed, executable plan) and recorded on the interaction.
/// </summary>
public class ConfirmAssistantPlanRequest
{
    /// <summary>The chosen tools, each with an explicit source decision for EVERY parameter.</summary>
    public List<PlanToolInput> Tools { get; set; } = new();

    public string? Domain { get; set; }
    public List<string> Entities { get; set; } = new();

    /// <summary>count | sum | list | compare | top | detail | status (defaults to "list").</summary>
    public string? Action { get; set; }

    /// <summary>Primary entity for the plan's match key (defaults to the first entity).</summary>
    public string? Entity { get; set; }

    /// <summary>Secondary entity for mixing plans (defaults to the second entity, if any).</summary>
    public string? SecondaryEntity { get; set; }

    /// <summary>Sample question — used when creating a plan with no owning interaction (from the no-answer queue).</summary>
    public string? SampleQuestion { get; set; }
}

/// <summary>A chosen tool plus a source decision for each of its parameters.</summary>
public class PlanToolInput
{
    public string Name { get; set; } = string.Empty;
    public List<PlanParamInput> Params { get; set; } = new();
}

/// <summary>
/// A reviewer's explicit decision for one tool parameter. <see cref="Source"/> is
/// static | extract | context | omit. "omit" is a deliberate "use the default" — a
/// plan can only be confirmed when every parameter has one of these set, so a needed
/// filter (period/status/…) can never be silently left at its default.
/// </summary>
public class PlanParamInput
{
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Placeholder { get; set; }
    public string? ContextKey { get; set; }
}

/// <summary>A clustered no-answer entry in the review queue (read model).</summary>
public class NoAnswerClusterDto
{
    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;          // code-set
    public string? ReviewedReason { get; set; }                 // reviewer override
    public string EffectiveReason { get; set; } = string.Empty; // reviewed ?? code-set
    public string SampleQuestion { get; set; } = string.Empty;
    public string Locale { get; set; } = "en";
    public string Evidence { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public string? UserFacingMessage { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? SampleInteractionId { get; set; }

    /// <summary>This is a backward "mis-refusal" (MissingParameter).</summary>
    public bool IsMisRefusal { get; set; }
    /// <summary>This is a forward coverage gap (NoCallingTool / UnsupportedDomain / UnsupportedEntity).</summary>
    public bool IsCoverageGap { get; set; }

    public DateTime LastSeenAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A user-reported answer in the review queue (read model snapshot).</summary>
public class ReportedAnswerDto
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Feedback { get; set; }
    public string Locale { get; set; } = "en";
    public Guid? BranchId { get; set; }
    public string? ReportedBy { get; set; }
    public bool Resolved { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

