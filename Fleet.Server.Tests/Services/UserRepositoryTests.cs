using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Fleet.Server.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class UserRepositoryTests
{
    private FleetDbContext _context = null!;
    private UserRepository _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new FleetDbContext(options);
        _sut = new UserRepository(_context, NullLogger<UserRepository>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task GetProfileAsync_HidesProviderInternalEmails()
    {
        const string externalId = "117653d4-fb26-4b34-865d-fa3e6761aa7f";
        _context.UserProfiles.Add(new UserProfile
        {
            Id = 1,
            EntraObjectId = externalId,
            Username = "google.user",
            DisplayName = "Google User",
            Email = $"{externalId}@fleetaidev.onmicrosoft.com",
        });
        await _context.SaveChangesAsync();

        var profile = await _sut.GetProfileAsync(1);

        Assert.IsNotNull(profile);
        Assert.AreEqual(string.Empty, profile.Email);
        Assert.AreEqual("Google User", profile.DisplayName);
    }

    [TestMethod]
    public async Task UpdateProfileAsync_RejectsProviderInternalEmails()
    {
        const string externalId = "117653d4-fb26-4b34-865d-fa3e6761aa7f";
        _context.UserProfiles.Add(new UserProfile
        {
            Id = 1,
            EntraObjectId = externalId,
            Username = "google.user",
            DisplayName = "Google User",
            Email = "real.user@gmail.com",
        });
        await _context.SaveChangesAsync();

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            _sut.UpdateProfileAsync(
                1,
                new UpdateProfileRequest(
                    "Google User",
                    $"{externalId}@fleetaidev.onmicrosoft.com",
                    string.Empty,
                    string.Empty)));
    }
}
