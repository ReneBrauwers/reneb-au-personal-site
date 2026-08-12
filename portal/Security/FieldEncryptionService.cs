using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;

namespace ReneB.Portal.Security;

public sealed class FieldEncryptionService
{
    private readonly string _activeKeyId;
    private readonly IReadOnlyDictionary<string, byte[]> _keys;
    private readonly byte[] _lookupMaterial;

    public FieldEncryptionService(IOptions<EncryptionOptions> options, IWebHostEnvironment environment)
    {
        var configured = options.Value;
        KeyRingDocument keyRing;

        if (File.Exists(configured.KeyFile))
        {
            keyRing = JsonSerializer.Deserialize<KeyRingDocument>(File.ReadAllText(configured.KeyFile), JsonOptions)
                ?? throw new InvalidOperationException("The encryption keyring is empty.");
        }
        else if (environment.IsDevelopment() && configured.AllowDevelopmentKey)
        {
            keyRing = new KeyRingDocument
            {
                ActiveKeyId = "development-only",
                LookupMaterial = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("reneb-au-development-key-do-not-use-in-production"))),
                Keys = new Dictionary<string, string>
                {
                    ["development-only"] = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("reneb-au-development-key-do-not-use-in-production")))
                }
            };
        }
        else
        {
            throw new InvalidOperationException($"Encryption keyring not found at '{configured.KeyFile}'.");
        }

        if (string.IsNullOrWhiteSpace(keyRing.ActiveKeyId) || !keyRing.Keys.TryGetValue(keyRing.ActiveKeyId, out _))
        {
            throw new InvalidOperationException("The encryption keyring does not contain its active key.");
        }

        var decoded = keyRing.Keys.ToDictionary(pair => pair.Key, pair => Convert.FromBase64String(pair.Value), StringComparer.Ordinal);
        if (decoded.Values.Any(key => key.Length != 32))
        {
            throw new InvalidOperationException("Every field-encryption key must be exactly 32 bytes.");
        }
        var lookupMaterial = Convert.FromBase64String(keyRing.LookupMaterial);
        if (lookupMaterial.Length != 32)
        {
            throw new InvalidOperationException("The stable lookup key must be exactly 32 bytes.");
        }

        _activeKeyId = keyRing.ActiveKeyId;
        _keys = decoded;
        _lookupMaterial = lookupMaterial;
    }

    public string Encrypt(string? value) => Convert.ToBase64String(EncryptBytes(Encoding.UTF8.GetBytes(value ?? string.Empty)));

    public string Decrypt(string value) => Encoding.UTF8.GetString(DecryptBytes(Convert.FromBase64String(value)));

    public byte[] EncryptBytes(byte[] plaintext)
    {
        var keyId = Encoding.UTF8.GetBytes(_activeKeyId);
        if (keyId.Length > byte.MaxValue)
        {
            throw new InvalidOperationException("Encryption key identifier is too long.");
        }

        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_keys[_activeKeyId], tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, keyId);

        var result = new byte[2 + keyId.Length + nonce.Length + tag.Length + ciphertext.Length];
        result[0] = 1;
        result[1] = checked((byte)keyId.Length);
        keyId.CopyTo(result.AsSpan(2));
        nonce.CopyTo(result.AsSpan(2 + keyId.Length));
        tag.CopyTo(result.AsSpan(2 + keyId.Length + nonce.Length));
        ciphertext.CopyTo(result.AsSpan(2 + keyId.Length + nonce.Length + tag.Length));
        return result;
    }

    public byte[] DecryptBytes(byte[] encrypted)
    {
        if (encrypted.Length < 31 || encrypted[0] != 1)
        {
            throw new CryptographicException("Encrypted value has an unsupported format.");
        }

        var keyIdLength = encrypted[1];
        if (encrypted.Length < 30 + keyIdLength)
        {
            throw new CryptographicException("Encrypted value is truncated.");
        }

        var keyIdBytes = encrypted.AsSpan(2, keyIdLength).ToArray();
        var keyId = Encoding.UTF8.GetString(keyIdBytes);
        if (!_keys.TryGetValue(keyId, out var key))
        {
            throw new CryptographicException($"Encryption key '{keyId}' is unavailable.");
        }

        var nonceOffset = 2 + keyIdLength;
        var nonce = encrypted.AsSpan(nonceOffset, 12);
        var tag = encrypted.AsSpan(nonceOffset + 12, 16);
        var ciphertext = encrypted.AsSpan(nonceOffset + 28);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, keyIdBytes);
        return plaintext;
    }

    public string LookupHash(string value)
    {
        var normalized = Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant());
        using var hmac = new HMACSHA256(_lookupMaterial);
        return Convert.ToHexString(hmac.ComputeHash(normalized));
    }

    private sealed class KeyRingDocument
    {
        public string ActiveKeyId { get; set; } = string.Empty;
        [JsonPropertyName("lookupKey")]
        public string LookupMaterial { get; set; } = string.Empty;
        public Dictionary<string, string> Keys { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
