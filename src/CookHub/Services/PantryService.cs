using CookHub.Data;
using CookHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CookHub.Services;

// The user's "ingredients on hand" list.
public sealed class PantryService(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<LookupItem>> GetOnHandAsync()
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.IngredientsOnHand
            .Where(o => o.UserId == userId)
            .OrderBy(o => o.Ingredient!.Name)
            .Select(o => new LookupItem(o.IngredientId, o.Ingredient!.Name))
            .ToListAsync();
    }

    public async Task SetOnHandAsync(int ingredientId, bool onHand)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!onHand)
        {
            await db.IngredientsOnHand
                .Where(o => o.UserId == userId && o.IngredientId == ingredientId)
                .ExecuteDeleteAsync();
            return;
        }

        if (await db.IngredientsOnHand.AnyAsync(o => o.UserId == userId && o.IngredientId == ingredientId))
        {
            return;
        }

        db.IngredientsOnHand.Add(new IngredientOnHand { UserId = userId, IngredientId = ingredientId, AddedUtc = DateTime.UtcNow });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A double click can add it twice; that's fine as long as one of them stuck.
            if (!await IsOnHandAsync(userId, ingredientId))
            {
                throw;
            }
        }
    }

    public async Task ClearAsync()
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.IngredientsOnHand.Where(o => o.UserId == userId).ExecuteDeleteAsync();
    }

    private async Task<bool> IsOnHandAsync(string userId, int ingredientId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.IngredientsOnHand.AnyAsync(o => o.UserId == userId && o.IngredientId == ingredientId);
    }
}
