using System.Text.Json;
using System.Text.Json.Serialization;
using CookHub.Data.Entities;

namespace CookHub.Services;

// Thrown with a message that is safe to show to the user.
public sealed class RecipeImportException(string message, Exception? inner = null) : Exception(message, inner);

// A recipe pasted as JSON on the New recipe page, in CookHub's shape.
public sealed class ImportedRecipe
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("ingredients")] public List<ImportedIngredient> Ingredients { get; set; } = [];
    [JsonPropertyName("steps")] public List<string> Steps { get; set; } = [];
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
}

public sealed class ImportedIngredient
{
    [JsonPropertyName("amount"), JsonConverter(typeof(StringOrNumberConverter))] public string Amount { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("preparation")] public string Preparation { get; set; } = "";
}

// Reads a JSON string or number as text, so "amount": 2 works as well as "amount": "2".
internal sealed class StringOrNumberConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.String => reader.GetString(),
        JsonTokenType.Number => System.Text.Encoding.UTF8.GetString(reader.ValueSpan),
        JsonTokenType.Null => "",
        _ => throw new JsonException("Expected text or a number."),
    };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}

// Converts recipe JSON pasted by the user into an edit model for the recipe form.
public sealed class RecipeImportService(LookupService lookups)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Example JSON shown in the text area, so the user can see the expected structure.
    public static string Template { get; } = JsonSerializer.Serialize(new ImportedRecipe
    {
        Title = "Street tacos",
        Description = "Quick weeknight chicken tacos.",
        Ingredients =
        [
            new() { Amount = "1 lb", Name = "chicken thigh", Preparation = "diced" },
            new() { Amount = "8", Name = "corn tortilla", Preparation = "warmed" },
            new() { Amount = "", Name = "salt", Preparation = "to taste" },
        ],
        Steps =
        [
            "Season the chicken with salt.",
            "Cook the chicken in a hot pan until browned.",
            "Serve in the warm tortillas.",
        ],
        Tags = ["mexican", "dinner", "chicken"],
    }, WriteOptions);

    // Parses the JSON and builds an edit model ready to review and save.
    // Ingredients and tags are looked up by name, and any new names are added to the shared catalog.
    public async Task<RecipeEditModel> ConvertAsync(string? json)
    {
        var recipe = Parse(json);
        return await ToEditModelAsync(recipe);
    }

    internal static ImportedRecipe Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new RecipeImportException("Paste some recipe JSON first.");
        }

        ImportedRecipe? recipe;
        try
        {
            recipe = JsonSerializer.Deserialize<ImportedRecipe>(json, ReadOptions);
        }
        catch (JsonException ex)
        {
            var where = ex.LineNumber is { } line ? $" (line {line + 1})" : "";
            var field = string.IsNullOrEmpty(ex.Path) || ex.Path == "$" ? "" : $" near {ex.Path.TrimStart('$', '.')}";
            throw new RecipeImportException($"That isn't valid recipe JSON{field}{where}. Compare it with the template.", ex);
        }

        if (recipe is null)
        {
            throw new RecipeImportException("That isn't valid recipe JSON. Compare it with the template.");
        }

        // A JSON null for a list leaves it null; treat it as empty.
        recipe.Ingredients ??= [];
        recipe.Steps ??= [];
        recipe.Tags ??= [];

        if (string.IsNullOrWhiteSpace(recipe.Title) && recipe.Ingredients.Count == 0 && recipe.Steps.Count == 0)
        {
            throw new RecipeImportException("No recipe found in that JSON. It needs at least a title, ingredients or steps.");
        }

        return recipe;
    }

    internal async Task<RecipeEditModel> ToEditModelAsync(ImportedRecipe recipe)
    {
        var model = new RecipeEditModel
        {
            Title = Clip(recipe.Title, Recipe.TitleMaxLength),
            Description = NullIfEmpty(Clip(recipe.Description, Recipe.DescriptionMaxLength)),
        };

        foreach (var line in recipe.Ingredients)
        {
            var name = Clip(LookupService.CleanName(line?.Name ?? ""), Ingredient.NameMaxLength);
            if (name.Length == 0)
            {
                continue;
            }

            model.Ingredients.Add(new RecipeEditModel.IngredientRow
            {
                Amount = NullIfEmpty(Clip(line!.Amount, RecipeIngredient.AmountMaxLength)),
                Ingredient = await lookups.GetOrCreateIngredientAsync(name),
                Preparation = NullIfEmpty(Clip(line.Preparation, RecipeIngredient.PreparationMaxLength)),
            });
        }

        model.Steps = recipe.Steps
            .Select(s => Clip(s, RecipeStep.InstructionMaxLength))
            .Where(s => s.Length > 0)
            .Select(s => new RecipeEditModel.StepRow { Instruction = s })
            .ToList();

        foreach (var tagName in recipe.Tags)
        {
            var name = Clip(LookupService.CleanName(tagName ?? ""), Tag.NameMaxLength);
            if (name.Length == 0)
            {
                continue;
            }

            var tag = await lookups.GetOrCreateTagAsync(name);
            if (model.Tags.All(t => t.Id != tag.Id))
            {
                model.Tags.Add(tag);
            }
        }

        // Leave a blank row to type into, as on a new recipe.
        if (model.Ingredients.Count == 0)
        {
            model.Ingredients.Add(new());
        }

        if (model.Steps.Count == 0)
        {
            model.Steps.Add(new());
        }

        return model;
    }

    private static string Clip(string? text, int maxLength)
    {
        var trimmed = text?.Trim() ?? "";
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd();
    }

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;
}
