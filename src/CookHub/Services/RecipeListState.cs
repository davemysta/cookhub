namespace CookHub.Services;

public enum RecipeListView
{
    Cards,
    List,
}

// Remembers the recipe list's search, filters and view while the user moves around the app.
public sealed class RecipeListState
{
    public RecipeQuery Query { get; } = new();
    public RecipeListView View { get; set; } = RecipeListView.Cards;
}
