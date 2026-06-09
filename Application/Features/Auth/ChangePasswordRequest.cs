using System.ComponentModel.DataAnnotations;

namespace Application.Features.Auth;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "OldPasswordRequired")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "NewPasswordRequired")]
    [MinLength(8, ErrorMessage = "PasswordTooShort")]
    [MaxLength(128, ErrorMessage = "MaxLengthError")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "ConfirmPasswordRequired")]
    [Compare(nameof(NewPassword), ErrorMessage = "PasswordsDoNotMatch")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
