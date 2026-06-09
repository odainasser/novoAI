namespace Web.Models.Terminals;

public class TerminalDto
{
    public Guid Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string? BranchNameEn { get; set; }
    public string? BranchNameAr { get; set; }
    public string? ComputerIp { get; set; }
    public string? PrinterIp { get; set; }
    public string? PaymentMachineIp { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateTerminalRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string? ComputerIp { get; set; }
    public string? PrinterIp { get; set; }
    public string? PaymentMachineIp { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateTerminalRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string? ComputerIp { get; set; }
    public string? PrinterIp { get; set; }
    public string? PaymentMachineIp { get; set; }
    public bool IsActive { get; set; }
}
