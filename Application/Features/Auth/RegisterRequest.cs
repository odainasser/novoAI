using System.ComponentModel.DataAnnotations;

namespace Application.Features.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "InvalidEmailFormat")]
    [MaxLength(256, ErrorMessage = "MaxLengthError")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "PasswordRequired")]
    [MinLength(8, ErrorMessage = "PasswordTooShort")]
    [MaxLength(128, ErrorMessage = "MaxLengthError")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "ConfirmPasswordRequired")]
    [Compare(nameof(Password), ErrorMessage = "PasswordsDoNotMatch")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    // Note: Roles are intentionally omitted to ensure public registration always assigns the default Client role.
    // Do not add a Roles property to this class.
}
