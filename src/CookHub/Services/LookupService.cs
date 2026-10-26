using CookHub.Data;
using CookHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CookHub.Services;

// Ingredients and tags are shared lookup lists that grow as people use new names.
public sealed class LookupService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private const int MaxResults = 25;

    // Trims and collapses inner whitespace, so "  green   onion " becomes "green onion".
    public static string CleanName(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static string Normalize(string name) => CleanName(name).ToUpperInvariant();

    public async Task<IReadOnlyList<LookupItem>> SearchIngredientsAsync(string? text, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(text ?? "");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Ingredients
            .Where(i => i.NormalizedName.Contains(normalized))
            .OrderBy(i => i.NormalizedName.StartsWith(normalized) ? 0 : 1)
            .ThenBy(i => i.Name)
            .Take(MaxResults)
            .Select(i => new LookupItem(i.Id, i.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupItem>> SearchTagsAsync(string? text, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(text ?? "");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tags
            .Where(t => t.NormalizedName.Contains(normalized))
            .OrderBy(t => t.NormalizedName.StartsWith(normalized) ? 0 : 1)
            .ThenBy(t => t.Name)
            .Take(MaxResults)
            .Select(t => new LookupItem(t.Id, t.Name))
            .ToListAsync(cancellationToken);
    }

    // Tags used by at least one of the user's recipes, for the recipe list filter.
    public async Task<IReadOnlyList<LookupItem>> GetTagsInUseAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Tags
            .Where(t => t.Recipes.Any(r => r.OwnerId == userId))
            .OrderBy(t => t.Name)
            .Select(t => new LookupItem(t.Id, t.Name))
            .ToListAsync();
    }

    public async Task<LookupItem> GetOrCreateIngredientAsync(string name)
    {
        var clean = ValidateName(name, Ingredient.NameMaxLength);
        var normalized = clean.ToUpperInvariant();

        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.Ingredients.FirstOrDefaultAsync(i => i.NormalizedName == normalized);
        if (existing is not null)
        {
            return new LookupItem(existing.Id, existing.Name);
        }

        var ingredient = new Ingredient { Name = clean, NormalizedName = normalized };
        db.Ingredients.Add(ingredient);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Someone else added the same name at the same moment; use theirs.
            await using var retryDb = await dbFactory.CreateDbContextAsync();
            ingredient = await retryDb.Ingredients.FirstAsync(i => i.NormalizedName == normalized);
        }

        return new LookupItem(ingredient.Id, ingredient.Name);
    }

    public async Task<LookupItem> GetOrCreateTagAsync(string name)
    {
        // Tags are stored lower case so they look consistent as chips.
        var clean = ValidateName(name, Tag.NameMaxLength).ToLowerInvariant();
        var normalized = clean.ToUpperInvariant();

        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.Tags.FirstOrDefaultAsync(t => t.NormalizedName == normalized);
        if (existing is not null)
        {
            return new LookupItem(existing.Id, existing.Name);
        }

        var tag = new Tag { Name = clean, NormalizedName = normalized };
        db.Tags.Add(tag);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            await using var retryDb = await dbFactory.CreateDbContextAsync();
            tag = await retryDb.Tags.FirstAsync(t => t.NormalizedName == normalized);
        }

        return new LookupItem(tag.Id, tag.Name);
    }

    private static string ValidateName(string name, int maxLength)
    {
        var clean = CleanName(name);
        if (clean.Length == 0)
        {
            throw new ArgumentException("A name is required.", nameof(name));
        }

        if (clean.Length > maxLength)
        {
            throw new ArgumentException($"Names can be at most {maxLength} characters.", nameof(name));
        }

        return clean;
    }
}
