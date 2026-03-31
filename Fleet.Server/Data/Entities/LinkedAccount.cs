namespace Fleet.Server.Data.Entities;

public class LinkedAccount
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ConnectedAs { get; set; }
    public string? AccessToken { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAtUtc { get; set; }
    public string? ExternalUserId { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public bool IsPrimary { get; set; }

    // Foreign key
    public int UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
}
