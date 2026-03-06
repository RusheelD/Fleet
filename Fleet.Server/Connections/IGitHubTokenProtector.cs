namespace Fleet.Server.Connections;

/// <summary>
/// Protects and unprotects GitHub OAuth access tokens for storage at rest.
/// </summary>
public interface IGitHubTokenProtector
{
    string Protect(string token);
    string? Unprotect(string? protectedToken);
}
