namespace Web.Models.Assistant;

/// <summary>One logged assistant turn as a reviewable plan (admin read model).</summary>
public class AssistantInteractionDto
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Locale { get; set; } = "en";
    public string? Answer { get; set; }
    public bool Answered { get; set; }
    public bool IsMixing { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<string> Tools { get; set; } = new();
    public List<string> Domains { get; set; } = new();
    public List<string> Entities { get; set; } = new();

    public bool PlanConfirmed { get; set; }
    public List<string> ConfirmedTools { get; set; } = new();
    public string? ConfirmedDomain { get; set; }
    public List<string> ConfirmedEntities { get; set; } = new();
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
}

/// <summary>The code-owned vocabulary for the plan-correction dropdowns.</summary>
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

/// <summary>A reviewer's corrected plan for a turn (every param needs an explicit source).</summary>
public class ConfirmAssistantPlanRequest
{
    public List<PlanToolInput> Tools { get; set; } = new();
    public string? Domain { get; set; }
    public List<string> Entities { get; set; } = new();
    public string? Action { get; set; }
    public string? Entity { get; set; }
    public string? SecondaryEntity { get; set; }
    public string? SampleQuestion { get; set; }
}

public class PlanToolInput
{
    public string Name { get; set; } = string.Empty;
    public List<PlanParamInput> Params { get; set; } = new();
}

public class PlanParamInput
{
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // static | extract | context | omit
    public string? Value { get; set; }
    public string? Placeholder { get; set; }
    public string? ContextKey { get; set; }
}

/// <summary>A clustered no-answer entry in the review queue (Web read model).</summary>
public class NoAnswerClusterDto
{
    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReviewedReason { get; set; }
    public string EffectiveReason { get; set; } = string.Empty;
    public string SampleQuestion { get; set; } = string.Empty;
    public string Locale { get; set; } = "en";
    public string Evidence { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public string? UserFacingMessage { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? SampleInteractionId { get; set; }
    public bool IsMisRefusal { get; set; }
    public bool IsCoverageGap { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A user-reported answer in the review queue (Web read model snapshot).</summary>
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

