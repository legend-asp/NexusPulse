namespace NexusPulse.Infrastructure.Security;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

public sealed class TotpService
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private readonly ConcurrentDictionary<string, (int FailedCount, DateTime LockoutUntil)> _rateLimits = new();
    private readonly ConcurrentDictionary<string, long> _lastUsedSteps = new();

    public bool IsRateLimited(string clientIp, out int retryAfterSeconds)
    {
        if (_rateLimits.TryGetValue(clientIp, out var entry) && entry.LockoutUntil > DateTime.UtcNow)
        {
            retryAfterSeconds = (int)Math.Ceiling((entry.LockoutUntil - DateTime.UtcNow).TotalSeconds);
            return true;
        }

        retryAfterSeconds = 0;
        return false;
    }

    public void RecordFailedAttempt(string clientIp)
    {
        _rateLimits.AddOrUpdate(
            clientIp,
            _ => (1, DateTime.MinValue),
            (_, current) =>
            {
                if (current.LockoutUntil > DateTime.UtcNow)
                    return current;

                var nextCount = current.LockoutUntil != DateTime.MinValue ? 1 : current.FailedCount + 1;
                var lockout = nextCount >= 5 ? DateTime.UtcNow.AddMinutes(2) : DateTime.MinValue;
                return (nextCount, lockout);
            });
    }

    public void ClearFailedAttempts(string clientIp) => _rateLimits.TryRemove(clientIp, out _);

    public bool ValidatePassword(string? configuredPassword, string? providedPassword)
    {
        if (string.IsNullOrEmpty(configuredPassword))
            return true;

        if (string.IsNullOrEmpty(providedPassword))
            return false;

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredPassword));
        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedPassword));
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    public bool ValidateTotpCode(
        string secretBase32,
        string? code,
        string userKey = "admin",
        DateTimeOffset? utcNow = null,
        int allowedDriftSteps = 1,
        bool enforceReplayProtection = true)
    {
        if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(code))
            return false;

        var trimmedCode = code.Trim().Replace(" ", "");
        if (trimmedCode.Length != 6 || !trimmedCode.All(char.IsAsciiDigit))
            return false;

        var keyBytes = DecodeBase32(secretBase32);
        if (keyBytes.Length == 0)
            return false;

        var now = utcNow ?? DateTimeOffset.UtcNow;
        var currentStep = now.ToUnixTimeSeconds() / 30;
        var providedBytes = Encoding.ASCII.GetBytes(trimmedCode);

        for (int offset = -allowedDriftSteps; offset <= allowedDriftSteps; offset++)
        {
            long candidateStep = currentStep + offset;
            if (enforceReplayProtection && _lastUsedSteps.TryGetValue(userKey, out var lastStep) && candidateStep <= lastStep)
            {
                continue;
            }

            var expectedCode = ComputeTotpCode(keyBytes, candidateStep);
            var expectedBytes = Encoding.ASCII.GetBytes(expectedCode);

            if (CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
            {
                if (enforceReplayProtection)
                {
                    _lastUsedSteps[userKey] = candidateStep;
                }
                return true;
            }
        }

        return false;
    }

    public static string ComputeTotpCode(byte[] key, long timeStep)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        for (int i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(timeStep & 0xFF);
            timeStep >>= 8;
        }

        using var hmac = new HMACSHA1(key);
        byte[] hash = hmac.ComputeHash(counterBytes.ToArray());

        int offset = hash[^1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        int otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    public static byte[] DecodeBase32(string base32)
    {
        var cleaned = base32.Trim().Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();
        if (cleaned.Length == 0)
            return [];

        int byteCount = cleaned.Length * 5 / 8;
        var result = new byte[byteCount];
        int buffer = 0;
        int bitsLeft = 0;
        int index = 0;

        foreach (char c in cleaned)
        {
            int val = Base32Alphabet.IndexOf(c);
            if (val < 0)
                return [];

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                result[index++] = (byte)(buffer >> (bitsLeft - 8));
                bitsLeft -= 8;
            }
        }

        return result;
    }

    public string CreateSessionToken(string username, string sessionSecret, TimeSpan lifetime)
    {
        long expiresAt = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        string payload = $"{username}|{expiresAt}";
        string sig = ComputeHmacHex(payload, sessionSecret);
        string raw = $"{payload}|{sig}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public bool ValidateSessionToken(string? token, string sessionSecret, out string? username)
    {
        username = null;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sessionSecret))
            return false;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decoded.Split('|');
            if (parts.Length != 3)
                return false;

            if (!long.TryParse(parts[1], out long expiresAt))
                return false;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAt)
                return false;

            string payload = $"{parts[0]}|{parts[1]}";
            string expectedSig = ComputeHmacHex(payload, sessionSecret);

            var expectedBytes = Encoding.UTF8.GetBytes(expectedSig);
            var actualBytes = Encoding.UTF8.GetBytes(parts[2]);

            if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
                return false;

            username = parts[0];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeHmacHex(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        return Convert.ToHexString(hash);
    }
}
