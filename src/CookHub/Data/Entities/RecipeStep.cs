namespace CookHub.Data.Entities;

public class RecipeStep
{
    public const int InstructionMaxLength = 2000;

    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public int StepNumber { get; set; }
    public string Instruction { get; set; } = "";
}
