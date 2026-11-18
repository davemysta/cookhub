using CookHub.Services;

namespace CookHub.Tests;

public class RecipeImportServiceTests
{
    [Fact]
    public async Task ConvertAsync_fills_in_the_form_from_json()
    {
        using var db = new TestDb();
        var service = new RecipeImportService(new LookupService(db));

        var model = await service.ConvertAsync("""
            {
              "title": "Street tacos",
              "description": "Quick weeknight tacos.",
              "ingredients": [
                { "amount": "1 lb", "name": "chicken thigh", "preparation": "diced" },
                { "amount": "", "name": "salt", "preparation": "to taste" }
              ],
              "steps": ["Season the chicken.", "Cook until browned."],
              "tags": ["mexican", "dinner"]
            }
            """);

        Assert.Equal("Street tacos", model.Title);
        Assert.Equal("Quick weeknight tacos.", model.Description);
        Assert.Collection(model.Ingredients,
            row =>
            {
                Assert.Equal("1 lb", row.Amount);
                Assert.Equal("chicken thigh", row.Ingredient!.Name);
                Assert.Equal("diced", row.Preparation);
            },
            row =>
            {
                Assert.Null(row.Amount);
                Assert.Equal("salt", row.Ingredient!.Name);
                Assert.Equal("to taste", row.Preparation);
            });
        Assert.Equal(["Season the chicken.", "Cook until browned."], model.Steps.Select(s => s.Instruction));
        Assert.Equal(["mexican", "dinner"], model.Tags.Select(t => t.Name));
        Assert.Empty(model.Validate());
    }

    [Fact]
    public async Task ConvertAsync_accepts_the_template()
    {
        using var db = new TestDb();
        var service = new RecipeImportService(new LookupService(db));

        var model = await service.ConvertAsync(RecipeImportService.Template);

        Assert.Equal("Street tacos", model.Title);
        Assert.Equal(3, model.Ingredients.Count);
        Assert.Equal(3, model.Steps.Count);
        Assert.Empty(model.Validate());
    }

    [Fact]
    public async Task ConvertAsync_is_lenient_about_casing_comments_numbers_and_missing_fields()
    {
        using var db = new TestDb();
        var service = new RecipeImportService(new LookupService(db));

        var model = await service.ConvertAsync("""
            {
              // Only a title, ingredients and steps.
              "Title": "Toast",
              "Ingredients": [ { "amount": 2, "name": "bread" }, ],
              "Steps": ["Toast the bread."],
            }
            """);

        Assert.Equal("Toast", model.Title);
        Assert.Null(model.Description);
        var row = Assert.Single(model.Ingredients);
        Assert.Equal("2", row.Amount);
        Assert.Null(row.Preparation);
        Assert.Empty(model.Tags);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ConvertAsync_asks_for_json_when_empty(string? json)
    {
        using var db = new TestDb();
        var service = new RecipeImportService(new LookupService(db));

        var error = await Assert.ThrowsAsync<RecipeImportException>(() => service.ConvertAsync(json));

        Assert.Equal("Paste some recipe JSON first.", error.Message);
    }

    [Theory]
    [InlineData("{ \"title\": ")]
    [InlineData("not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"title\": \"Toast\", \"steps\": \"Toast the bread.\" }")]
    public async Task ConvertAsync_reports_invalid_json(string json)
    {
        using var db = new TestDb();
        var service = new RecipeImportService(new LookupService(db));

        var error = await Assert.ThrowsAsync<RecipeImportException>(() => service.ConvertAsync(json));

        Assert.StartsWith("That isn't valid recipe JSON", error.Message);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task ConvertAsync_reports_json_without_a_recipe(string json)
    {
        using var db = new TestDb();
        var service = new RecipeImportService(new LookupService(db));

        await Assert.ThrowsAsync<RecipeImportException>(() => service.ConvertAsync(json));
    }

    [Fact]
    public async Task ToEditModelAsync_clips_long_values_and_skips_blank_lines()
    {
        using var db = new TestDb();
        var service = new RecipeImportService(new LookupService(db));

        var model = await service.ToEditModelAsync(new ImportedRecipe
        {
            Title = new string('t', 300),
            Ingredients = [new() { Name = "  " }, new() { Amount = new string('1', 80), Name = "flour" }],
            Steps = ["", "Mix."],
            Tags = ["Baking", "baking"],
        });

        Assert.Equal(200, model.Title.Length);
        Assert.Null(model.Description);
        var row = Assert.Single(model.Ingredients);
        Assert.Equal(50, row.Amount!.Length);
        Assert.Equal(["Mix."], model.Steps.Select(s => s.Instruction));
        Assert.Equal(["baking"], model.Tags.Select(t => t.Name));
    }
}
