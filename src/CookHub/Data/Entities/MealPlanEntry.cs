namespace CookHub.Data.Entities;

// A recipe planned for a week. The owner is the recipe's owner.
public class MealPlanEntry
{
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    // Always a Monday.
    public DateOnly WeekStart { get; set; }

    // Optional day within the week; null means "sometime this week".
    public DayOfWeek? Day { get; set; }

    public DateTime AddedUtc { get; set; }
}
