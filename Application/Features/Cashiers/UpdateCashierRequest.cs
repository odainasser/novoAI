namespace Application.Features.Cashiers;

public class UpdateCashierRequest
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool CanRefund { get; set; } = true;
    public List<Guid> WarehouseIds { get; set; } = new();
}
