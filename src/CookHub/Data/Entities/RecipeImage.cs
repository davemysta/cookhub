namespace CookHub.Data.Entities;

public class RecipeImage
{
    public const int MaxBytes = 5 * 1024 * 1024;

    // Only raster formats; SVG could carry script that would run on the app's own origin.
    public static IReadOnlySet<string> AllowedContentTypes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/gif", "image/webp" };

    public const string AcceptAttribute = ".jpg,.jpeg,.png,.gif,.webp";

    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = "";
}
