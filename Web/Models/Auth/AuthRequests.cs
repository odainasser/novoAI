using System.ComponentModel.DataAnnotations;

namespace Web.Models.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "InvalidEmailFormat")]
    [MaxLength(256, ErrorMessage = "MaxLengthError")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "PasswordRequired")]
    [MaxLength(128, ErrorMessage = "MaxLengthError")]
    public string Password { get; set; } = string.Empty;
}

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
}

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

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "InvalidEmailFormat")]
    [MaxLength(256, ErrorMessage = "MaxLengthError")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "InvalidEmailFormat")]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Token { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "NewPasswordRequired")]
    [MinLength(8, ErrorMessage = "MinLengthError")]
    [MaxLength(128, ErrorMessage = "MaxLengthError")]
    public string NewPassword { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "ConfirmPasswordRequired")]
    [Compare(nameof(NewPassword), ErrorMessage = "PasswordsDoNotMatch")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ConfirmEmailRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
