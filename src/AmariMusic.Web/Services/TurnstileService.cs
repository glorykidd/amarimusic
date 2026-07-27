using System.Text.Json.Serialization;

namespace AmariMusic.Services;

public class TurnstileService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<TurnstileService> logger)
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(config["Turnstile:SecretKey"]);

    public async Task<bool> VerifyAsync(string? token, string? remoteIp)
    {
        var secretKey = config["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            logger.LogWarning("Turnstile:SecretKey not configured — rejecting submission that required verification");
            return false;
        }

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var client = httpClientFactory.CreateClient(nameof(TurnstileService));
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = secretKey,
                ["response"] = token,
                ["remoteip"] = remoteIp ?? string.Empty
            });

            using var response = await client.PostAsync(VerifyUrl, form);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TurnstileVerifyResponse>();
            return result?.Success ?? false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Turnstile verification request failed");
            return false;
        }
    }

    private class TurnstileVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}
