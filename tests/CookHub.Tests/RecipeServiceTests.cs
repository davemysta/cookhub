using CookHub.Data.Entities;
using CookHub.Services;

namespace CookHub.Tests;

public class RecipeServiceTests
{
    [Fact]
    public void Validate_requires_a_title_an_ingredient_and_a_step()
    {
        var errors = new RecipeEditModel { Ingredients = [new()], Steps = [new()] }.Validate();

        Assert.Contains("A title is required.", errors);
        Assert.Contains("Add at least one ingredient.", errors);
        Assert.Contains("Add at least one step.", errors);
    }

    [Fact]
    public void Validate_requires_a_looked_up_ingredient_but_not_amount_or_preparation()
    {
        var model = new RecipeEditModel
        {
            Title = "Soup",
            Steps = [new() { Instruction = "Simmer." }],
            Ingredients =
            [
                new() { Amount = "1 cup", Ingredient = new LookupItem(1, "milk") },
                new() { Amount = "", Ingredient = new LookupItem(2, "carrot"), Preparation = "diced" },
                new() { Amount = "2" },
            ],
        };

        var errors = model.Validate();

        Assert.Equal(["Ingredient 3: look up and select an ingredient."], errors);
    }

    [Fact]
    public async Task SaveAsync_stores_a_blank_amount_as_no_amount()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var salt = await app.Lookups.GetOrCreateIngredientAsync("salt");

        var id = await app.Recipes.SaveAsync(new RecipeEditModel
        {
            Title = "Fries",
            Steps = [new() { Instruction = "Fry." }],
            Ingredients = [new() { Amount = "  ", Ingredient = salt, Preparation = "to taste" }],
        });

        var line = Assert.Single((await app.Recipes.GetAsync(id))!.Ingredients);
        Assert.Null(line.Amount);
        Assert.Equal("salt", line.Name);
        Assert.Equal("to taste", line.Preparation);
    }

    [Fact]
    public void Validate_limits_the_description_to_500_characters()
    {
        var model = new RecipeEditModel
        {
            Title = "Soup",
            Description = new string('x', Recipe.DescriptionMaxLength + 1),
            Steps = [new() { Instruction = "Simmer." }],
            Ingredients = [new() { Amount = "1", Ingredient = new LookupItem(1, "milk") }],
        };

        Assert.Contains("The description can be at most 500 characters.", model.Validate());
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData(null)]
    public void Validate_only_accepts_raster_images(string? contentType)
    {
        var model = new RecipeEditModel
        {
            Title = "Soup",
            Steps = [new() { Instruction = "Simmer." }],
            Ingredients = [new() { Amount = "1", Ingredient = new LookupItem(1, "milk") }],
            NewImageData = [1],
            NewImageContentType = contentType,
        };

        Assert.Contains("The photo must be a JPEG, PNG, GIF or WebP image.", model.Validate());
    }

    [Fact]
    public async Task SaveAsync_rejects_an_invalid_recipe()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));

        await Assert.ThrowsAsync<ArgumentException>(() => app.Recipes.SaveAsync(new RecipeEditModel { Title = "Empty" }));
    }

    [Fact]
    public async Task A_saved_recipe_round_trips_with_ingredients_in_order_and_likes()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var sam = await app.Family.AddAsync("Sam");

        var id = await app.AddRecipeAsync("Tacos", ["tortillas", "chicken", "lime"], ["mexican"], likedBy: [null, sam.Id]);
        var recipe = await app.Recipes.GetAsync(id);

        Assert.NotNull(recipe);
        Assert.Equal(["tortillas", "chicken", "lime"], recipe.Ingredients.Select(i => i.Name));
        Assert.Equal(["mexican"], recipe.Tags.Select(t => t.Name));
        Assert.Equal(ImageUrls.Placeholder, recipe.ImageUrl);
        Assert.All(recipe.LikedBy, l => Assert.True(l.Liked));
        Assert.Equal(["Dave", "Sam"], recipe.LikedBy.Select(l => l.Person.Name));
    }

    [Fact]
    public async Task Editing_keeps_the_image_unless_it_is_removed()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var id = await app.AddRecipeAsync("Tacos", ["tortillas"]);

        var model = (await app.Recipes.GetForEditAsync(id))!;
        model.NewImageData = [1, 2, 3];
        model.NewImageContentType = "image/png";
        await app.Recipes.SaveAsync(model);
        Assert.StartsWith($"recipe-images/{id}?v=", (await app.Recipes.GetAsync(id))!.ImageUrl);

        model = (await app.Recipes.GetForEditAsync(id))!;
        model.Title = "Street Tacos";
        model.Ingredients.Add(new() { Amount = "1", Ingredient = await app.Lookups.GetOrCreateIngredientAsync("lime") });
        await app.Recipes.SaveAsync(model);
        var edited = (await app.Recipes.GetAsync(id))!;
        Assert.Equal("Street Tacos", edited.Title);
        Assert.Equal(["tortillas", "lime"], edited.Ingredients.Select(i => i.Name));
        Assert.NotEqual(ImageUrls.Placeholder, edited.ImageUrl);

        model = (await app.Recipes.GetForEditAsync(id))!;
        model.RemoveImage = true;
        await app.Recipes.SaveAsync(model);
        Assert.Equal(ImageUrls.Placeholder, (await app.Recipes.GetAsync(id))!.ImageUrl);
    }

    [Fact]
    public async Task Search_matches_title_tag_and_ingredient_case_insensitively()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        await app.AddRecipeAsync("Chicken Tacos", ["tortillas", "chicken"], ["mexican"]);
        await app.AddRecipeAsync("Carrot Soup", ["carrot", "milk"], ["vegetarian"]);

        async Task<IEnumerable<string>> Find(string text) =>
            (await app.Recipes.SearchAsync(new RecipeQuery { Search = text })).Items.Select(r => r.Title);

        Assert.Equal(["Chicken Tacos"], await Find("TACO"));
        Assert.Equal(["Carrot Soup"], await Find("Vegetarian"));
        Assert.Equal(["Carrot Soup"], await Find("milk"));
        Assert.Empty(await Find("pizza"));
    }

    [Fact]
    public async Task Tag_filter_requires_every_selected_tag()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        await app.AddRecipeAsync("Tacos", ["tortillas"], ["mexican", "quick"]);
        await app.AddRecipeAsync("Mole", ["chocolate"], ["mexican"]);
        var mexican = await app.Lookups.GetOrCreateTagAsync("mexican");
        var quick = await app.Lookups.GetOrCreateTagAsync("quick");

        var both = await app.Recipes.SearchAsync(new RecipeQuery { TagIds = [mexican.Id, quick.Id] });
        var one = await app.Recipes.SearchAsync(new RecipeQuery { TagIds = [mexican.Id] });

        Assert.Equal(["Tacos"], both.Items.Select(r => r.Title));
        Assert.Equal(2, one.TotalCount);
    }

    [Fact]
    public async Task Liked_by_filter_supports_me_a_member_and_everyone()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var sam = await app.Family.AddAsync("Sam");
        var alex = await app.Family.AddAsync("Alex");
        await app.AddRecipeAsync("Everyone's favourite", ["a"], likedBy: [null, sam.Id, alex.Id]);
        await app.AddRecipeAsync("Sam and me", ["b"], likedBy: [null, sam.Id]);
        await app.AddRecipeAsync("Nobody", ["c"]);

        async Task<IEnumerable<string>> Find(LikedByFilter filter) =>
            (await app.Recipes.SearchAsync(new RecipeQuery { LikedBy = filter })).Items.Select(r => r.Title);

        Assert.Equal(3, (await Find(LikedByFilter.Anyone)).Count());
        Assert.Equal(["Everyone's favourite"], await Find(new LikedByFilter(LikedByFilterKind.Everyone)));
        Assert.Equal(["Everyone's favourite", "Sam and me"], await Find(new LikedByFilter(LikedByFilterKind.Me)));
        Assert.Equal(["Everyone's favourite"], await Find(new LikedByFilter(LikedByFilterKind.FamilyMember, alex.Id)));
    }

    [Fact]
    public void Liked_by_filter_keys_round_trip()
    {
        foreach (var filter in new[] { LikedByFilter.Anyone, new(LikedByFilterKind.Everyone), new(LikedByFilterKind.Me), new(LikedByFilterKind.FamilyMember, 42) })
        {
            Assert.Equal(filter, LikedByFilter.FromKey(filter.Key));
        }

        Assert.Equal(LikedByFilter.Anyone, LikedByFilter.FromKey("member:nope"));
    }

    [Fact]
    public async Task Sorting_by_on_hand_puts_recipes_with_the_most_ingredients_on_hand_first()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        await app.AddRecipeAsync("A: none on hand", ["x", "y"]);
        await app.AddRecipeAsync("B: two on hand of three", ["milk", "carrot", "onion"]);
        await app.AddRecipeAsync("C: one on hand of one", ["milk"]);
        await app.AddRecipeAsync("D: two on hand of two", ["milk", "carrot"]);
        await app.SetOnHandAsync("milk", "carrot");

        var result = await app.Recipes.SearchAsync(new RecipeQuery { Sort = RecipeSort.MostOnHand });

        Assert.Equal(["D: two on hand of two", "B: two on hand of three", "C: one on hand of one", "A: none on hand"], result.Items.Select(r => r.Title));
        Assert.Equal((2, 3), (result.Items[1].OnHandCount, result.Items[1].IngredientCount));
    }

    [Fact]
    public async Task Results_are_paged_fifty_at_a_time()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        for (var i = 1; i <= 120; i++)
        {
            await app.AddRecipeAsync($"Recipe {i:000}", ["salt"]);
        }

        var first = await app.Recipes.SearchAsync(new RecipeQuery { Page = 1 });
        var last = await app.Recipes.SearchAsync(new RecipeQuery { Page = 3 });
        var beyond = await app.Recipes.SearchAsync(new RecipeQuery { Page = 99 });

        Assert.Equal((50, 120, 3), (first.Items.Count, first.TotalCount, first.PageCount));
        Assert.Equal("Recipe 001", first.Items[0].Title);
        Assert.Equal(20, last.Items.Count);
        Assert.Equal("Recipe 101", last.Items[0].Title);
        Assert.Equal(3, beyond.Page);
    }

    [Fact]
    public async Task Users_only_see_and_change_their_own_recipes()
    {
        using var db = new TestDb();
        var dave = db.As(db.AddUser("Dave"));
        var other = db.As(db.AddUser("Other"));
        var id = await dave.AddRecipeAsync("Dave's secret chili", ["beans"]);

        Assert.Equal(0, (await other.Recipes.SearchAsync(new RecipeQuery())).TotalCount);
        Assert.Null(await other.Recipes.GetAsync(id));
        await other.Recipes.SetLikedAsync(id, null, true);
        await other.Recipes.DeleteAsync(id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.MealPlans.AddAsync(id, Week.Current));

        var recipe = await dave.Recipes.GetAsync(id);
        Assert.NotNull(recipe);
        Assert.False(recipe.LikedBy.Single().Liked);
    }

    [Fact]
    public async Task Removing_a_family_member_removes_their_likes()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var sam = await app.Family.AddAsync("Sam");
        var id = await app.AddRecipeAsync("Tacos", ["tortillas"], likedBy: [sam.Id]);

        await app.Family.DeleteAsync(sam.Id);

        var recipe = (await app.Recipes.GetAsync(id))!;
        Assert.Equal(["Dave"], recipe.LikedBy.Select(l => l.Person.Name));
        await using var context = db.CreateDbContext();
        Assert.Empty(context.RecipeFamilyLikes);
    }

    [Fact]
    public async Task Deleting_a_recipe_removes_it_from_meal_plans()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var id = await app.AddRecipeAsync("Tacos", ["tortillas"]);
        await app.MealPlans.AddAsync(id, Week.Current);

        await app.Recipes.DeleteAsync(id);

        Assert.Empty(await app.MealPlans.GetWeekAsync(Week.Current));
    }

    [Fact]
    public async Task Suggestions_skip_planned_recipes_and_rank_by_share_on_hand()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var planned = await app.AddRecipeAsync("Planned", ["milk"]);
        await app.AddRecipeAsync("Half", ["milk", "flour"]);
        await app.AddRecipeAsync("All", ["milk", "carrot"]);
        await app.AddRecipeAsync("None", ["saffron"]);
        await app.SetOnHandAsync("milk", "carrot");
        await app.MealPlans.AddAsync(planned, Week.Current);

        var suggestions = await app.Recipes.GetSuggestionsAsync(Week.Current, 5);

        Assert.Equal(["All", "Half"], suggestions.Select(s => s.Title));
    }
}
