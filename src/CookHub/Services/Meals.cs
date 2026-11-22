using CookHub.Data.Entities;

namespace CookHub.Services;

public static class Meals
{
    // In the order they're eaten, which is the row order of the meal plan table.
    public static IReadOnlyList<Meal> All { get; } = [Meal.Breakfast, Meal.Lunch, Meal.Dinner];

    // Entries with no meal sort after the ones that have one.
    public static int Index(Meal? meal) => meal is null ? All.Count : (int)meal.Value;
}
