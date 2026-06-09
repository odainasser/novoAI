using System.ComponentModel.DataAnnotations;

namespace Application.Features.Suppliers;

public class SupplierDto
{
    public Guid Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? ContactPersonEn { get; set; }
    public string? ContactPersonAr { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateSupplierRequest
{
    [Required(ErrorMessage = "NameEnRequired")]
    [MaxLength(200, ErrorMessage = "NameEnMaxLength")]
    public string NameEn { get; set; } = string.Empty;

    [Required(ErrorMessage = "NameArRequired")]
    [MaxLength(200, ErrorMessage = "NameArMaxLength")]
    public string NameAr { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "MaxLengthError")]
    public string? ContactPersonEn { get; set; }

    [MaxLength(200, ErrorMessage = "MaxLengthError")]
    public string? ContactPersonAr { get; set; }

    [EmailAddress(ErrorMessage = "InvalidEmailFormat")]
    [MaxLength(256, ErrorMessage = "MaxLengthError")]
    public string? ContactEmail { get; set; }

    [MaxLength(50, ErrorMessage = "MaxLengthError")]
    public string? ContactPhone { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSupplierRequest
{
    [Required(ErrorMessage = "NameEnRequired")]
    [MaxLength(200, ErrorMessage = "NameEnMaxLength")]
    public string NameEn { get; set; } = string.Empty;

    [Required(ErrorMessage = "NameArRequired")]
    [MaxLength(200, ErrorMessage = "NameArMaxLength")]
    public string NameAr { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "MaxLengthError")]
    public string? ContactPersonEn { get; set; }

    [MaxLength(200, ErrorMessage = "MaxLengthError")]
    public string? ContactPersonAr { get; set; }

    [EmailAddress(ErrorMessage = "InvalidEmailFormat")]
    [MaxLength(256, ErrorMessage = "MaxLengthError")]
    public string? ContactEmail { get; set; }

    [MaxLength(50, ErrorMessage = "MaxLengthError")]
    public string? ContactPhone { get; set; }

    public bool IsActive { get; set; }
}
