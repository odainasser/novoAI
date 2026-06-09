namespace Application.Validators;

internal static class PhoneNumberValidation
{
    public static bool IsValid(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return true;

        var trimmed = phone.Trim();
        var start = trimmed.StartsWith("+") ? 1 : 0;

        var digitCount = 0;
        for (var i = start; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (char.IsDigit(c)) { digitCount++; continue; }
            if (c == ' ' || c == '-' || c == '(' || c == ')') continue;
            return false;
        }

        return digitCount >= 7 && digitCount <= 15;
    }
}
