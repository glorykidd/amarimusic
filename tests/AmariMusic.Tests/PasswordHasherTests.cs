using AmariMusic.Services;

namespace AmariMusic.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHasher.Hash("correct-password");

        Assert.True(PasswordHasher.Verify("correct-password", hash));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("correct-password");

        Assert.False(PasswordHasher.Verify("wrong-password", hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_WithNullOrEmptyHash_ReturnsFalse(string? encodedHash)
    {
        Assert.False(PasswordHasher.Verify("anything", encodedHash));
    }

    [Theory]
    [InlineData("210000")]
    [InlineData("210000.onlyonepart")]
    [InlineData("210000.dGVzdA==.dGVzdA==.extra")]
    public void Verify_WithWrongPartCount_ReturnsFalse(string malformed)
    {
        Assert.False(PasswordHasher.Verify("anything", malformed));
    }

    [Theory]
    [InlineData("notanumber.dGVzdA==.dGVzdA==")]
    [InlineData("0.dGVzdA==.dGVzdA==")]
    [InlineData("-1.dGVzdA==.dGVzdA==")]
    [InlineData("2147483647.dGVzdA==.dGVzdA==")]
    public void Verify_WithInvalidIterations_ReturnsFalse(string malformed)
    {
        Assert.False(PasswordHasher.Verify("anything", malformed));
    }

    [Theory]
    [InlineData("210000.not-valid-base64!!.dGVzdA==")]
    [InlineData("210000.dGVzdA==.not-valid-base64!!")]
    public void Verify_WithInvalidBase64_ReturnsFalse(string malformed)
    {
        Assert.False(PasswordHasher.Verify("anything", malformed));
    }

    [Fact]
    public void Verify_WithEmptyFinalSegment_ReturnsFalse()
    {
        // Regression guard: an empty salt/hash segment must not make FixedTimeEquals
        // compare two zero-length arrays and report a match for any password.
        var hashWithEmptySalt = "210000..dGVzdA==";
        var hashWithEmptyDigest = "210000.dGVzdA==.";

        Assert.False(PasswordHasher.Verify("anything", hashWithEmptySalt));
        Assert.False(PasswordHasher.Verify("anything", hashWithEmptyDigest));
    }

    [Fact]
    public void Hash_ProducesDifferentOutputForSamePasswordDueToRandomSalt()
    {
        var first = PasswordHasher.Hash("same-password");
        var second = PasswordHasher.Hash("same-password");

        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify("same-password", first));
        Assert.True(PasswordHasher.Verify("same-password", second));
    }
}
