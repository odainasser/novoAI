using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace Infrastructure.Services;

internal static class IdentityHelpers
{
    internal static ValidationFailure MapIdentityErrorToValidationFailure(IdentityError error)
    {
        string propertyName = string.Empty;

        if (error.Code.StartsWith("Password"))
        {
            propertyName = "Password";
        }
        else if (error.Code.Contains("Email") || error.Code.Contains("UserName"))
        {
            propertyName = "Email";
        }

        // Use Code as the Error Message key for localization
        return new ValidationFailure(propertyName, error.Code);
    }

    internal static string GenerateRandomPassword(int length = 16)
    {
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%^&*()_+-=";
        const string all = upper + lower + digits + special;

        var password = new char[length];
        var bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        // Ensure at least one of each required character type
        password[0] = upper[bytes[0] % upper.Length];
        password[1] = lower[bytes[1] % lower.Length];
        password[2] = digits[bytes[2] % digits.Length];
        password[3] = special[bytes[3] % special.Length];

        for (int i = 4; i < length; i++)
        {
            password[i] = all[bytes[i] % all.Length];
        }

        // Shuffle the password
        var random = new Random(BitConverter.ToInt32(bytes, 0));
        for (int i = password.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }
}
