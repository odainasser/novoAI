namespace Application.Features.Dashboard;

public class DashboardSummaryDto
{
    // Facilities
    public int TotalWarehouses { get; set; }
    public int TotalBranches { get; set; }
    public int TotalTerminals { get; set; }

    // Users
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
}
