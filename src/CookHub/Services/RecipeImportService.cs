using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using CookHub.Data.Entities;
using Microsoft.Extensions.Options;

namespace CookHub.Services;

// Settings from the "Anthropic" configuration section: user secrets in development,
// an environment variable or appsettings on the server. See README.md.
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-5-5";

    // Only needed to point at a different endpoint, e.g. in tests.
    public string? BaseUrl { get; set; }
}

// Thrown with a message that is safe to show to the user.
public sealed class RecipeImportException(string message, Exception? inner = null) : Exception(message, inner);

// What Claude returns for a recipe page, already in CookHub's shape.
public sealed class ImportedRecipe
{
    [JsonPropertyName("found")] public bool Found { get; set; }
    [JsonPropertyName("problem")] public string Problem { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("ingredients")] public List<ImportedIngredient> Ingredients { get; set; } = [];
    [JsonPropertyName("steps")] public List<string> Steps { get; set; } = [];
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
}

public sealed class ImportedIngredient
{
    [JsonPropertyName("amount")] public string Amount { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("preparation")] public string Preparation { get; set; } = "";
}

// Asks Claude to read a recipe web page (with its web fetch tool) and convert it to CookHub's format.
public sealed class RecipeImportService(
    HttpClient httpClient, IOptions<AnthropicOptions> options, LookupService lookups, ILogger<RecipeImportService> logger)
{
    // Server-side tool loops can pause; resume a few times before giving up.
    private const int MaxContinuations = 3;

    private static readonly string SystemPrompt = $"""
        You convert recipes from web pages into CookHub, a personal recipe box.

        Fetch the page at the URL the user gives you with the web_fetch tool and find the main recipe on it.
        Ignore ads, comments, reviews, related recipes and the author's life story.

        Return the recipe in this format:
        - title: the recipe's name, at most {Recipe.TitleMaxLength} characters.
        - description: one or two sentences describing the dish, at most {Recipe.DescriptionMaxLength} characters.
          Use the page's own summary if it has one; otherwise write a short one. Empty if there is nothing to go on.
        - ingredients: one entry per ingredient line, in the page's order. Split each line into:
          - amount: quantity and unit, e.g. "1 cup", "2", "1/2 tsp", "1 (14 oz) can", at most {RecipeIngredient.AmountMaxLength} characters.
            Use an empty string when there is no amount, e.g. "salt and pepper to taste" or "oil for frying".
          - name: the ingredient itself, singular and lower case where natural, e.g. "carrot", "all-purpose flour",
            "olive oil", at most {Ingredient.NameMaxLength} characters. No quantities or preparation in the name.
          - preparation: how it is prepared, e.g. "diced", "finely chopped", "softened", "to taste", "divided",
            at most {RecipeIngredient.PreparationMaxLength} characters. Empty string if none.
          Skip section headings such as "For the sauce:".
        - steps: the method, one instruction per step, in order, without step numbers. Keep the page's wording where
          you can; split a very long paragraph into separate steps.
        - tags: 1 to 5 short lower-case tags useful for finding the recipe, such as the cuisine, the course and the main
          ingredient, e.g. "mexican", "dinner", "chicken", "vegetarian". At most {Tag.NameMaxLength} characters each.

        Set found to true when you found a recipe. If the page can't be fetched or has no recipe, set found to false,
        explain why in problem in one short sentence, and leave the other fields empty. Never invent a recipe that isn't on the page.
        """;

    // Structured outputs require every property to be listed as required and no extra properties.
    private static readonly Dictionary<string, JsonElement> Schema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "found", "problem", "title", "description", "ingredients", "steps", "tags" }),
        ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["found"] = new { type = "boolean" },
            ["problem"] = new { type = "string" },
            ["title"] = new { type = "string" },
            ["description"] = new { type = "string" },
            ["ingredients"] = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "amount", "name", "preparation" },
                    properties = new
                    {
                        amount = new { type = "string" },
                        name = new { type = "string" },
                        preparation = new { type = "string" },
                    },
                },
            },
            ["steps"] = new { type = "array", items = new { type = "string" } },
            ["tags"] = new { type = "array", items = new { type = "string" } },
        }),
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    // Returns null when the text isn't an absolute http(s) URL.
    public static Uri? ParseUrl(string? text)
    {
        var trimmed = text?.Trim() ?? "";
        if (trimmed.Length > 0 && !trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = "https://" + trimmed;
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;
    }

    // Fetches and converts the recipe, then builds an edit model ready to review and save.
    // Ingredients and tags are looked up by name, and any new names are added to the shared catalog.
    public async Task<RecipeEditModel> ImportAsync(string url, CancellationToken cancellationToken = default)
    {
        var uri = ParseUrl(url) ?? throw new RecipeImportException("Please enter a full web address, e.g. https://example.com/my-recipe.");
        var recipe = await FetchAsync(uri, cancellationToken);
        return await ToEditModelAsync(recipe);
    }

    internal async Task<ImportedRecipe> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new RecipeImportException("Adding recipes from a link needs a Claude API key. See README.md to set one up.");
        }

        var client = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? new AnthropicClient { ApiKey = settings.ApiKey, HttpClient = httpClient }
            : new AnthropicClient { ApiKey = settings.ApiKey, HttpClient = httpClient, BaseUrl = settings.BaseUrl };
        // Keep the fetch tool on the recipe's own site (subdomains included), so text on the page can't send it elsewhere.
        var domain = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;

        List<BetaMessageParam> messages =
        [
            new BetaUserMessageParam
            {
                Content = $"Get the recipe from this page and convert it to CookHub's format: {uri.AbsoluteUri}",
            },
        ];

        try
        {
            for (var attempt = 0; ; attempt++)
            {
                var response = await client.Beta.Messages.Create(new MessageCreateParams
                {
                    Model = settings.Model,
                    MaxTokens = 16000,
                    System = SystemPrompt,
                    Messages = messages,
                    Tools =
                    [
                        new BetaWebFetchTool20260209 { AllowedDomains = [domain], MaxUses = 3 },
                    ],
                    OutputConfig = new BetaOutputConfig
                    {
                        Effort = Effort.Medium,
                        Format = new BetaJsonOutputFormat { Schema = Schema },
                    },
                    // If a safety classifier declines the request, let the API retry it on a suitable model.
                    Betas = ["server-side-fallback-2026-07-01"],
                    Fallbacks = new Default(),
                }, cancellationToken);

                if (response.StopReason == BetaStopReason.PauseTurn && attempt < MaxContinuations)
                {
                    // The server paused its tool loop; send the partial turn back and it carries on.
                    messages.Add(new BetaAssistantMessageParam { Content = response.Content.Select(ToParam).ToList() });
                    continue;
                }

                if (response.StopReason == BetaStopReason.Refusal)
                {
                    throw new RecipeImportException("Claude declined to read that page.");
                }

                if (response.StopReason == BetaStopReason.MaxTokens)
                {
                    throw new RecipeImportException("That recipe was too long to convert.");
                }

                var json = string.Concat(response.Content.Select(b => b.TryPickText(out var text) ? text.Text : ""));
                var recipe = Parse(json);
                if (!recipe.Found)
                {
                    throw new RecipeImportException(string.IsNullOrWhiteSpace(recipe.Problem)
                        ? "Couldn't find a recipe on that page."
                        : $"Couldn't find a recipe on that page: {recipe.Problem.Trim()}");
                }

                return recipe;
            }
        }
        catch (AnthropicUnauthorizedException ex)
        {
            throw new RecipeImportException("The Claude API key was rejected. Check the key in your settings.", ex);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new RecipeImportException("Claude is busy right now. Please try again in a minute.", ex);
        }
        catch (AnthropicApiException ex)
        {
            logger.LogWarning(ex, "Claude API request failed while importing {Url}", uri);
            throw new RecipeImportException("Couldn't reach Claude. Please try again.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Network error while importing {Url}", uri);
            throw new RecipeImportException("Couldn't reach Claude. Check the server's internet connection.", ex);
        }
    }

    internal static ImportedRecipe Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ImportedRecipe>(json) ?? throw new JsonException("Empty response.");
        }
        catch (JsonException ex)
        {
            throw new RecipeImportException("Claude's answer couldn't be read. Please try again.", ex);
        }
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
            var name = Clip(LookupService.CleanName(line.Name), Ingredient.NameMaxLength);
            if (name.Length == 0)
            {
                continue;
            }

            model.Ingredients.Add(new RecipeEditModel.IngredientRow
            {
                Amount = NullIfEmpty(Clip(line.Amount, RecipeIngredient.AmountMaxLength)),
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
            var name = Clip(LookupService.CleanName(tagName), Tag.NameMaxLength);
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

    // Turns a response block back into a request block, to resume a paused turn.
    private static BetaContentBlockParam ToParam(BetaContentBlock block) => block.Value switch
    {
        BetaTextBlock text => new BetaTextBlockParam { Text = text.Text },
        _ => JsonSerializer.Deserialize<BetaContentBlockParam>(JsonSerializer.Serialize(block))!,
    };
}
