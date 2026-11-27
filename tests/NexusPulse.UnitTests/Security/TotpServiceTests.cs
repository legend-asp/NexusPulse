namespace NexusPulse.UnitTests.Security;

using FluentAssertions;
using NexusPulse.Infrastructure.Security;
using Xunit;

public class TotpServiceTests
{
    // RFC 6238 Test Secret ("12345678901234567890" -> Base32 "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ")
    private const string RfcTestSecretBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "287082")]         // T = 1 -> 94287082 -> last 6 digits 287082
    [InlineData(1111111109L, "081804")] // T = 37037036 -> 07081804 -> 081804
    [InlineData(1234567890L, "005924")] // T = 41152263 -> 89005924 -> 005924
    public void ValidateTotpCode_WithRfc6238Vectors_ShouldReturnTrue(long unixSeconds, string expected6Digits)
    {
        var service = new TotpService();
        var utcTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        bool result = service.ValidateTotpCode(
            RfcTestSecretBase32,
            expected6Digits,
            userKey: "rfc-user",
            utcNow: utcTime,
            allowedDriftSteps: 0,
            enforceReplayProtection: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateTotpCode_WhenReusedWithinSameStep_ShouldRejectReplay()
    {
        var service = new TotpService();
        var utcTime = DateTimeOffset.FromUnixTimeSeconds(1700000000L);
        var validCode = TotpService.ComputeTotpCode(TotpService.DecodeBase32(RfcTestSecretBase32), 1700000000L / 30);

        bool firstUse = service.ValidateTotpCode(RfcTestSecretBase32, validCode, "admin", utcTime);
        bool secondUse = service.ValidateTotpCode(RfcTestSecretBase32, validCode, "admin", utcTime);

        firstUse.Should().BeTrue();
        secondUse.Should().BeFalse();
    }

    [Fact]
    public void SessionToken_CreateAndValidate_ShouldVerifySignatureAndRejectTampering()
    {
        var service = new TotpService();
        const string secret = "super-secret-signing-key-32bytes!";

        var token = service.CreateSessionToken("admin", secret, TimeSpan.FromHours(1));
        bool valid = service.ValidateSessionToken(token, secret, out var username);
        bool tampered = service.ValidateSessionToken(token + "X", secret, out _);
        bool wrongSecret = service.ValidateSessionToken(token, "wrong-secret", out _);

        valid.Should().BeTrue();
        username.Should().Be("admin");
        tampered.Should().BeFalse();
        wrongSecret.Should().BeFalse();
    }
}
