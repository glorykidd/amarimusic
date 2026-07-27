using AmariMusic.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AmariMusic.Tests;

public class AdminAuthValidatorTests
{
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "AmariMusic.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static IConfiguration BuildConfig(string? username, string? passwordHash)
    {
        var data = new Dictionary<string, string?>
        {
            ["AdminAuth:Username"] = username,
            ["AdminAuth:PasswordHash"] = passwordHash,
        };
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static IHostEnvironment BuildEnvironment(string environmentName) =>
        new FakeHostEnvironment { EnvironmentName = environmentName };

    [Fact]
    public void ValidateOrThrow_ThrowsWhenNotDevelopmentAndUsernameMissing()
    {
        var env = BuildEnvironment(Environments.Production);
        var config = BuildConfig(null, "somehash");

        Assert.Throws<InvalidOperationException>(() => AdminAuthValidator.ValidateOrThrow(env, config));
    }

    [Fact]
    public void ValidateOrThrow_ThrowsWhenNotDevelopmentAndPasswordHashMissing()
    {
        var env = BuildEnvironment(Environments.Production);
        var config = BuildConfig("admin", null);

        Assert.Throws<InvalidOperationException>(() => AdminAuthValidator.ValidateOrThrow(env, config));
    }

    [Fact]
    public void ValidateOrThrow_DoesNotThrowWhenNotDevelopmentAndBothSet()
    {
        var env = BuildEnvironment(Environments.Production);
        var config = BuildConfig("admin", "somehash");

        AdminAuthValidator.ValidateOrThrow(env, config);
    }

    [Fact]
    public void ValidateOrThrow_DoesNotThrowInDevelopmentEvenWhenBothMissing()
    {
        var env = BuildEnvironment(Environments.Development);
        var config = BuildConfig(null, null);

        AdminAuthValidator.ValidateOrThrow(env, config);
    }
}
