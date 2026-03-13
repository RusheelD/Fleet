using Fleet.Server.Agents;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class RepoSandboxTests
{
    [TestMethod]
    public void EnsureGitWorkingDirectory_UsesProvidedWorkingDirectoryAndCreatesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var workingDirectory = Path.Combine(root, "provided");

        try
        {
            var result = RepoSandbox.EnsureGitWorkingDirectory(workingDirectory, repoRoot: string.Empty);

            Assert.AreEqual(workingDirectory, result);
            Assert.IsTrue(Directory.Exists(workingDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void EnsureGitWorkingDirectory_FallsBackToRepoRootWhenWorkingDirectoryIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var repoRoot = Path.Combine(root, "repo");

        try
        {
            var result = RepoSandbox.EnsureGitWorkingDirectory(workingDir: null, repoRoot);

            Assert.AreEqual(repoRoot, result);
            Assert.IsTrue(Directory.Exists(repoRoot));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void EnsureGitWorkingDirectory_FallsBackToFleetAgentTempRootWhenBothPathsAreMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var tempOverride = Path.Combine(root, "temp-base");
        var expected = Path.Combine(tempOverride, "fleet-agent");

        try
        {
            Directory.CreateDirectory(tempOverride);

            var result = RepoSandbox.EnsureGitWorkingDirectory(
                workingDir: null,
                repoRoot: string.Empty,
                tempPathOverride: tempOverride);

            Assert.AreEqual(expected, result);
            Assert.IsTrue(Directory.Exists(expected));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void EnsureSandboxWorkspaceRoot_FallsBackToAppOwnedDirectoryWhenTempRootIsUnavailable()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var blockedTempPath = Path.Combine(root, "blocked-temp");
        var appBase = Path.Combine(root, "app-base");
        var expected = Path.Combine(appBase, ".fleet-agent");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(blockedTempPath, "not a directory");

            var result = RepoSandbox.EnsureSandboxWorkspaceRoot(
                tempPathOverride: blockedTempPath,
                appBaseOverride: appBase);

            Assert.AreEqual(expected, result);
            Assert.IsTrue(Directory.Exists(expected));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            else if (File.Exists(blockedTempPath))
                File.Delete(blockedTempPath);
        }
    }

    [TestMethod]
    public void EnsureSandboxWorkspaceRoot_FallsBackToAppOwnedDirectoryWhenTempBasePathDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var missingTempBase = Path.Combine(root, "missing-temp-base");
        var appBase = Path.Combine(root, "app-base");
        var expected = Path.Combine(appBase, ".fleet-agent");

        try
        {
            Directory.CreateDirectory(root);

            var result = RepoSandbox.EnsureSandboxWorkspaceRoot(
                tempPathOverride: missingTempBase,
                appBaseOverride: appBase);

            Assert.AreEqual(expected, result);
            Assert.IsTrue(Directory.Exists(expected));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
