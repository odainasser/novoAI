namespace Application.Services;

public interface IAssistantService
{
    Task<AssistantResponse> AskAsync(AssistantRequest request, string userId, IEnumerable<string> userPermissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a user-reported answer as a snapshot for admin review. A copy of the
    /// question + answer the user saw, with optional feedback — the user's own chat
    /// history is left untouched.
    /// </summary>
    Task ReportAnswerAsync(AssistantReportRequest request, string userId, CancellationToken cancellationToken = default);
}

public class AssistantReportRequest
{
    /// <summary>The registered app this report belongs to (e.g. "bytemart").</summary>
    public string? AppCode { get; set; }

    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Feedback { get; set; }
    public string Locale { get; set; } = "en";
    public Guid? BranchId { get; set; }
}

public class AssistantRequest
{
    /// <summary>
    /// The registered app whose tools should answer this question (e.g. "bytemart").
    /// Optional for single-app deployments: when empty, the oldest active app is used.
    /// </summary>
    public string? AppCode { get; set; }

    public string Question { get; set; } = string.Empty;
    public List<AssistantMessage> History { get; set; } = new();
    public string Locale { get; set; } = "en";

    /// <summary>
    /// When set, the assistant is hard-locked to this branch: every
    /// warehouse-aware query is constrained to the branch's warehouses and
    /// cross-branch / company-wide questions are refused. Sent by the Branch
    /// Panel widget (the active branch); null elsewhere (e.g. the Admin panel).
    /// </summary>
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
