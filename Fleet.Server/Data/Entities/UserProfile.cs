namespace Fleet.Server.Data.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public string EntraObjectId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Role { get; set; } = Auth.UserRoles.Free;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Stored as JSON (jsonb) — demonstrates embedded document storage
    public UserPreferences Preferences { get; set; } = new();

    // Navigation
    public List<LinkedAccount> LinkedAccounts { get; set; } = [];
}
