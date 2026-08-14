using System.Buffers.Binary;
using System.Security.Cryptography;

namespace ReneB.Portal.Security;

public static class TotpService
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] CreateSecret() => RandomNumberGenerator.GetBytes(20);

    public static bool Validate(byte[] secret, string code, DateTimeOffset now)
    {
        if (code.Length != 6 || code.Any(value => value is < '0' or > '9'))
        {
            return false;
        }

        var supplied = int.Parse(code, System.Globalization.CultureInfo.InvariantCulture);
        var counter = now.ToUnixTimeSeconds() / 30;
        for (var offset = -1; offset <= 1; offset++)
        {
            if (Compute(secret, counter + offset) == supplied)
            {
                return true;
            }
        }

        return false;
    }

    public static string GenerateCode(byte[] secret, DateTimeOffset now)
        => Compute(secret, now.ToUnixTimeSeconds() / 30).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    public static string ToBase32(byte[] data)
    {
        var result = new System.Text.StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                result.Append(Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            result.Append(Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        }

        return result.ToString();
    }

    private static int Compute(byte[] secret, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return binary % 1_000_000;
    }
}
