namespace Web.Services;

// Active branch for the Branch Panel.
//
// Per spec: this is in-memory only. A hard reload clears the selection and
// forces the employee to pick again. This is deliberately different from the
// Cashier portal's ActiveStoreContext (which persists to localStorage to
// survive offline reloads); the Branch Panel is online-only and short-lived.
public class ActiveBranchContext
{
    private readonly object _lock = new();

    public Guid? BranchId { get; private set; }
    public string? BranchNameEn { get; private set; }
    public string? BranchNameAr { get; private set; }

    public event Action? OnChanged;

    public bool IsSelected => BranchId.HasValue;

    public void Set(Guid branchId, string? nameEn, string? nameAr)
    {
        lock (_lock)
        {
            BranchId = branchId;
            BranchNameEn = nameEn;
            BranchNameAr = nameAr;
        }
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            BranchId = null;
            BranchNameEn = null;
            BranchNameAr = null;
        }
        OnChanged?.Invoke();
    }
}
