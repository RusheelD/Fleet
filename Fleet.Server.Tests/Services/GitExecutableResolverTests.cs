using Fleet.Server.Agents;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class GitExecutableResolverTests
{
    [TestMethod]
    public void BuildProcessPath_AppendsCommonGitDirectories()
    {
        var initial = OperatingSystem.IsWindows() ? @"C:\tools" : "/app/bin";

        var result = GitExecutableResolver.BuildProcessPath(initial);

        if (OperatingSystem.IsWindows())
        {
            StringAssert.Contains(result, @"C:\tools");
            StringAssert.Contains(result, "Git");
        }
        else
        {
            StringAssert.Contains(result, "/app/bin");
            StringAssert.Contains(result, "/usr/bin");
        }
    }

    [TestMethod]
    public void Resolve_ReturnsConfiguredPathWhenProvided()
    {
        const string configuredPath = "/custom/git";

        var result = GitExecutableResolver.Resolve(configuredPath);

        Assert.AreEqual(configuredPath, result);
    }
}
