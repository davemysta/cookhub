using CookHub.Data;
using CookHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CookHub.Services;

public sealed class MealPlanService(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<MealPlanItem>> GetWeekAsync(DateOnly weekStart)
    {
        weekStart = Week.StartOf(weekStart);
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();

        var rows = await db.MealPlanEntries
            .Where(m => m.WeekStart == weekStart && m.Recipe!.OwnerId == userId)
            .Select(m => new
            {
                m.Id,
                m.RecipeId,
                m.Recipe!.Title,
                m.Recipe.UpdatedUtc,
                HasImage = m.Recipe.Image != null,
                m.Day,
                m.Meal,
                m.AddedUtc,
                Total = m.Recipe.Ingredients.Count,
                OnHand = m.Recipe.Ingredients.Count(i => db.IngredientsOnHand.Any(o => o.UserId == userId && o.IngredientId == i.IngredientId)),
            })
            .ToListAsync();

        return rows
            .OrderBy(r => Week.DayIndex(r.Day))
            .ThenBy(r => Meals.Index(r.Meal))
            .ThenBy(r => r.AddedUtc)
            .Select(r => new MealPlanItem(r.Id, r.RecipeId, r.Title, ImageUrls.ForRecipe(r.RecipeId, r.HasImage, r.UpdatedUtc), r.Day, r.Meal, r.Total, r.OnHand))
            .ToList();
    }

    public async Task AddAsync(int recipeId, DateOnly weekStart, DayOfWeek? day = null, Meal? meal = null)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.Recipes.AnyAsync(r => r.Id == recipeId && r.OwnerId == userId))
        {
            throw new InvalidOperationException("That recipe no longer exists.");
        }

        db.MealPlanEntries.Add(new MealPlanEntry
        {
            RecipeId = recipeId,
            WeekStart = Week.StartOf(weekStart),
            Day = day,
            Meal = meal,
            AddedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task SetDayAsync(int entryId, DayOfWeek? day)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.MealPlanEntries
            .Where(m => m.Id == entryId && m.Recipe!.OwnerId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Day, day));
    }

    public async Task SetMealAsync(int entryId, Meal? meal)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.MealPlanEntries
            .Where(m => m.Id == entryId && m.Recipe!.OwnerId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Meal, meal));
    }

    // Moves an entry to a day and meal at once, e.g. when it's dragged to another cell of the table.
    public async Task MoveAsync(int entryId, DayOfWeek? day, Meal? meal)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.MealPlanEntries
            .Where(m => m.Id == entryId && m.Recipe!.OwnerId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Day, day).SetProperty(m => m.Meal, meal));
    }

    public async Task RemoveAsync(int entryId)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.MealPlanEntries
            .Where(m => m.Id == entryId && m.Recipe!.OwnerId == userId)
            .ExecuteDeleteAsync();
    }

    // Every ingredient line of every recipe planned for the week, flagged with whether it's on hand.
    // The shopping list page groups and filters these.
    public async Task<IReadOnlyList<ShoppingLine>> GetShoppingLinesAsync(DateOnly weekStart)
    {
        weekStart = Week.StartOf(weekStart);
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();

        var lines = await (
                from entry in db.MealPlanEntries
                join line in db.RecipeIngredients on entry.RecipeId equals line.RecipeId
                where entry.WeekStart == weekStart && entry.Recipe!.OwnerId == userId
                select new ShoppingLine(
                    entry.Id,
                    entry.RecipeId,
                    entry.Recipe!.Title,
                    entry.Day,
                    line.IngredientId,
                    line.Ingredient!.Name,
                    line.Amount,
                    line.Preparation,
                    db.IngredientsOnHand.Any(o => o.UserId == userId && o.IngredientId == line.IngredientId)))
            .ToListAsync();

        return lines.OrderBy(l => l.IngredientName).ThenBy(l => l.RecipeTitle).ToList();
    }
}
