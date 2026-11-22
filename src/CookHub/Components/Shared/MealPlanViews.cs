using CookHub.Services;

namespace CookHub.Components.Shared;

public enum MealPlanView
{
    List,
    Table,
}

// Reads and writes the ?view=table query string of the meal plan page, alongside ?week=.
public static class MealPlanViews
{
    public static MealPlanView Parse(string? value) =>
        string.Equals(value, "table", StringComparison.OrdinalIgnoreCase) ? MealPlanView.Table : MealPlanView.List;

    public static string Url(DateOnly weekStart, MealPlanView view)
    {
        var url = WeekQuery.Url("meal-plan", weekStart);
        if (view == MealPlanView.List)
        {
            return url;
        }

        return url + (url.Contains('?') ? "&" : "?") + "view=table";
    }
}
