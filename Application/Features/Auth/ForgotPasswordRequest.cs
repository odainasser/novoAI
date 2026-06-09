using System.ComponentModel.DataAnnotations;

namespace Application.Features.Auth;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "InvalidEmailFormat")]
    [MaxLength(256, ErrorMessage = "MaxLengthError")] // Ensure MaxLengthError exists
    public string Email { get; set; } = string.Empty;
}
