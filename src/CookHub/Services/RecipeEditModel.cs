using CookHub.Data.Entities;

namespace CookHub.Services;

public sealed class RecipeEditModel
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public List<LookupItem> Tags { get; set; } = [];
    public List<IngredientRow> Ingredients { get; set; } = [];
    public List<StepRow> Steps { get; set; } = [];
    public HashSet<int?> LikedBy { get; set; } = [];

    // Current image, if any, for display while editing.
    public string? ExistingImageUrl { get; set; }

    // Set when the user picks a new image.
    public byte[]? NewImageData { get; set; }
    public string? NewImageContentType { get; set; }
    public bool RemoveImage { get; set; }

    public sealed class IngredientRow
    {
        public string Amount { get; set; } = "";
        public LookupItem? Ingredient { get; set; }
        public string? Preparation { get; set; }
    }

    public sealed class StepRow
    {
        public string Instruction { get; set; } = "";
    }

    // Rows the user left completely blank are ignored rather than treated as errors.
    public IEnumerable<IngredientRow> FilledIngredients => Ingredients.Where(i =>
        !string.IsNullOrWhiteSpace(i.Amount) || i.Ingredient is not null || !string.IsNullOrWhiteSpace(i.Preparation));

    public IEnumerable<StepRow> FilledSteps => Steps.Where(s => !string.IsNullOrWhiteSpace(s.Instruction));

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Title))
        {
            errors.Add("A title is required.");
        }
        else if (Title.Trim().Length > Recipe.TitleMaxLength)
        {
            errors.Add($"The title can be at most {Recipe.TitleMaxLength} characters.");
        }

        if (Description?.Trim().Length > Recipe.DescriptionMaxLength)
        {
            errors.Add($"The description can be at most {Recipe.DescriptionMaxLength} characters.");
        }

        var ingredients = FilledIngredients.ToList();
        if (ingredients.Count == 0)
        {
            errors.Add("Add at least one ingredient.");
        }

        for (var i = 0; i < ingredients.Count; i++)
        {
            var row = ingredients[i];
            if (string.IsNullOrWhiteSpace(row.Amount))
            {
                errors.Add($"Ingredient {i + 1}: an amount is required.");
            }
            else if (row.Amount.Trim().Length > RecipeIngredient.AmountMaxLength)
            {
                errors.Add($"Ingredient {i + 1}: the amount can be at most {RecipeIngredient.AmountMaxLength} characters.");
            }

            if (row.Ingredient is null)
            {
                errors.Add($"Ingredient {i + 1}: look up and select an ingredient.");
            }

            if (row.Preparation?.Trim().Length > RecipeIngredient.PreparationMaxLength)
            {
                errors.Add($"Ingredient {i + 1}: the preparation can be at most {RecipeIngredient.PreparationMaxLength} characters.");
            }
        }

        var steps = FilledSteps.ToList();
        if (steps.Count == 0)
        {
            errors.Add("Add at least one step.");
        }

        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Instruction.Trim().Length > RecipeStep.InstructionMaxLength)
            {
                errors.Add($"Step {i + 1}: can be at most {RecipeStep.InstructionMaxLength} characters.");
            }
        }

        if (NewImageData is { Length: > RecipeImage.MaxBytes })
        {
            errors.Add($"The image can be at most {RecipeImage.MaxBytes / 1024 / 1024} MB.");
        }

        if (NewImageData is not null && !RecipeImage.AllowedContentTypes.Contains(NewImageContentType ?? ""))
        {
            errors.Add("The photo must be a JPEG, PNG, GIF or WebP image.");
        }

        return errors;
    }
}
