namespace CookHub.Data.Entities;

public class FamilyMember
{
    public const int NameMaxLength = 100;

    public int Id { get; set; }

    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    public string Name { get; set; } = "";

    public List<RecipeFamilyLike> Likes { get; set; } = [];
}
