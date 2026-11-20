namespace CookHub;

// Switches for features that are turned on or off when the app is built.
public static class FeatureFlags
{
    // When false, the "Create an account" link is hidden and /Account/Register returns 404 (for GET
    // and form posts alike), so nobody can sign up. Existing accounts can still log in.
    // Set it to true, publish, and restart the service to let people register again.
    public const bool RegistrationIsEnabled = false;
}
