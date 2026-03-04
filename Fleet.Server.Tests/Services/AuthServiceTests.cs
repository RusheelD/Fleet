using System.Security.Claims;
using Fleet.Server.Auth;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AuthServiceTests
{
    private Mock<IAuthRepository> _authRepo = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessor = null!;
    private Mock<ILogger<AuthService>> _logger = null!;
    private AuthService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _authRepo = new Mock<IAuthRepository>();
        _httpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new Mock<ILogger<AuthService>>();
        _sut = new AuthService(_authRepo.Object, _httpContextAccessor.Object, _logger.Object);
    }

    private void SetupHttpContext(string? oid = null, string? name = null, string? email = null)
    {
        var claims = new List<Claim>();
        if (oid is not null)
            claims.Add(new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", oid));
        if (name is not null)
            claims.Add(new Claim("name", name));
        if (email is not null)
            claims.Add(new Claim(ClaimTypes.Email, email));

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
