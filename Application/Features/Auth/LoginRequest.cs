using System.ComponentModel.DataAnnotations;

namespace Application.Features.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "InvalidEmailFormat")]
    [MaxLength(256, ErrorMessage = "MaxLengthError")] // Ensure MaxLengthError exists or use generic
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "PasswordRequired")]
    [MaxLength(128, ErrorMessage = "MaxLengthError")]
    public string Password { get; set; } = string.Empty;
}
