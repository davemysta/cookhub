namespace CookHub.Data.Entities;

public class Recipe
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 500;

    public int Id { get; set; }

    public string OwnerId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }

    public string Title { get; set; } = "";
    public string? Description { get; set; }

    // The owner appears in "Liked by" next to their family members.
    public bool LikedByOwner { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    // Stored in its own table so list queries never load image bytes.
    public RecipeImage? Image { get; set; }

    public List<RecipeIngredient> Ingredients { get; set; } = [];
    public List<RecipeStep> Steps { get; set; } = [];
    public List<Tag> Tags { get; set; } = [];
    public List<RecipeFamilyLike> FamilyLikes { get; set; } = [];
    public List<MealPlanEntry> MealPlanEntries { get; set; } = [];
}
