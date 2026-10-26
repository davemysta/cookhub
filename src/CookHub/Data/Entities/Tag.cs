namespace CookHub.Data.Entities;

// Shared lookup list; new names are added the first time someone uses them.
public class Tag
{
    public const int NameMaxLength = 50;

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";

    public List<Recipe> Recipes { get; set; } = [];
}
