using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CookHub.Data;

public class ApplicationUser : IdentityUser
{
    // Shown in the "Liked by" list alongside family members.
    [PersonalData]
    [MaxLength(100)]
    public string DisplayName { get; set; } = "";
}
