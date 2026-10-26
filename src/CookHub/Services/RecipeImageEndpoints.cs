using System.Security.Claims;

namespace CookHub.Services;

public static class RecipeImageEndpoints
{
    public static IEndpointConventionBuilder MapRecipeImages(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/recipe-images/{id:int}", async (int id, ClaimsPrincipal user, RecipeService recipes, HttpContext context) =>
            {
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                var image = userId is null ? null : await recipes.GetImageAsync(id, userId);
                if (image is null)
                {
                    return Results.NotFound();
                }

                // URLs carry a version that changes when the recipe is saved, so they can be cached for a long time.
                context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
                context.Response.Headers.XContentTypeOptions = "nosniff";
                return Results.File(image.Value.Data, image.Value.ContentType);
            })
            .RequireAuthorization();
}
