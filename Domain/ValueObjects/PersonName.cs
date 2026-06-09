namespace Domain.ValueObjects;

public class PersonName
{
    public string FirstName { get; }
    public string? LastName { get; }

    public PersonName(string firstName, string? lastName = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name cannot be empty.", nameof(firstName));

        FirstName = firstName.Trim();
        LastName = lastName?.Trim();
    }

    public string FullName => LastName != null ? $"{FirstName} {LastName}" : FirstName;

    public override string ToString() => FullName;

    public override bool Equals(object? obj)
    {
        if (obj is not PersonName other)
            return false;

        return FirstName.Equals(other.FirstName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(LastName, other.LastName, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode() => HashCode.Combine(FirstName, LastName);
}
