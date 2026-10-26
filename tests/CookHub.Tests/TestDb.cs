using CookHub.Data;
using CookHub.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CookHub.Tests;

// An in-memory SQLite database shared by every context the services create, plus helpers
// to build services acting as a given user.
public sealed class TestDb : IDbContextFactory<ApplicationDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateDbContext() => new(_options);

    public string AddUser(string displayName)
    {
        using var db = CreateDbContext();
        var user = new ApplicationUser { UserName = $"{displayName}@test", Email = $"{displayName}@test", DisplayName = displayName };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    public Services As(string userId) => new(this, new FixedUser(userId));

    public void Dispose() => _connection.Dispose();

    public sealed class Services(TestDb db, ICurrentUser user)
    {
        public LookupService Lookups { get; } = new(db);
        public RecipeService Recipes { get; } = new(db, user);
        public FamilyService Family { get; } = new(db, user);
        public PantryService Pantry { get; } = new(db, user);
        public MealPlanService MealPlans { get; } = new(db, user);

        // Creates a recipe whose ingredients and tags are looked up (or added) by name.
        public async Task<int> AddRecipeAsync(string title, string[] ingredients, string[]? tags = null, IEnumerable<int?>? likedBy = null)
        {
            var model = new RecipeEditModel
            {
                Title = title,
                Steps = [new() { Instruction = "Cook it." }],
                LikedBy = likedBy?.ToHashSet() ?? [],
            };
            foreach (var name in ingredients)
            {
                model.Ingredients.Add(new() { Amount = "1", Ingredient = await Lookups.GetOrCreateIngredientAsync(name) });
            }

            foreach (var tag in tags ?? [])
            {
                model.Tags.Add(await Lookups.GetOrCreateTagAsync(tag));
            }

            return await Recipes.SaveAsync(model);
        }

        public async Task SetOnHandAsync(params string[] ingredients)
        {
            foreach (var name in ingredients)
            {
                await Pantry.SetOnHandAsync((await Lookups.GetOrCreateIngredientAsync(name)).Id, true);
            }
        }
    }

    private sealed class FixedUser(string userId) : ICurrentUser
    {
        public Task<string> GetUserIdAsync() => Task.FromResult(userId);
    }
}
