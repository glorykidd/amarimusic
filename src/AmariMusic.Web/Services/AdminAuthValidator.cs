namespace AmariMusic.Services;

public static class AdminAuthValidator
{
    /// <summary>
    /// Throws if AdminAuth is unconfigured outside Development, so the app fails at
    /// startup instead of booting with an admin login nobody can (or, worse, anybody can) pass.
    /// </summary>
    public static void ValidateOrThrow(IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsDevelopment())
            return;

        var username = configuration["AdminAuth:Username"];
        var passwordHash = configuration["AdminAuth:PasswordHash"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new InvalidOperationException(
                "AdminAuth:Username and AdminAuth:PasswordHash must be set in appsettings.Production.json (or appsettings.{Environment}.json) before starting outside the Development environment.");
        }
    }
}
