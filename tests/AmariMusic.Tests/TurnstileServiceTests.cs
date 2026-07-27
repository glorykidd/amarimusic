using System.Net;
using AmariMusic.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AmariMusic.Tests;

public class TurnstileServiceTests
{
    private sealed class StubHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(content) });
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private static IConfiguration BuildConfig(string? secretKey) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Turnstile:SecretKey"] = secretKey,
        }).Build();

    private static TurnstileService BuildService(string? secretKey, HttpStatusCode statusCode = HttpStatusCode.OK, string content = """{"success":true}""") =>
        new(new StubHttpClientFactory(new StubHandler(statusCode, content)), BuildConfig(secretKey), NullLogger<TurnstileService>.Instance);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsConfigured_ReturnsFalseWhenSecretKeyMissing(string? secretKey)
    {
        var service = BuildService(secretKey);

        Assert.False(service.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsTrueWhenSecretKeySet()
    {
        var service = BuildService("some-secret");

        Assert.True(service.IsConfigured);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsFalseWhenSecretKeyMissing()
    {
        var service = BuildService(null);

        Assert.False(await service.VerifyAsync("some-token", "1.2.3.4"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_ReturnsFalseWhenTokenMissing(string? token)
    {
        var service = BuildService("some-secret");

        Assert.False(await service.VerifyAsync(token, "1.2.3.4"));
    }

    [Fact]
    public async Task VerifyAsync_ReturnsTrueWhenCloudflareReportsSuccess()
    {
        var service = BuildService("some-secret", HttpStatusCode.OK, """{"success":true}""");

        Assert.True(await service.VerifyAsync("valid-token", "1.2.3.4"));
    }

    [Fact]
    public async Task VerifyAsync_ReturnsFalseWhenCloudflareReportsFailure()
    {
        var service = BuildService("some-secret", HttpStatusCode.OK, """{"success":false,"error-codes":["invalid-input-response"]}""");

        Assert.False(await service.VerifyAsync("bad-token", "1.2.3.4"));
    }

    [Fact]
    public async Task VerifyAsync_ReturnsFalseWhenCloudflareRequestFails()
    {
        var service = BuildService("some-secret", HttpStatusCode.InternalServerError, "error");

        Assert.False(await service.VerifyAsync("some-token", "1.2.3.4"));
    }
}
