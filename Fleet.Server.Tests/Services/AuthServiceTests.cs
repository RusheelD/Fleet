using System.Security.Claims;
using Fleet.Server.Auth;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AuthServiceTests
{
    private Mock<IAuthRepository> _authRepo = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessor = null!;
    private Mock<ILogger<AuthService>> _logger = null!;
    private IConfiguration _configuration = null!;
    private AuthService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _authRepo = new Mock<IAuthRepository>();
        _httpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new Mock<ILogger<AuthService>>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        _authRepo.Setup(r => r.GetLoginIdentityAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((LoginIdentity?)null);
        _authRepo.Setup(r => r.GetLoginIdentitiesAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<LoginIdentity>());
        _authRepo.Setup(r => r.CreateLoginIdentityAsync(It.IsAny<LoginIdentity>()))
            .ReturnsAsync((LoginIdentity identity) =>
            {
                identity.Id = identity.Id == 0 ? 1 : identity.Id;
                return identity;
            });
        _authRepo.Setup(r => r.UpdateLoginIdentityAsync(It.IsAny<LoginIdentity>()))
            .Returns(Task.CompletedTask);
        _authRepo.Setup(r => r.DeleteLoginIdentityAsync(It.IsAny<LoginIdentity>()))
            .Returns(Task.CompletedTask);
        _authRepo.Setup(r => r.CountLoginIdentitiesAsync(It.IsAny<int>()))
            .ReturnsAsync(1);
        _sut = new AuthService(_authRepo.Object, _httpContextAccessor.Object, _configuration, _logger.Object);
    }

    private void SetupHttpContext(string? oid = null, string? name = null, string? email = null, string? provider = null)
    {
        var claims = new List<Claim>();
        if (oid is not null)
            claims.Add(new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", oid));
        if (name is not null)
            claims.Add(new Claim("name", name));
        if (email is not null)
            claims.Add(new Claim(ClaimTypes.Email, email));
        if (provider is not null)
            claims.Add(new Claim("idp", provider));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
    }

    // ── GetOrCreateCurrentUserAsync ──────────────────────────

    [TestMethod]
    public async Task GetOrCreateCurrentUserAsync_ExistingUser_ReturnsProfile()
    {
        SetupHttpContext(oid: "oid-123", name: "John Doe", email: "john@test.com");

        var user = new UserProfile
        {
            Id = 1,
            EntraObjectId = "oid-123",
            DisplayName = "John Doe",
            Email = "john@test.com",
            Bio = "Dev",
            Location = "NYC",
            AvatarUrl = ""
        };
        _authRepo.Setup(r => r.GetByEntraObjectIdAsync("oid-123")).ReturnsAsync(user);

        var result = await _sut.GetOrCreateCurrentUserAsync();

        Assert.AreEqual("John Doe", result.DisplayName);
        Assert.AreEqual("john@test.com", result.Email);
        _authRepo.Verify(r => r.CreateUserAsync(It.IsAny<UserProfile>()), Times.Never);
    }

    [TestMethod]
    public async Task GetOrCreateCurrentUserAsync_ExistingUser_HidesProviderInternalEmail()
    {
        const string externalId = "117653d4-fb26-4b34-865d-fa3e6761aa7f";
        SetupHttpContext(oid: externalId, name: "Google User", provider: "google.com");

        var user = new UserProfile
        {
            Id = 1,
            EntraObjectId = externalId,
            DisplayName = "Google User",
            Email = $"{externalId}@fleetaidev.onmicrosoft.com",
            Bio = "Dev",
            Location = "NYC",
            AvatarUrl = ""
        };
        _authRepo.Setup(r => r.GetByEntraObjectIdAsync(externalId)).ReturnsAsync(user);

        var result = await _sut.GetOrCreateCurrentUserAsync();

        Assert.AreEqual("Google User", result.DisplayName);
        Assert.AreEqual(string.Empty, result.Email);
    }

    [TestMethod]
    public async Task GetOrCreateCurrentUserAsync_NewUser_AutoProvisions()
    {
        SetupHttpContext(oid: "oid-new", name: "Jane Smith", email: "jane@test.com");

        _authRepo.Setup(r => r.GetByEntraObjectIdAsync("oid-new")).ReturnsAsync((UserProfile?)null);
        _authRepo.Setup(r => r.CreateUserAsync(It.IsAny<UserProfile>()))
            .ReturnsAsync((UserProfile u) => { u.Id = 99; return u; });

        var result = await _sut.GetOrCreateCurrentUserAsync();

        Assert.AreEqual("Jane Smith", result.DisplayName);
        Assert.AreEqual("jane@test.com", result.Email);
        _authRepo.Verify(r => r.CreateUserAsync(It.Is<UserProfile>(u =>
            u.EntraObjectId == "oid-new" &&
            u.Username == "jane" &&
            u.DisplayName == "Jane Smith")), Times.Once);
    }

    [TestMethod]
    public async Task GetOrCreateCurrentUserAsync_LinkedGoogleIdentity_ReturnsLinkedProfile()
    {
        SetupHttpContext(oid: "google-oid", name: "Jane Google", email: "jane@test.com", provider: "google.com");

        var user = new UserProfile
        {
            Id = 7,
            EntraObjectId = "email-oid",
            DisplayName = "Jane Fleet",
            Email = "jane@test.com",
            Bio = "Dev",
            Location = "NYC",
            AvatarUrl = "",
        };
        var identity = new LoginIdentity
        {
            Id = 10,
            Provider = "Google",
            ProviderUserId = "google-oid",
            UserProfileId = 7,
            UserProfile = user,
            Email = "jane@test.com",
            DisplayName = "Jane Google",
        };
        _authRepo.Setup(r => r.GetLoginIdentityAsync("Google", "google-oid")).ReturnsAsync(identity);

        var result = await _sut.GetOrCreateCurrentUserAsync();

        Assert.AreEqual("Jane Fleet", result.DisplayName);
        _authRepo.Verify(r => r.GetByEntraObjectIdAsync(It.IsAny<string>()), Times.Never);
        _authRepo.Verify(r => r.UpdateLoginIdentityAsync(identity), Times.Once);
    }

    [TestMethod]
    public async Task GetOrCreateCurrentUserAsync_NewUnlimitedUser_AssignsUnlimitedRole()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UserRoles:UnlimitedEntraObjectIds:0"] = "unlimited-oid",
            })
            .Build();
        _sut = new AuthService(_authRepo.Object, _httpContextAccessor.Object, _configuration, _logger.Object);

        SetupHttpContext(oid: "unlimited-oid", name: "Unlimited User", email: "user@test.com");

        _authRepo.Setup(r => r.GetByEntraObjectIdAsync("unlimited-oid")).ReturnsAsync((UserProfile?)null);
        _authRepo.Setup(r => r.CreateUserAsync(It.IsAny<UserProfile>()))
            .ReturnsAsync((UserProfile u) => { u.Id = 100; return u; });

        await _sut.GetOrCreateCurrentUserAsync();

        _authRepo.Verify(r => r.CreateUserAsync(It.Is<UserProfile>(u =>
            u.EntraObjectId == "unlimited-oid" &&
            u.Role == UserRoles.Unlimited)), Times.Once);
    }

    [TestMethod]
    public async Task GetOrCreateCurrentUserAsync_AdminEmail_AssignsUnlimitedRole()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:AllowedEmails:0"] = "admin@test.com",
            })
            .Build();
        _sut = new AuthService(_authRepo.Object, _httpContextAccessor.Object, _configuration, _logger.Object);

        SetupHttpContext(oid: "oid-admin", name: "Admin User", email: "admin@test.com");

        _authRepo.Setup(r => r.GetByEntraObjectIdAsync("oid-admin")).ReturnsAsync((UserProfile?)null);
        _authRepo.Setup(r => r.CreateUserAsync(It.IsAny<UserProfile>()))
            .ReturnsAsync((UserProfile u) => { u.Id = 101; return u; });

        await _sut.GetOrCreateCurrentUserAsync();

        _authRepo.Verify(r => r.CreateUserAsync(It.Is<UserProfile>(u =>
            u.Email == "admin@test.com" &&
            u.Role == UserRoles.Unlimited)), Times.Once);
    }

    [TestMethod]
    public async Task GetOrCreateCurrentUserAsync_RusheelEmail_AssignsUnlimitedRole()
    {
        SetupHttpContext(oid: "oid-rusheel", name: "Rusheel", email: "rusheel@live.com");

        _authRepo.Setup(r => r.GetByEntraObjectIdAsync("oid-rusheel")).ReturnsAsync((UserProfile?)null);
        _authRepo.Setup(r => r.CreateUserAsync(It.IsAny<UserProfile>()))
            .ReturnsAsync((UserProfile u) => { u.Id = 200; return u; });

        await _sut.GetOrCreateCurrentUserAsync();

        _authRepo.Verify(r => r.CreateUserAsync(It.Is<UserProfile>(u =>
            u.Email == "rusheel@live.com" &&
            u.Role == UserRoles.Unlimited)), Times.Once);
    }

    [TestMethod]
    public async Task GetOrCreateCurrentUserAsync_NoEmail_DerivedFromName()
    {
        SetupHttpContext(oid: "oid-noemail", name: "John Doe");

        _authRepo.Setup(r => r.GetByEntraObjectIdAsync("oid-noemail")).ReturnsAsync((UserProfile?)null);
        _authRepo.Setup(r => r.CreateUserAsync(It.IsAny<UserProfile>()))
            .ReturnsAsync((UserProfile u) => u);

        await _sut.GetOrCreateCurrentUserAsync();

        _authRepo.Verify(r => r.CreateUserAsync(It.Is<UserProfile>(u =>
            u.Username == "john.doe")), Times.Once);
    }

    [TestMethod]
    public async Task CompleteLoginProviderLinkAsync_CreatesIdentityForTargetUser()
    {
        var targetUser = new UserProfile
        {
            Id = 42,
            EntraObjectId = "email-oid",
            DisplayName = "Fleet User",
            Email = "user@test.com",
        };
        _authRepo.Setup(r => r.GetByIdAsync(42)).ReturnsAsync(targetUser);
        _authRepo.Setup(r => r.GetLoginIdentitiesAsync(42)).ReturnsAsync([
            new LoginIdentity
            {
                Id = 1,
                Provider = "Email",
                ProviderUserId = "email-oid",
                UserProfileId = 42,
            },
        ]);
        SetupHttpContext(oid: "microsoft-oid", name: "Fleet User", email: "user@outlook.com", provider: "live.com");
        var state = await _sut.CreateLoginProviderLinkStateAsync(42, "Microsoft");

        var result = await _sut.CompleteLoginProviderLinkAsync(state.State);

        Assert.AreEqual("Fleet User", result.DisplayName);
        _authRepo.Verify(r => r.CreateLoginIdentityAsync(It.Is<LoginIdentity>(identity =>
            identity.UserProfileId == 42 &&
            identity.Provider == "Microsoft" &&
            identity.ProviderUserId == "microsoft-oid" &&
            identity.Email == "user@outlook.com")), Times.Once);
    }

    [TestMethod]
    public async Task CompleteLoginProviderLinkAsync_RejectsIdentityLinkedToAnotherUser()
    {
        var targetUser = new UserProfile { Id = 42, EntraObjectId = "email-oid", DisplayName = "Fleet User", Email = "user@test.com" };
        var otherUser = new UserProfile { Id = 99, EntraObjectId = "other-oid", DisplayName = "Other", Email = "other@test.com" };
        _authRepo.Setup(r => r.GetByIdAsync(42)).ReturnsAsync(targetUser);
        _authRepo.Setup(r => r.GetLoginIdentityAsync("Google", "google-oid")).ReturnsAsync(new LoginIdentity
        {
            Id = 5,
            Provider = "Google",
            ProviderUserId = "google-oid",
            UserProfileId = 99,
            UserProfile = otherUser,
        });
        SetupHttpContext(oid: "google-oid", name: "Other", email: "other@test.com", provider: "google.com");
        var state = await _sut.CreateLoginProviderLinkStateAsync(42, "Google");

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _sut.CompleteLoginProviderLinkAsync(state.State));
    }

    [TestMethod]
    public async Task GetLoginIdentitiesAsync_HidesProviderInternalEmails()
    {
        const string externalId = "117653d4-fb26-4b34-865d-fa3e6761aa7f";
        SetupHttpContext(oid: "current-oid", name: "Current User", email: "current@test.com", provider: "google.com");
        _authRepo.Setup(r => r.GetLoginIdentitiesAsync(42)).ReturnsAsync([
            new LoginIdentity
            {
                Id = 7,
                Provider = "Google",
                ProviderUserId = externalId,
                UserProfileId = 42,
                Email = $"{externalId}@fleetaidev.onmicrosoft.com",
                DisplayName = "Google User",
                LinkedAtUtc = DateTime.UtcNow,
            },
        ]);

        var result = await _sut.GetLoginIdentitiesAsync(42);

        Assert.AreEqual(1, result.Count);
        Assert.IsNull(result[0].Email);
        Assert.AreEqual("Google User", result[0].DisplayName);
    }

    [TestMethod]
    public async Task DeleteLoginIdentityAsync_RejectsRemovingLastIdentity()
    {
        _authRepo.Setup(r => r.GetLoginIdentityByIdAsync(42, 7)).ReturnsAsync(new LoginIdentity
        {
            Id = 7,
            Provider = "Email",
            ProviderUserId = "email-oid",
            UserProfileId = 42,
        });
        _authRepo.Setup(r => r.CountLoginIdentitiesAsync(42)).ReturnsAsync(1);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _sut.DeleteLoginIdentityAsync(42, 7));
    }

    // ── GetCurrentUserIdAsync ────────────────────────────────

    [TestMethod]
    public async Task GetCurrentUserIdAsync_ExistingUser_ReturnsId()
    {
        SetupHttpContext(oid: "oid-123");

        var user = new UserProfile { Id = 42, EntraObjectId = "oid-123" };
        _authRepo.Setup(r => r.GetByEntraObjectIdAsync("oid-123")).ReturnsAsync(user);

        var result = await _sut.GetCurrentUserIdAsync();

        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public async Task GetCurrentUserIdAsync_CachesResult_OnSecondCall()
    {
        SetupHttpContext(oid: "oid-123");

        var user = new UserProfile { Id = 42, EntraObjectId = "oid-123" };
        _authRepo.Setup(r => r.GetByEntraObjectIdAsync("oid-123")).ReturnsAsync(user);

        var first = await _sut.GetCurrentUserIdAsync();
        var second = await _sut.GetCurrentUserIdAsync();

        Assert.AreEqual(42, first);
        Assert.AreEqual(42, second);
        // Should only resolve the user once (cached)
        _authRepo.Verify(r => r.GetByEntraObjectIdAsync("oid-123"), Times.Once);
    }

    // ── Error cases ──────────────────────────────────────────

    [TestMethod]
    public async Task GetCurrentUserIdAsync_NoHttpContext_Throws()
    {
        _httpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
            () => _sut.GetCurrentUserIdAsync());
    }

    [TestMethod]
    public async Task GetCurrentUserIdAsync_NoOidClaim_Throws()
    {
        SetupHttpContext(); // No OID claim

        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
            () => _sut.GetCurrentUserIdAsync());
    }

    // ── OID variations ───────────────────────────────────────

    [TestMethod]
    public async Task GetCurrentUserIdAsync_ShortOidClaim_Works()
    {
        // Some tokens use "oid" instead of the long claim name
        var claims = new List<Claim> { new("oid", "short-oid-123") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var user = new UserProfile { Id = 10, EntraObjectId = "short-oid-123" };
        _authRepo.Setup(r => r.GetByEntraObjectIdAsync("short-oid-123")).ReturnsAsync(user);

        var result = await _sut.GetCurrentUserIdAsync();

        Assert.AreEqual(10, result);
    }
}
