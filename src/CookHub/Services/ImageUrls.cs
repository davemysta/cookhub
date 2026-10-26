namespace CookHub.Services;

public static class ImageUrls
{
    public const string Placeholder = "images/recipe-placeholder.svg";

    // The version query string changes whenever the recipe is saved, so browsers refetch a replaced image.
    public static string ForRecipe(int recipeId, bool hasImage, DateTime updatedUtc) =>
        hasImage ? $"recipe-images/{recipeId}?v={updatedUtc.Ticks}" : Placeholder;
}
