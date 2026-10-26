namespace CookHub.Services;

// Meal plan weeks run Monday to Sunday.
public static class Week
{
    public static DateOnly StartOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static DateOnly Current => StartOf(DateOnly.FromDateTime(DateTime.Now));

    public static string Label(DateOnly weekStart)
    {
        var end = weekStart.AddDays(6);
        return weekStart.Year == end.Year
            ? $"{weekStart:ddd MMM d} – {end:ddd MMM d, yyyy}"
            : $"{weekStart:ddd MMM d, yyyy} – {end:ddd MMM d, yyyy}";
    }

    // Monday first, to match the week.
    public static IReadOnlyList<DayOfWeek> Days { get; } =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    public static int DayIndex(DayOfWeek? day) => day is null ? 7 : ((int)day.Value + 6) % 7;
}
