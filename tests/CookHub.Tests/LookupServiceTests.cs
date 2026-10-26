namespace CookHub.Tests;

public class LookupServiceTests
{
    [Fact]
    public async Task GetOrCreate_reuses_an_existing_ingredient_regardless_of_case_and_spacing()
    {
        using var db = new TestDb();
        var lookups = db.As(db.AddUser("Dave")).Lookups;

        var first = await lookups.GetOrCreateIngredientAsync("Green Onion");
        var second = await lookups.GetOrCreateIngredientAsync("  green   onion ");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Green Onion", second.Name);
        Assert.Single(await lookups.SearchIngredientsAsync("onion"));
    }

    [Fact]
    public async Task Tags_are_stored_lower_case_and_created_once()
    {
        using var db = new TestDb();
        var lookups = db.As(db.AddUser("Dave")).Lookups;

        var tag = await lookups.GetOrCreateTagAsync("Mexican");
        var again = await lookups.GetOrCreateTagAsync("MEXICAN");

        Assert.Equal("mexican", tag.Name);
        Assert.Equal(tag.Id, again.Id);
    }

    [Fact]
    public async Task Search_puts_names_starting_with_the_text_first()
    {
        using var db = new TestDb();
        var lookups = db.As(db.AddUser("Dave")).Lookups;
        await lookups.GetOrCreateIngredientAsync("sweet potato");
        await lookups.GetOrCreateIngredientAsync("potato");

        var results = await lookups.SearchIngredientsAsync("pot");

        Assert.Equal(["potato", "sweet potato"], results.Select(r => r.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_names_are_rejected(string name)
    {
        using var db = new TestDb();
        var lookups = db.As(db.AddUser("Dave")).Lookups;

        await Assert.ThrowsAsync<ArgumentException>(() => lookups.GetOrCreateIngredientAsync(name));
    }
}
