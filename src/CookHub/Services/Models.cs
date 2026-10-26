namespace CookHub.Services;

public sealed record LookupItem(int Id, string Name);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

// A person who can like a recipe: the user (FamilyMemberId null) or one of their family members.
public sealed record Person(int? FamilyMemberId, string Name)
{
    public bool IsMe => FamilyMemberId is null;
}

public sealed record LikedByEntry(Person Person, bool Liked);

public enum LikedByFilterKind
{
    Anyone,
    Everyone,
    Me,
    FamilyMember,
}

public sealed record LikedByFilter(LikedByFilterKind Kind, int? FamilyMemberId = null)
{
    public static LikedByFilter Anyone { get; } = new(LikedByFilterKind.Anyone);

    // A stable string form, used as the value of the filter drop-down.
    public string Key => Kind switch
    {
        LikedByFilterKind.Everyone => "everyone",
        LikedByFilterKind.Me => "me",
        LikedByFilterKind.FamilyMember => $"member:{FamilyMemberId}",
        _ => "anyone",
    };

    public static LikedByFilter FromKey(string? key) => key switch
    {
        "everyone" => new(LikedByFilterKind.Everyone),
        "me" => new(LikedByFilterKind.Me),
        _ when key?.StartsWith("member:") == true && int.TryParse(key["member:".Length..], out var id) => new(LikedByFilterKind.FamilyMember, id),
        _ => Anyone,
    };
}

public enum RecipeSort
{
    MostOnHand,
    Title,
    Newest,
}

public sealed class RecipeQuery
{
    public const int DefaultPageSize = 50;

    public string? Search { get; set; }
    public IReadOnlyCollection<int> TagIds { get; set; } = [];
    public LikedByFilter LikedBy { get; set; } = LikedByFilter.Anyone;
    public RecipeSort Sort { get; set; } = RecipeSort.Title;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;
}

public sealed record RecipeSummary(
    int Id,
    string Title,
    string? Description,
    string ImageUrl,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> LikedBy,
    int IngredientCount,
    int OnHandCount)
{
    public int MissingCount => IngredientCount - OnHandCount;
}

public sealed record RecipeIngredientLine(int IngredientId, string Name, string Amount, string? Preparation, bool OnHand);

public sealed record RecipeDetailsModel(
    int Id,
    string Title,
    string? Description,
    string ImageUrl,
    IReadOnlyList<LookupItem> Tags,
    IReadOnlyList<RecipeIngredientLine> Ingredients,
    IReadOnlyList<string> Steps,
    IReadOnlyList<LikedByEntry> LikedBy,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record MealPlanItem(
    int EntryId,
    int RecipeId,
    string Title,
    string ImageUrl,
    DayOfWeek? Day,
    int IngredientCount,
    int OnHandCount)
{
    public int MissingCount => IngredientCount - OnHandCount;
}

public sealed record ShoppingLine(
    int EntryId,
    int RecipeId,
    string RecipeTitle,
    DayOfWeek? Day,
    int IngredientId,
    string IngredientName,
    string Amount,
    string? Preparation,
    bool OnHand);
