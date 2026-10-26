using CookHub.Data;
using CookHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CookHub.Services;

public sealed class FamilyService(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<LookupItem>> GetMembersAsync()
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FamilyMembers
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .Select(f => new LookupItem(f.Id, f.Name))
            .ToListAsync();
    }

    // The user followed by their family members: everyone who can appear in "Liked by".
    public async Task<IReadOnlyList<Person>> GetPeopleAsync()
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await GetPeopleAsync(db, userId);
    }

    internal static async Task<IReadOnlyList<Person>> GetPeopleAsync(ApplicationDbContext db, string userId)
    {
        var me = await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync();
        var members = await db.FamilyMembers
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .Select(f => new Person(f.Id, f.Name))
            .ToListAsync();
        return [new Person(null, string.IsNullOrWhiteSpace(me) ? "Me" : me), .. members];
    }

    public async Task<LookupItem> AddAsync(string name)
    {
        var clean = ValidateName(name);
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var member = new FamilyMember { UserId = userId, Name = clean };
        db.FamilyMembers.Add(member);
        await db.SaveChangesAsync();
        return new LookupItem(member.Id, member.Name);
    }

    public async Task RenameAsync(int id, string name)
    {
        var clean = ValidateName(name);
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.FamilyMembers
            .Where(f => f.Id == id && f.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Name, clean));
    }

    public async Task DeleteAsync(int id)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // The database doesn't cascade this one (see ApplicationDbContext), so remove the likes first.
        await db.RecipeFamilyLikes
            .Where(l => l.FamilyMemberId == id && l.FamilyMember!.UserId == userId)
            .ExecuteDeleteAsync();
        await db.FamilyMembers
            .Where(f => f.Id == id && f.UserId == userId)
            .ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }

    private static string ValidateName(string name)
    {
        var clean = LookupService.CleanName(name);
        if (clean.Length == 0)
        {
            throw new ArgumentException("A name is required.", nameof(name));
        }

        if (clean.Length > FamilyMember.NameMaxLength)
        {
            throw new ArgumentException($"Names can be at most {FamilyMember.NameMaxLength} characters.", nameof(name));
        }

        return clean;
    }
}
