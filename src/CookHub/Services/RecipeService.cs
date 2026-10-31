using CookHub.Data;
using CookHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CookHub.Services;

public sealed class RecipeService(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUser currentUser)
{
    public async Task<PagedResult<RecipeSummary>> SearchAsync(RecipeQuery query)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();

        var recipes = db.Recipes.Where(r => r.OwnerId == userId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Upper-casing keeps the search case-insensitive whatever the database collation is.
            var term = query.Search.Trim().ToUpperInvariant();
            recipes = recipes.Where(r =>
                r.Title.ToUpper().Contains(term)
                || (r.Description != null && r.Description.ToUpper().Contains(term))
                || r.Tags.Any(t => t.NormalizedName.Contains(term))
                || r.Ingredients.Any(i => i.Ingredient!.NormalizedName.Contains(term)));
        }

        // A recipe must have every selected tag.
        foreach (var tagId in query.TagIds)
        {
            recipes = recipes.Where(r => r.Tags.Any(t => t.Id == tagId));
        }

        recipes = query.LikedBy.Kind switch
        {
            LikedByFilterKind.Me => recipes.Where(r => r.LikedByOwner),
            LikedByFilterKind.FamilyMember => recipes.Where(r => r.FamilyLikes.Any(l => l.FamilyMemberId == query.LikedBy.FamilyMemberId)),
            LikedByFilterKind.Everyone => recipes.Where(r =>
                r.LikedByOwner
                && db.FamilyMembers.Where(f => f.UserId == userId).All(f => r.FamilyLikes.Any(l => l.FamilyMemberId == f.Id))),
            _ => recipes,
        };

        var totalCount = await recipes.CountAsync();
        var pageSize = Math.Max(1, query.PageSize);
        var pageCount = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Clamp(query.Page, 1, pageCount);

        var withCounts = WithCounts(db, recipes, userId);
        var ordered = query.Sort switch
        {
            RecipeSort.MostOnHand => withCounts
                .OrderByDescending(x => x.OnHand)
                .ThenBy(x => x.Total - x.OnHand)
                .ThenBy(x => x.Recipe.Title),
            RecipeSort.Newest => withCounts
                .OrderByDescending(x => x.Recipe.CreatedUtc)
                .ThenBy(x => x.Recipe.Title),
            _ => withCounts
                .OrderBy(x => x.Recipe.Title)
                .ThenBy(x => x.Recipe.Id),
        };

        var items = await ToSummariesAsync(db, ordered.Skip((page - 1) * pageSize).Take(pageSize), userId);
        return new PagedResult<RecipeSummary>(items, totalCount, page, pageSize);
    }

    public async Task<int> CountAsync()
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Recipes.CountAsync(r => r.OwnerId == userId);
    }

    // Recipes not already planned for the week, ranked by how much of them is already on hand.
    public async Task<IReadOnlyList<RecipeSummary>> GetSuggestionsAsync(DateOnly weekStart, int count)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();

        var candidates = db.Recipes.Where(r =>
            r.OwnerId == userId
            && r.Ingredients.Any()
            && !r.MealPlanEntries.Any(m => m.WeekStart == weekStart));

        var ranked = WithCounts(db, candidates, userId)
            .Where(x => x.OnHand > 0)
            .OrderByDescending(x => (double)x.OnHand / x.Total)
            .ThenByDescending(x => x.OnHand)
            .ThenBy(x => x.Recipe.Title)
            .Take(count);

        return await ToSummariesAsync(db, ranked, userId);
    }

    public async Task<IReadOnlyList<LookupItem>> SearchTitlesAsync(string? text, CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetUserIdAsync();
        var term = (text ?? "").Trim().ToUpperInvariant();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Recipes
            .Where(r => r.OwnerId == userId && r.Title.ToUpper().Contains(term))
            .OrderBy(r => r.Title)
            .Take(25)
            .Select(r => new LookupItem(r.Id, r.Title))
            .ToListAsync(cancellationToken);
    }

    public async Task<RecipeDetailsModel?> GetAsync(int id)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();

        var recipe = await db.Recipes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            .Include(r => r.Steps)
            .Include(r => r.Tags)
            .Include(r => r.FamilyLikes)
            .FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == userId);
        if (recipe is null)
        {
            return null;
        }

        var hasImage = await db.RecipeImages.AnyAsync(i => i.RecipeId == id);
        var ingredientIds = recipe.Ingredients.Select(i => i.IngredientId).ToList();
        var onHand = (await db.IngredientsOnHand
            .Where(o => o.UserId == userId && ingredientIds.Contains(o.IngredientId))
            .Select(o => o.IngredientId)
            .ToListAsync()).ToHashSet();
        var people = await FamilyService.GetPeopleAsync(db, userId);
        var likedMemberIds = recipe.FamilyLikes.Select(l => l.FamilyMemberId).ToHashSet();

        return new RecipeDetailsModel(
            recipe.Id,
            recipe.Title,
            recipe.Description,
            ImageUrls.ForRecipe(recipe.Id, hasImage, recipe.UpdatedUtc),
            recipe.Tags.OrderBy(t => t.Name).Select(t => new LookupItem(t.Id, t.Name)).ToList(),
            recipe.Ingredients
                .OrderBy(i => i.SortOrder)
                .Select(i => new RecipeIngredientLine(i.IngredientId, i.Ingredient!.Name, i.Amount, i.Preparation, onHand.Contains(i.IngredientId)))
                .ToList(),
            recipe.Steps.OrderBy(s => s.StepNumber).Select(s => s.Instruction).ToList(),
            people
                .Select(p => new LikedByEntry(p, p.IsMe ? recipe.LikedByOwner : likedMemberIds.Contains(p.FamilyMemberId!.Value)))
                .ToList(),
            recipe.CreatedUtc,
            recipe.UpdatedUtc);
    }

    public async Task<RecipeEditModel?> GetForEditAsync(int id)
    {
        var details = await GetAsync(id);
        if (details is null)
        {
            return null;
        }

        return new RecipeEditModel
        {
            Id = details.Id,
            Title = details.Title,
            Description = details.Description,
            Tags = details.Tags.ToList(),
            Ingredients = details.Ingredients
                .Select(i => new RecipeEditModel.IngredientRow
                {
                    Amount = i.Amount,
                    Ingredient = new LookupItem(i.IngredientId, i.Name),
                    Preparation = i.Preparation,
                })
                .ToList(),
            Steps = details.Steps.Select(s => new RecipeEditModel.StepRow { Instruction = s }).ToList(),
            LikedBy = details.LikedBy.Where(l => l.Liked).Select(l => l.Person.FamilyMemberId).ToHashSet(),
            ExistingImageUrl = details.ImageUrl == ImageUrls.Placeholder ? null : details.ImageUrl,
        };
    }

    // Creates or updates a recipe and returns its id.
    public async Task<int> SaveAsync(RecipeEditModel model)
    {
        var errors = model.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(model));
        }

        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        Recipe recipe;
        if (model.Id == 0)
        {
            recipe = new Recipe { OwnerId = userId, CreatedUtc = now };
            db.Recipes.Add(recipe);
        }
        else
        {
            recipe = await db.Recipes
                .AsSplitQuery()
                .Include(r => r.Ingredients)
                .Include(r => r.Steps)
                .Include(r => r.Tags)
                .Include(r => r.FamilyLikes)
                .Include(r => r.Image)
                .FirstOrDefaultAsync(r => r.Id == model.Id && r.OwnerId == userId)
                ?? throw new InvalidOperationException("That recipe no longer exists.");
        }

        recipe.Title = model.Title.Trim();
        recipe.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        recipe.UpdatedUtc = now;

        db.RecipeIngredients.RemoveRange(recipe.Ingredients);
        recipe.Ingredients = model.FilledIngredients
            .Select((row, index) => new RecipeIngredient
            {
                IngredientId = row.Ingredient!.Id,
                Amount = string.IsNullOrWhiteSpace(row.Amount) ? null : row.Amount.Trim(),
                Preparation = string.IsNullOrWhiteSpace(row.Preparation) ? null : row.Preparation.Trim(),
                SortOrder = index,
            })
            .ToList();

        db.RecipeSteps.RemoveRange(recipe.Steps);
        recipe.Steps = model.FilledSteps
            .Select((row, index) => new RecipeStep { StepNumber = index + 1, Instruction = row.Instruction.Trim() })
            .ToList();

        var tagIds = model.Tags.Select(t => t.Id).Distinct().ToList();
        var tags = await db.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync();
        recipe.Tags.RemoveAll(t => !tagIds.Contains(t.Id));
        recipe.Tags.AddRange(tags.Where(t => recipe.Tags.All(existing => existing.Id != t.Id)));

        recipe.LikedByOwner = model.LikedBy.Contains(null);
        var memberIds = model.LikedBy.Where(id => id is not null).Select(id => id!.Value).ToList();
        var validMemberIds = await db.FamilyMembers
            .Where(f => f.UserId == userId && memberIds.Contains(f.Id))
            .Select(f => f.Id)
            .ToListAsync();
        db.RecipeFamilyLikes.RemoveRange(recipe.FamilyLikes.Where(l => !validMemberIds.Contains(l.FamilyMemberId)));
        foreach (var memberId in validMemberIds.Where(id => recipe.FamilyLikes.All(l => l.FamilyMemberId != id)))
        {
            recipe.FamilyLikes.Add(new RecipeFamilyLike { FamilyMemberId = memberId });
        }

        if (model.NewImageData is { Length: > 0 })
        {
            recipe.Image ??= new RecipeImage();
            recipe.Image.Data = model.NewImageData;
            recipe.Image.ContentType = model.NewImageContentType!;
        }
        else if (model.RemoveImage && recipe.Image is not null)
        {
            db.RecipeImages.Remove(recipe.Image);
        }

        await db.SaveChangesAsync();
        return recipe.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        // Ingredients, steps, tag links, likes, the image and meal plan entries cascade.
        await db.Recipes.Where(r => r.Id == id && r.OwnerId == userId).ExecuteDeleteAsync();
    }

    public async Task SetLikedAsync(int recipeId, int? familyMemberId, bool liked)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();

        if (familyMemberId is null)
        {
            await db.Recipes
                .Where(r => r.Id == recipeId && r.OwnerId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.LikedByOwner, liked));
            return;
        }

        var owned = await db.Recipes.AnyAsync(r => r.Id == recipeId && r.OwnerId == userId)
            && await db.FamilyMembers.AnyAsync(f => f.Id == familyMemberId && f.UserId == userId);
        if (!owned)
        {
            return;
        }

        var existing = await db.RecipeFamilyLikes.FindAsync(recipeId, familyMemberId.Value);
        if (liked && existing is null)
        {
            db.RecipeFamilyLikes.Add(new RecipeFamilyLike { RecipeId = recipeId, FamilyMemberId = familyMemberId.Value });
        }
        else if (!liked && existing is not null)
        {
            db.RecipeFamilyLikes.Remove(existing);
        }

        await db.SaveChangesAsync();
    }

    // Used by the image endpoint, which runs outside a Blazor circuit and so passes the user id in.
    public async Task<(byte[] Data, string ContentType)?> GetImageAsync(int recipeId, string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var image = await db.RecipeImages
            .Where(i => i.RecipeId == recipeId && i.Recipe!.OwnerId == userId)
            .Select(i => new { i.Data, i.ContentType })
            .FirstOrDefaultAsync();
        return image is null ? null : (image.Data, image.ContentType);
    }

    private sealed class RecipeWithCounts
    {
        public required Recipe Recipe { get; init; }
        public int Total { get; init; }
        public int OnHand { get; init; }
    }

    private static IQueryable<RecipeWithCounts> WithCounts(ApplicationDbContext db, IQueryable<Recipe> recipes, string userId) =>
        recipes.Select(r => new RecipeWithCounts
        {
            Recipe = r,
            Total = r.Ingredients.Count,
            OnHand = r.Ingredients.Count(i => db.IngredientsOnHand.Any(o => o.UserId == userId && o.IngredientId == i.IngredientId)),
        });

    private static async Task<IReadOnlyList<RecipeSummary>> ToSummariesAsync(ApplicationDbContext db, IQueryable<RecipeWithCounts> source, string userId)
    {
        var rows = await source
            .Select(x => new
            {
                x.Recipe.Id,
                x.Recipe.Title,
                x.Recipe.Description,
                x.Recipe.UpdatedUtc,
                HasImage = x.Recipe.Image != null,
                Tags = x.Recipe.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToList(),
                x.Recipe.LikedByOwner,
                FamilyLikes = x.Recipe.FamilyLikes.OrderBy(l => l.FamilyMember!.Name).Select(l => l.FamilyMember!.Name).ToList(),
                x.Total,
                x.OnHand,
            })
            .AsSplitQuery()
            .ToListAsync();

        var myName = (await FamilyService.GetPeopleAsync(db, userId))[0].Name;

        return rows
            .Select(r => new RecipeSummary(
                r.Id,
                r.Title,
                r.Description,
                ImageUrls.ForRecipe(r.Id, r.HasImage, r.UpdatedUtc),
                r.Tags,
                r.LikedByOwner ? [myName, .. r.FamilyLikes] : r.FamilyLikes,
                r.Total,
                r.OnHand))
            .ToList();
    }
}
