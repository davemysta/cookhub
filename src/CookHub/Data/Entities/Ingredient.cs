namespace CookHub.Data.Entities;

// Shared lookup list; new names are added the first time someone uses them.
public class Ingredient
{
    public const int NameMaxLength = 100;

    public int Id { get; set; }
    public string Name { get; set; } = "";
    // Upper-cased name with a unique index, so "Carrot" and "carrot" are the same ingredient.
    public string NormalizedName { get; set; } = "";
}
