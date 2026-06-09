using System.Globalization;

namespace Web.Services;

/// <summary>
/// Extension methods for IJsonStringLocalizer to provide additional functionality
/// </summary>
public static class JsonStringLocalizerExtensions
{
    /// <summary>
    /// Gets a localized string for a boolean value (Yes/No)
    /// </summary>
    public static string GetBooleanString(this IJsonStringLocalizer localizer, bool value)
    {
        return value ? localizer["Yes"] : localizer["No"];
    }

    /// <summary>
    /// Gets a localized string for status (Active/Inactive)
    /// </summary>
    public static string GetStatusString(this IJsonStringLocalizer localizer, bool isActive)
    {
        return isActive ? localizer["Active"] : localizer["Inactive"];
    }

    /// <summary>
    /// Gets a localized date string using current culture
    /// </summary>
    public static string GetLocalizedDate(this IJsonStringLocalizer localizer, DateTime date)
    {
        return date.ToString("d", CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Gets a localized date and time string using current culture
    /// </summary>
    public static string GetLocalizedDateTime(this IJsonStringLocalizer localizer, DateTime dateTime)
    {
        return dateTime.ToString("g", CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Gets a localized number string using current culture
    /// </summary>
    public static string GetLocalizedNumber(this IJsonStringLocalizer localizer, decimal number)
    {
        return number.ToString("N", CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Gets a localized currency string using current culture
    /// </summary>
    public static string GetLocalizedCurrency(this IJsonStringLocalizer localizer, decimal amount, string? currencyCode = null)
    {
        var culture = CultureInfo.CurrentUICulture;
        if (string.IsNullOrEmpty(currencyCode))
        {
            var unit = localizer["CurrencyUnit"];
            return string.Format(culture, "{0:N2} {1}", amount, string.IsNullOrEmpty(unit) ? "AED" : unit);
        }

        return string.Format(culture, "{0:N2} {1}", amount, currencyCode);
    }

    /// <summary>
    /// Gets a localized confirmation message for delete action
    /// </summary>
    public static string GetDeleteConfirmation(this IJsonStringLocalizer localizer, string? itemName = null)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            return localizer["ConfirmDelete"];
        }
        
        return localizer.GetString("ConfirmDeleteItem", itemName);
    }
}
