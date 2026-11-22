using CookHub.Data.Entities;
using CookHub.Services;

namespace CookHub.Tests;

public class MealPlanServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);

    [Fact]
    public async Task Entries_are_stored_against_the_Monday_and_sorted_by_day()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var tacos = await app.AddRecipeAsync("Tacos", ["tortillas"]);
        var soup = await app.AddRecipeAsync("Soup", ["carrot"]);
        var rice = await app.AddRecipeAsync("Rice", ["rice"]);

        // Any date in the week lands on that week.
        await app.MealPlans.AddAsync(tacos, new DateOnly(2026, 11, 1));
        await app.MealPlans.AddAsync(soup, Monday, DayOfWeek.Sunday);
        await app.MealPlans.AddAsync(rice, Monday, DayOfWeek.Monday);

        var week = await app.MealPlans.GetWeekAsync(Monday);

        Assert.Equal(["Rice", "Soup", "Tacos"], week.Select(w => w.Title));
        Assert.Empty(await app.MealPlans.GetWeekAsync(Monday.AddDays(7)));
    }

    [Fact]
    public async Task Entries_on_the_same_day_are_sorted_by_meal_and_can_be_moved_to_another_cell()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var pancakes = await app.AddRecipeAsync("Pancakes", ["flour"]);
        var tacos = await app.AddRecipeAsync("Tacos", ["tortillas"]);
        var salad = await app.AddRecipeAsync("Salad", ["lettuce"]);
        var soup = await app.AddRecipeAsync("Soup", ["carrot"]);

        await app.MealPlans.AddAsync(tacos, Monday, DayOfWeek.Tuesday, Meal.Dinner);
        await app.MealPlans.AddAsync(soup, Monday, DayOfWeek.Tuesday);
        await app.MealPlans.AddAsync(salad, Monday, DayOfWeek.Tuesday, Meal.Lunch);
        await app.MealPlans.AddAsync(pancakes, Monday, DayOfWeek.Tuesday, Meal.Breakfast);

        var week = await app.MealPlans.GetWeekAsync(Monday);
        Assert.Equal(["Pancakes", "Salad", "Tacos", "Soup"], week.Select(w => w.Title));
        Assert.False(week.Single(w => w.Title == "Soup").IsScheduled);

        // Two recipes can share a cell.
        var soupEntry = week.Single(w => w.Title == "Soup");
        await app.MealPlans.MoveAsync(soupEntry.EntryId, DayOfWeek.Tuesday, Meal.Dinner);
        await app.MealPlans.SetMealAsync(week.Single(w => w.Title == "Pancakes").EntryId, null);

        week = await app.MealPlans.GetWeekAsync(Monday);
        Assert.Equal([DayOfWeek.Tuesday, DayOfWeek.Tuesday], week.Where(w => w.Meal == Meal.Dinner).Select(w => w.Day));
        Assert.Null(week.Single(w => w.Title == "Pancakes").Meal);
    }

    [Fact]
    public async Task Entries_of_another_user_cannot_be_moved()
    {
        using var db = new TestDb();
        var dave = db.As(db.AddUser("Dave"));
        var eve = db.As(db.AddUser("Eve"));
        await dave.MealPlans.AddAsync(await dave.AddRecipeAsync("Tacos", ["tortillas"]), Monday);
        var entry = (await dave.MealPlans.GetWeekAsync(Monday)).Single();

        await eve.MealPlans.MoveAsync(entry.EntryId, DayOfWeek.Friday, Meal.Dinner);
        await eve.MealPlans.SetMealAsync(entry.EntryId, Meal.Lunch);

        var after = (await dave.MealPlans.GetWeekAsync(Monday)).Single();
        Assert.Null(after.Day);
        Assert.Null(after.Meal);
    }

    [Fact]
    public async Task Shopping_lines_cover_planned_recipes_and_flag_what_is_on_hand()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        var tacos = await app.AddRecipeAsync("Tacos", ["tortillas", "carrot"]);
        var soup = await app.AddRecipeAsync("Soup", ["carrot", "milk"]);
        await app.AddRecipeAsync("Not planned", ["saffron"]);
        await app.MealPlans.AddAsync(tacos, Monday);
        await app.MealPlans.AddAsync(soup, Monday);
        await app.SetOnHandAsync("milk");

        var lines = await app.MealPlans.GetShoppingLinesAsync(Monday);

        Assert.Equal(4, lines.Count);
        Assert.Equal(["carrot", "tortillas"], lines.Where(l => !l.OnHand).Select(l => l.IngredientName).Distinct());
        Assert.Equal(["milk"], lines.Where(l => l.OnHand).Select(l => l.IngredientName));
        Assert.DoesNotContain(lines, l => l.IngredientName == "saffron");
    }

    [Fact]
    public async Task Marking_an_ingredient_on_hand_takes_it_off_every_recipe_on_the_list()
    {
        using var db = new TestDb();
        var app = db.As(db.AddUser("Dave"));
        await app.MealPlans.AddAsync(await app.AddRecipeAsync("Tacos", ["carrot"]), Monday);
        await app.MealPlans.AddAsync(await app.AddRecipeAsync("Soup", ["carrot"]), Monday);
        var carrot = await app.Lookups.GetOrCreateIngredientAsync("carrot");

        await app.Pantry.SetOnHandAsync(carrot.Id, true);
        await app.Pantry.SetOnHandAsync(carrot.Id, true); // A double click is harmless.

        Assert.All(await app.MealPlans.GetShoppingLinesAsync(Monday), l => Assert.True(l.OnHand));
        Assert.Single(await app.Pantry.GetOnHandAsync());

        await app.Pantry.SetOnHandAsync(carrot.Id, false);
        Assert.Empty(await app.Pantry.GetOnHandAsync());
    }

    [Fact]
    public async Task On_hand_lists_are_per_user()
    {
        using var db = new TestDb();
        var dave = db.As(db.AddUser("Dave"));
        var other = db.As(db.AddUser("Other"));
        await dave.SetOnHandAsync("milk");

        Assert.Empty(await other.Pantry.GetOnHandAsync());
    }
}
