using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;

namespace ReneB.Portal.Security;

public sealed class AiCredentialEncryptionService
{
    private readonly string _activeKeyId;
    private readonly IReadOnlyDictionary<string, byte[]> _keys;

    public AiCredentialEncryptionService(IOptions<AiCredentialEncryptionOptions> options, IWebHostEnvironment environment)
    {
        KeyRingDocument ring;
        if (File.Exists(options.Value.KeyFile))
        {
            ring = JsonSerializer.Deserialize<KeyRingDocument>(File.ReadAllText(options.Value.KeyFile), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("The AI credential keyring is empty.");
        }
        else if (environment.IsDevelopment() && options.Value.AllowDevelopmentKey)
        {
            ring = new KeyRingDocument
            {
                ActiveKeyId = "development-only",
                Keys = new() { ["development-only"] = Convert.ToBase64String(SHA256.HashData("reneb-ai-credential-development-only"u8.ToArray())) }
            };
        }
        else throw new InvalidOperationException($"AI credential keyring not found at '{options.Value.KeyFile}'.");

        if (string.IsNullOrWhiteSpace(ring.ActiveKeyId) || !ring.Keys.ContainsKey(ring.ActiveKeyId))
            throw new InvalidOperationException("The AI credential keyring does not contain its active key.");
        _activeKeyId = ring.ActiveKeyId;
        _keys = ring.Keys.ToDictionary(pair => pair.Key, pair => Convert.FromBase64String(pair.Value), StringComparer.Ordinal);
        if (_keys.Values.Any(key => key.Length != 32)) throw new InvalidOperationException("Every AI credential key must be exactly 32 bytes.");
    }

    public string Encrypt(string value)
    {
        var keyId = Encoding.UTF8.GetBytes(_activeKeyId);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_keys[_activeKeyId], tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, keyId);
        var result = new byte[2 + keyId.Length + 12 + 16 + ciphertext.Length];
        result[0] = 1; result[1] = checked((byte)keyId.Length);
        keyId.CopyTo(result.AsSpan(2)); nonce.CopyTo(result.AsSpan(2 + keyId.Length)); tag.CopyTo(result.AsSpan(14 + keyId.Length)); ciphertext.CopyTo(result.AsSpan(30 + keyId.Length));
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encryptedValue)
    {
        var encrypted = Convert.FromBase64String(encryptedValue);
        if (encrypted.Length < 31 || encrypted[0] != 1) throw new CryptographicException("AI credential has an unsupported encrypted format.");
        var keyIdLength = encrypted[1];
        if (encrypted.Length < 30 + keyIdLength) throw new CryptographicException("AI credential is truncated.");
        var keyIdBytes = encrypted.AsSpan(2, keyIdLength).ToArray();
        var keyId = Encoding.UTF8.GetString(keyIdBytes);
        if (!_keys.TryGetValue(keyId, out var key)) throw new CryptographicException($"AI credential key '{keyId}' is unavailable.");
        var plaintext = new byte[encrypted.Length - 30 - keyIdLength];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(encrypted.AsSpan(2 + keyIdLength, 12), encrypted.AsSpan(30 + keyIdLength), encrypted.AsSpan(14 + keyIdLength, 16), plaintext, keyIdBytes);
        return Encoding.UTF8.GetString(plaintext);
    }

    public static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];

    private sealed class KeyRingDocument
    {
        public string ActiveKeyId { get; set; } = string.Empty;
        public Dictionary<string, string> Keys { get; set; } = [];
    }
}
