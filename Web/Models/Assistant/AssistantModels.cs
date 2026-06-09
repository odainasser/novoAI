namespace Web.Models.Assistant;

public class AssistantRequest
{
    public string Question { get; set; } = string.Empty;
    public List<AssistantMessage> History { get; set; } = new();
    public string Locale { get; set; } = "en";

    // When set, hard-locks the assistant to this branch (Branch Panel scoping).
    public Guid? BranchId { get; set; }
}

public class AssistantResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<AssistantMessage> History { get; set; } = new();
}

public class AssistantMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>Report an assistant answer (snapshot of the reported turn + optional feedback).</summary>
public class AssistantReportRequest
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Feedback { get; set; }
    public string Locale { get; set; } = "en";
    public Guid? BranchId { get; set; }
}
