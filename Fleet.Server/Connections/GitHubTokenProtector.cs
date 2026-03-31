using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Fleet.Server.Connections;

public class GitHubTokenProtector : IGitHubTokenProtector
{
    private const string StableTokenPrefix = "ghep1:";
    private readonly IDataProtector _protector;
    private readonly byte[]? _stableEncryptionKey;

    public GitHubTokenProtector(IDataProtectionProvider dataProtectionProvider, IConfiguration? configuration = null)
    {
        _protector = dataProtectionProvider.CreateProtector("Fleet.GitHub.AccessToken.v1");
        _stableEncryptionKey = BuildStableEncryptionKey(configuration);
    }

    public string Protect(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token must not be empty.", nameof(token));

        if (_stableEncryptionKey is not null)
            return EncryptWithStableKey(token);

        return _protector.Protect(token);
    }

    public string? Unprotect(string? protectedToken)
    {
        if (string.IsNullOrWhiteSpace(protectedToken))
            return null;

        if (protectedToken.StartsWith(StableTokenPrefix, StringComparison.Ordinal))
            return DecryptWithStableKey(protectedToken);

        try
        {
            return _protector.Unprotect(protectedToken);
        }
        catch (CryptographicException)
        {
            return LooksLikeAspNetDataProtectionPayload(protectedToken)
                ? null
                : protectedToken;
        }
        catch (FormatException)
        {
            return LooksLikeAspNetDataProtectionPayload(protectedToken)
                ? null
                : protectedToken;
        }
    }

    private string EncryptWithStableKey(string token)
    {
        if (_stableEncryptionKey is null)
            throw new InvalidOperationException("A stable GitHub token encryption key is not configured.");

        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var plaintext = Encoding.UTF8.GetBytes(token);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aes = new AesGcm(_stableEncryptionKey, tagSizeInBytes: tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

        return $"{StableTokenPrefix}{Convert.ToBase64String(payload)}";
    }

    private string? DecryptWithStableKey(string protectedToken)
    {
        if (_stableEncryptionKey is null)
            return null;

        try
        {
            var payload = Convert.FromBase64String(protectedToken[StableTokenPrefix.Length..]);
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
        var secret = configuration?["GitHub:TokenEncryptionKey"];
        if (string.IsNullOrWhiteSpace(secret))
            secret = configuration?["GitHub:ClientSecret"];

        if (string.IsNullOrWhiteSpace(secret))
            return null;

        return SHA256.HashData(Encoding.UTF8.GetBytes($"Fleet.GitHub.AccessToken.v2|{secret.Trim()}"));
    }

    private static bool LooksLikeAspNetDataProtectionPayload(string value)
        => value.StartsWith("CfDJ8", StringComparison.Ordinal);
}
