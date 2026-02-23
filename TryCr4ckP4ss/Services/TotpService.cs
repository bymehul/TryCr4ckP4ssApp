using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace TryCr4ckP4ss.Services;

public static class TotpService
{
    private const int PeriodSeconds = 30;
    private const int Digits = 6;

    public static bool HasSecret(string? secret) => !string.IsNullOrWhiteSpace(secret);

    public static bool TryGenerateCode(string? secret, DateTimeOffset timestamp, out string code)
    {
        code = string.Empty;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        try
        {
            code = GenerateCode(secret, timestamp);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string GenerateCode(string secret, DateTimeOffset timestamp)
    {
        var key = DecodeBase32(secret);
        var counter = timestamp.ToUnixTimeSeconds() / PeriodSeconds;
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0f;
        var truncated =
            ((hash[offset] & 0x7f) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];

        var code = truncated % 1_000_000;
        CryptographicOperations.ZeroMemory(hash);
        CryptographicOperations.ZeroMemory(key);
        return code.ToString("D6");
    }

    public static int GetSecondsRemaining(DateTimeOffset timestamp)
    {
        var elapsed = (int)(timestamp.ToUnixTimeSeconds() % PeriodSeconds);
        return PeriodSeconds - elapsed;
    }

    private static byte[] DecodeBase32(string value)
    {
        var normalized = value
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .TrimEnd('=')
            .ToUpperInvariant();

        if (normalized.Length == 0)
        {
            throw new FormatException("TOTP secret is empty.");
        }

        var output = new List<byte>(normalized.Length * 5 / 8);
        var bitBuffer = 0;
        var bitCount = 0;

        foreach (var c in normalized)
        {
            var charValue = c switch
            {
                >= 'A' and <= 'Z' => c - 'A',
                >= '2' and <= '7' => c - '2' + 26,
                _ => -1
            };

            if (charValue < 0)
            {
                throw new FormatException("TOTP secret must be a valid Base32 string.");
            }

            bitBuffer = (bitBuffer << 5) | charValue;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bitCount -= 8;
                output.Add((byte)((bitBuffer >> bitCount) & 0xff));
            }
        }

        return [..output];
    }
}
