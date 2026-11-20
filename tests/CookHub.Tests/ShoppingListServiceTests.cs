using CookHub.Data.Entities;
using CookHub.Services;

namespace CookHub.Tests;

public class ShoppingListServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);

    [Fact]
    public async Task Misc_items_belong_to_their_week_in_the_order_added()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));

        await app.ShoppingList.AddMiscItemAsync(Monday, "  paper   towels ");
        await app.ShoppingList.AddMiscItemAsync(new DateOnly(2026, 10, 29), "foil"); // Thursday of the same week.
        await app.ShoppingList.AddMiscItemAsync(Monday.AddDays(7), "candles");

        Assert.Equal(["paper towels", "foil"], (await app.ShoppingList.GetMiscItemsAsync(Monday)).Select(i => i.Name));
        Assert.Equal(["candles"], (await app.ShoppingList.GetMiscItemsAsync(Monday.AddDays(7))).Select(i => i.Name));
    }

    [Fact]
    public async Task Misc_items_can_be_ticked_and_removed()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var towels = await app.ShoppingList.AddMiscItemAsync(Monday, "paper towels");
        var foil = await app.ShoppingList.AddMiscItemAsync(Monday, "foil");

        await app.ShoppingList.SetMiscItemDoneAsync(towels.Id, true);
        await app.ShoppingList.RemoveMiscItemAsync(foil.Id);

        var item = Assert.Single(await app.ShoppingList.GetMiscItemsAsync(Monday));
        Assert.Equal("paper towels", item.Name);
        Assert.True(item.Done);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_misc_items_are_refused(string name)
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));

        await Assert.ThrowsAsync<ArgumentException>(() => app.ShoppingList.AddMiscItemAsync(Monday, name));
    }

    [Fact]
    public async Task Overlong_misc_items_are_refused()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            app.ShoppingList.AddMiscItemAsync(Monday, new string('x', ShoppingListItem.NameMaxLength + 1)));
    }

    [Fact]
    public async Task Users_cannot_see_or_change_each_others_misc_items()
    {
        using var db = new TestDb();
        var dave = db.As(db.AddUser("Dave"));
        var sam = db.As(db.AddUser("Sam"));
        var towels = await dave.ShoppingList.AddMiscItemAsync(Monday, "paper towels");

        await sam.ShoppingList.SetMiscItemDoneAsync(towels.Id, true);
        await sam.ShoppingList.RemoveMiscItemAsync(towels.Id);

        Assert.Empty(await sam.ShoppingList.GetMiscItemsAsync(Monday));
        var item = Assert.Single(await dave.ShoppingList.GetMiscItemsAsync(Monday));
        Assert.False(item.Done);
    }
}
