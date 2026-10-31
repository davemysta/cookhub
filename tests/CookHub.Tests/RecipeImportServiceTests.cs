using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CookHub.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CookHub.Tests;

public class RecipeImportServiceTests
{
    private const string PageUrl = "https://www.example.com/recipes/tacos";

    [Theory]
    [InlineData("https://example.com/tacos", "https://example.com/tacos")]
    [InlineData("  example.com/tacos ", "https://example.com/tacos")]
    [InlineData("http://example.com/tacos", "http://example.com/tacos")]
    public void ParseUrl_accepts_web_addresses(string text, string expected)
    {
        Assert.Equal(expected, RecipeImportService.ParseUrl(text)?.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ftp://example.com/tacos")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url")]
    public void ParseUrl_rejects_everything_else(string? text)
    {
        Assert.Null(RecipeImportService.ParseUrl(text));
    }

    [Fact]
    public async Task ImportAsync_asks_Claude_to_fetch_the_page_and_fills_in_the_form()
    {
        using var db = new TestDb();
        var claude = new FakeClaude(Reply("end_turn", TextBlock(RecipeJson())));
        var service = CreateService(db, claude);

        var model = await service.ImportAsync(PageUrl);

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

        var request = Assert.Single(claude.Requests);
        Assert.Equal("claude-opus-5-5", request["model"]!.GetValue<string>());
        Assert.Equal("default", request["fallbacks"]!.GetValue<string>());
        Assert.Contains(PageUrl, request["messages"]![0]!["content"]!.ToJsonString());
        var tool = Assert.Single(request["tools"]!.AsArray())!;
        Assert.Equal("web_fetch_20260209", tool["type"]!.GetValue<string>());
        Assert.Equal(["example.com"], tool["allowed_domains"]!.AsArray().Select(d => d!.GetValue<string>()));
        Assert.Equal("json_schema", request["output_config"]!["format"]!["type"]!.GetValue<string>());
        Assert.Contains("server-side-fallback-2026-07-01", claude.BetaHeaders.Single());
    }

    [Fact]
    public async Task ImportAsync_resumes_a_paused_turn()
    {
        using var db = new TestDb();
        var claude = new FakeClaude(
            Reply("pause_turn", new JsonObject
            {
                ["type"] = "server_tool_use",
                ["id"] = "srvtoolu_1",
                ["name"] = "web_fetch",
                ["input"] = new JsonObject { ["url"] = PageUrl },
            }),
            Reply("end_turn", TextBlock(RecipeJson())));
        var service = CreateService(db, claude);

        var model = await service.ImportAsync(PageUrl);

        Assert.Equal("Street tacos", model.Title);
        Assert.Equal(2, claude.Requests.Count);
        var messages = claude.Requests[1]["messages"]!.AsArray();
        Assert.Equal(2, messages.Count);
        Assert.Equal("assistant", messages[1]!["role"]!.GetValue<string>());
        Assert.Equal("server_tool_use", messages[1]!["content"]![0]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task ImportAsync_reports_a_page_without_a_recipe()
    {
        using var db = new TestDb();
        var json = JsonSerializer.Serialize(new ImportedRecipe { Found = false, Problem = "The page is a login screen." });
        var service = CreateService(db, new FakeClaude(Reply("end_turn", TextBlock(json))));

        var error = await Assert.ThrowsAsync<RecipeImportException>(() => service.ImportAsync(PageUrl));

        Assert.Equal("Couldn't find a recipe on that page: The page is a login screen.", error.Message);
    }

    [Fact]
    public async Task ImportAsync_needs_an_api_key()
    {
        using var db = new TestDb();
        var claude = new FakeClaude();
        var service = CreateService(db, claude, apiKey: "");

        var error = await Assert.ThrowsAsync<RecipeImportException>(() => service.ImportAsync(PageUrl));

        Assert.Contains("API key", error.Message);
        Assert.Empty(claude.Requests);
    }

    [Fact]
    public async Task ToEditModelAsync_clips_long_values_and_skips_blank_lines()
    {
        using var db = new TestDb();
        var service = CreateService(db, new FakeClaude());

        var model = await service.ToEditModelAsync(new ImportedRecipe
        {
            Found = true,
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

    private static RecipeImportService CreateService(TestDb db, FakeClaude claude, string apiKey = "test-key") =>
        new(
            new HttpClient(claude),
            Options.Create(new AnthropicOptions { ApiKey = apiKey, BaseUrl = "https://claude.test" }),
            new LookupService(db),
            NullLogger<RecipeImportService>.Instance);

    private static string RecipeJson() => JsonSerializer.Serialize(new ImportedRecipe
    {
        Found = true,
        Title = "Street tacos",
        Description = "Quick weeknight tacos.",
        Ingredients =
        [
            new() { Amount = "1 lb", Name = "chicken thigh", Preparation = "diced" },
            new() { Amount = "", Name = "salt", Preparation = "to taste" },
        ],
        Steps = ["Season the chicken.", "Cook until browned."],
        Tags = ["mexican", "dinner"],
    });

    private static JsonObject TextBlock(string text) => new() { ["type"] = "text", ["text"] = text };

    private static string Reply(string stopReason, JsonObject block) => new JsonObject
    {
        ["id"] = "msg_1",
        ["type"] = "message",
        ["role"] = "assistant",
        ["model"] = "claude-opus-5-5",
        ["content"] = new JsonArray(block),
        ["stop_reason"] = stopReason,
        ["stop_sequence"] = null,
        ["usage"] = new JsonObject { ["input_tokens"] = 10, ["output_tokens"] = 10 },
    }.ToJsonString();

    // Stands in for the Claude API: records each request and answers with the next canned reply.
    private sealed class FakeClaude(params string[] replies) : HttpMessageHandler
    {
        private readonly Queue<string> _replies = new(replies);

        public List<JsonNode> Requests { get; } = [];
        public List<string> BetaHeaders { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!);
            if (request.Headers.TryGetValues("anthropic-beta", out var betas))
            {
                BetaHeaders.Add(string.Join(",", betas));
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_replies.Dequeue(), Encoding.UTF8, "application/json"),
            };
        }
    }
}
