namespace CookHub.Data.Entities;

public class RecipeFamilyLike
{
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public int FamilyMemberId { get; set; }
    public FamilyMember? FamilyMember { get; set; }
}
