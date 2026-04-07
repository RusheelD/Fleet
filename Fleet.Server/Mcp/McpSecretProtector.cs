using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Fleet.Server.Mcp;

public class McpSecretProtector : IMcpSecretProtector
{
    private const string StableSecretPrefix = "mcpsec1:";
    private readonly IDataProtector _protector;
    private readonly byte[]? _stableEncryptionKey;

    public McpSecretProtector(IDataProtectionProvider dataProtectionProvider, IConfiguration? configuration = null)
    {
        _protector = dataProtectionProvider.CreateProtector("Fleet.Mcp.Secret.v1");
        _stableEncryptionKey = BuildStableEncryptionKey(configuration);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            return string.Empty;

        if (_stableEncryptionKey is not null)
            return EncryptWithStableKey(plaintext);

        return _protector.Protect(plaintext);
    }

    public string? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
            return null;

        if (protectedValue.StartsWith(StableSecretPrefix, StringComparison.Ordinal))
            return DecryptWithStableKey(protectedValue);

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            return LooksLikeAspNetDataProtectionPayload(protectedValue)
                ? null
                : protectedValue;
        }
        catch (FormatException)
        {
            return LooksLikeAspNetDataProtectionPayload(protectedValue)
                ? null
                : protectedValue;
        }
    }

    private string EncryptWithStableKey(string plaintext)
    {
        if (_stableEncryptionKey is null)
            throw new InvalidOperationException("A stable MCP secret encryption key is not configured.");

        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aes = new AesGcm(_stableEncryptionKey, tagSizeInBytes: tag.Length);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

        return $"{StableSecretPrefix}{Convert.ToBase64String(payload)}";
    }

    private string? DecryptWithStableKey(string protectedValue)
    {
        if (_stableEncryptionKey is null)
            return null;

        try
        {
            var payload = Convert.FromBase64String(protectedValue[StableSecretPrefix.Length..]);
            var nonceLength = AesGcm.NonceByteSizes.MaxSize;
            var tagLength = AesGcm.TagByteSizes.MaxSize;

            if (payload.Length <= nonceLength + tagLength)
                return null;

            var nonce = payload[..nonceLength];
            var tag = payload[nonceLength..(nonceLength + tagLength)];
            var ciphertext = payload[(nonceLength + tagLength)..];
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(_stableEncryptionKey, tagSizeInBytes: tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[]? BuildStableEncryptionKey(IConfiguration? configuration)
    {
        var secret = configuration?["Mcp:SecretEncryptionKey"];
        if (string.IsNullOrWhiteSpace(secret))
            secret = configuration?["GitHub:TokenEncryptionKey"];
        if (string.IsNullOrWhiteSpace(secret))
            secret = configuration?["GitHub:ClientSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            return null;

        return SHA256.HashData(Encoding.UTF8.GetBytes($"Fleet.Mcp.Secret.v1|{secret.Trim()}"));
    }

    private static bool LooksLikeAspNetDataProtectionPayload(string value)
        => value.StartsWith("CfDJ8", StringComparison.Ordinal);
}
