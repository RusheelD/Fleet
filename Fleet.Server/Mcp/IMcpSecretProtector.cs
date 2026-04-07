namespace Fleet.Server.Mcp;

public interface IMcpSecretProtector
{
    string Protect(string plaintext);
    string? Unprotect(string? protectedValue);
}
