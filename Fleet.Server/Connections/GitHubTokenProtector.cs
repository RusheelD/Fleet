using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Fleet.Server.Connections;

public class GitHubTokenProtector(IDataProtectionProvider dataProtectionProvider) : IGitHubTokenProtector
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("Fleet.GitHub.AccessToken.v1");

    public string Protect(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token must not be empty.", nameof(token));

        return _protector.Protect(token);
    }

    public string? Unprotect(string? protectedToken)
    {
        if (string.IsNullOrWhiteSpace(protectedToken))
            return null;

        try
        {
            return _protector.Unprotect(protectedToken);
        }
        catch (CryptographicException)
        {
            // Backward compatibility for legacy rows stored in plaintext.
            return protectedToken;
        }
        catch (FormatException)
        {
            // Backward compatibility for legacy rows stored in plaintext.
            return protectedToken;
        }
    }
}
