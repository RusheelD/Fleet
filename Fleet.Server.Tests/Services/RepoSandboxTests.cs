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
    public void EnsureGitWorkingDirectory_FallsBackToConfiguredSandboxRootWhenBothPathsAreMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var sandboxRoot = Path.Combine(root, "configured-sandbox");

        try
        {
            var result = RepoSandbox.EnsureGitWorkingDirectory(
                workingDir: null,
                repoRoot: string.Empty,
                sandboxRoot: sandboxRoot);

            Assert.AreEqual(sandboxRoot, result);
            Assert.IsTrue(Directory.Exists(sandboxRoot));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void EnsureSandboxWorkspaceRoot_UsesConfiguredRootWhenAvailable()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var sandboxRoot = Path.Combine(root, "configured-sandbox");

        try
        {
            var result = RepoSandbox.EnsureSandboxWorkspaceRoot(sandboxRoot);

            Assert.AreEqual(sandboxRoot, result);
            Assert.IsTrue(Directory.Exists(sandboxRoot));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void EnsureSandboxWorkspaceRoot_FallsBackToAppOwnedDirectoryWhenConfiguredRootIsUnavailable()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var blockedRoot = Path.Combine(root, "blocked-root");
        var appBase = Path.Combine(root, "app-base");
        var expected = Path.Combine(appBase, ".fleet-sandboxes");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(blockedRoot, "not a directory");

            var result = RepoSandbox.EnsureSandboxWorkspaceRoot(
                configuredRoot: Path.Combine(blockedRoot, "nested"),
                appBaseOverride: appBase);

            Assert.AreEqual(expected, result);
            Assert.IsTrue(Directory.Exists(expected));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            else if (File.Exists(blockedRoot))
                File.Delete(blockedRoot);
        }
    }
}
