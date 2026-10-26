using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace CookHub.Services;

public interface ICurrentUser
{
    Task<string> GetUserIdAsync();
}

public sealed class CurrentUser(AuthenticationStateProvider authenticationStateProvider) : ICurrentUser
{
    private string? _userId;

    public async Task<string> GetUserIdAsync()
    {
        if (_userId is not null)
        {
            return _userId;
        }

        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        _userId = state.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No user is signed in.");
        return _userId;
    }
}
