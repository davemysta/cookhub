using CookHub.Data;
using CookHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CookHub.Services;

// Items the user adds straight to a week's shopping list, alongside the ingredients that come
// from the meal plan (see MealPlanService.GetShoppingLinesAsync).
public sealed class ShoppingListService(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<MiscShoppingItem>> GetMiscItemsAsync(DateOnly weekStart)
    {
        weekStart = Week.StartOf(weekStart);
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ShoppingListItems
            .Where(i => i.UserId == userId && i.WeekStart == weekStart)
            .OrderBy(i => i.AddedUtc)
            .ThenBy(i => i.Id)
            .Select(i => new MiscShoppingItem(i.Id, i.Name, i.Done))
            .ToListAsync();
    }

    public async Task<MiscShoppingItem> AddMiscItemAsync(DateOnly weekStart, string name)
    {
        name = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (name.Length == 0)
        {
            throw new ArgumentException("Enter an item.", nameof(name));
        }

        if (name.Length > ShoppingListItem.NameMaxLength)
        {
            throw new ArgumentException($"An item can be at most {ShoppingListItem.NameMaxLength} characters.", nameof(name));
        }

        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = new ShoppingListItem
        {
            UserId = userId,
            WeekStart = Week.StartOf(weekStart),
            Name = name,
            AddedUtc = DateTime.UtcNow,
        };
        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync();
        return new MiscShoppingItem(item.Id, item.Name, item.Done);
    }

    public async Task SetMiscItemDoneAsync(int id, bool done)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ShoppingListItems
            .Where(i => i.Id == id && i.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Done, done));
    }

    public async Task RemoveMiscItemAsync(int id)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ShoppingListItems
            .Where(i => i.Id == id && i.UserId == userId)
            .ExecuteDeleteAsync();
    }
}
