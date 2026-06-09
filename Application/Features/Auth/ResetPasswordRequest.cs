using System.ComponentModel.DataAnnotations;

namespace Application.Features.Auth;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "InvalidEmailFormat")]
    public string Email { get; set; } = string.Empty;
    
    [Required] // Token is typically hidden, message might not be visible to user, but safe to key it if needed.
    public string Token { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "NewPasswordRequired")]
    [MinLength(8, ErrorMessage = "MinLengthError")] // I should add "MinLengthError" to json
    [MaxLength(128, ErrorMessage = "MaxLengthError")]
    public string NewPassword { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "ConfirmPasswordRequired")]
    [Compare(nameof(NewPassword), ErrorMessage = "PasswordsDoNotMatch")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
