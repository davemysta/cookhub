namespace CookHub.Data.Entities;

// One ingredient line, e.g. "1 cup" + "carrot" + "diced".
public class RecipeIngredient
{
    public const int AmountMaxLength = 50;
    public const int PreparationMaxLength = 100;

    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    public string Amount { get; set; } = "";
    public string? Preparation { get; set; }
    public int SortOrder { get; set; }
}
