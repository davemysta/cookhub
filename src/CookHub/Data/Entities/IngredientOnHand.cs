namespace CookHub.Data.Entities;

public class IngredientOnHand
{
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    public DateTime AddedUtc { get; set; }
}
