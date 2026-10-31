using CookHub.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CookHub.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<RecipeFamilyLike> RecipeFamilyLikes => Set<RecipeFamilyLike>();
    public DbSet<IngredientOnHand> IngredientsOnHand => Set<IngredientOnHand>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.DisplayName).HasMaxLength(100);
        });

        builder.Entity<Recipe>(b =>
        {
            b.Property(r => r.Title).HasMaxLength(Recipe.TitleMaxLength).IsRequired();
            b.Property(r => r.Description).HasMaxLength(Recipe.DescriptionMaxLength);
            b.HasIndex(r => new { r.OwnerId, r.Title });
            b.HasOne(r => r.Owner).WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(r => r.Image).WithOne(i => i.Recipe).HasForeignKey<RecipeImage>(i => i.RecipeId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(r => r.Tags).WithMany(t => t.Recipes).UsingEntity("RecipeTag");
        });

        builder.Entity<RecipeImage>(b =>
        {
            b.HasKey(i => i.RecipeId);
            b.Property(i => i.ContentType).HasMaxLength(100).IsRequired();
        });

        builder.Entity<Ingredient>(b =>
        {
            b.Property(i => i.Name).HasMaxLength(Ingredient.NameMaxLength).IsRequired();
            b.Property(i => i.NormalizedName).HasMaxLength(Ingredient.NameMaxLength).IsRequired();
            b.HasIndex(i => i.NormalizedName).IsUnique();
        });

        builder.Entity<Tag>(b =>
        {
            b.Property(t => t.Name).HasMaxLength(Tag.NameMaxLength).IsRequired();
            b.Property(t => t.NormalizedName).HasMaxLength(Tag.NameMaxLength).IsRequired();
            b.HasIndex(t => t.NormalizedName).IsUnique();
        });

        builder.Entity<RecipeIngredient>(b =>
        {
            b.Property(i => i.Amount).HasMaxLength(RecipeIngredient.AmountMaxLength);
            b.Property(i => i.Preparation).HasMaxLength(RecipeIngredient.PreparationMaxLength);
            b.HasOne(i => i.Recipe).WithMany(r => r.Ingredients).HasForeignKey(i => i.RecipeId).OnDelete(DeleteBehavior.Cascade);
            // Ingredients are shared, so one in use by a recipe can't be deleted.
            b.HasOne(i => i.Ingredient).WithMany().HasForeignKey(i => i.IngredientId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RecipeStep>(b =>
        {
            b.Property(s => s.Instruction).HasMaxLength(RecipeStep.InstructionMaxLength).IsRequired();
            b.HasOne(s => s.Recipe).WithMany(r => r.Steps).HasForeignKey(s => s.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FamilyMember>(b =>
        {
            b.Property(f => f.Name).HasMaxLength(FamilyMember.NameMaxLength).IsRequired();
            b.HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RecipeFamilyLike>(b =>
        {
            b.HasKey(l => new { l.RecipeId, l.FamilyMemberId });
            b.HasOne(l => l.Recipe).WithMany(r => r.FamilyLikes).HasForeignKey(l => l.RecipeId).OnDelete(DeleteBehavior.Cascade);
            // SQL Server rejects a second cascade path from the user (user -> family member -> like).
            // FamilyService deletes a member's likes itself before deleting the member.
            b.HasOne(l => l.FamilyMember).WithMany(f => f.Likes).HasForeignKey(l => l.FamilyMemberId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<IngredientOnHand>(b =>
        {
            b.HasKey(o => new { o.UserId, o.IngredientId });
            b.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(o => o.Ingredient).WithMany().HasForeignKey(o => o.IngredientId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MealPlanEntry>(b =>
        {
            b.HasOne(m => m.Recipe).WithMany(r => r.MealPlanEntries).HasForeignKey(m => m.RecipeId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(m => new { m.WeekStart, m.RecipeId });
        });
    }
}
