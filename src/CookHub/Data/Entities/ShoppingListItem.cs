namespace CookHub.Data.Entities;

// Something added straight to a week's shopping list rather than coming from a planned recipe,
// e.g. "paper towels". Shown under "Misc".
public class ShoppingListItem
{
    public const int NameMaxLength = 100;

    public int Id { get; set; }

    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    // Always a Monday.
    public DateOnly WeekStart { get; set; }

    public string Name { get; set; } = "";

    // Ticked off once it's in the basket.
    public bool Done { get; set; }

    public DateTime AddedUtc { get; set; }
}
