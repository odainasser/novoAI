namespace Web.Services;

public static class DateTimeExtensions
{
    // Server timestamps come back from JSON as Kind=Unspecified because EF
    // reads SQL Server datetime2 without a kind. They're UTC in fact, so we
    // explicitly mark them and convert to the browser's local time for display.
    public static DateTime ToLocal(this DateTime value)
    {
        var asUtc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        return asUtc.ToLocalTime();
    }

    public static DateTime? ToLocal(this DateTime? value) => value?.ToLocal();
}
