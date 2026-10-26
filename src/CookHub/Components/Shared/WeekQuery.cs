using System.Globalization;
using CookHub.Services;

namespace CookHub.Components.Shared;

// Reads and writes the ?week=yyyy-MM-dd query string shared by the meal plan and shopping list pages.
public static class WeekQuery
{
    public const string Format = "yyyy-MM-dd";

    public static DateOnly Parse(string? value) =>
        DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? Week.StartOf(date)
            : Week.Current;

    public static string Url(string page, DateOnly weekStart) =>
        weekStart == Week.Current ? page : $"{page}?week={weekStart.ToString(Format, CultureInfo.InvariantCulture)}";
}
