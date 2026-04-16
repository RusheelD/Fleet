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

    [TestMethod]
    public void BuildAuthenticatedCloneUrl_UsesStandardHttpsUsernameTokenAuth()
    {
        var result = RepoSandbox.BuildAuthenticatedCloneUrl(
            "RusheelD/Chess-Fleet",
            "gho_test-token+needs/encoding");

        Assert.AreEqual(
            "https://git:gho_test-token%2Bneeds%2Fencoding@github.com/RusheelD/Chess-Fleet.git",
            result);
    }

    [TestMethod]
    public void BuildCommitArgumentList_UsesSeparateMessageArgument()
    {
        var result = RepoSandbox.BuildCommitArgumentList("Ship \"quoted\" fix");

        CollectionAssert.AreEqual(
            new[] { "commit", "-m", "Ship \"quoted\" fix" },
            result.ToArray());
    }

    [TestMethod]
    public void BuildCommitEnvironment_SetsAuthorAndCommitterIdentity()
    {
        var result = RepoSandbox.BuildCommitEnvironment(
            "Fleet Agent",
            "agent@fleet.dev");

        Assert.AreEqual("Fleet Agent", result["GIT_AUTHOR_NAME"]);
        Assert.AreEqual("agent@fleet.dev", result["GIT_AUTHOR_EMAIL"]);
        Assert.AreEqual("Fleet Agent", result["GIT_COMMITTER_NAME"]);
        Assert.AreEqual("agent@fleet.dev", result["GIT_COMMITTER_EMAIL"]);
    }

    [TestMethod]
    public void BuildMergeBranchArgumentList_PrefersSourceBranchContents()
    {
        var result = RepoSandbox.BuildMergeBranchArgumentList("fleet/42-child-flow");

        CollectionAssert.AreEqual(
            new[]
            {
                "merge",
                "--no-ff",
                "--no-edit",
                "-X",
                "theirs",
                "refs/remotes/origin/fleet/42-child-flow",
            },
            result.ToArray());
    }

    [TestMethod]
    public void BuildFetchRemoteBranchArgumentList_UsesRequestedDepthWhenProvided()
    {
        var result = RepoSandbox.BuildFetchRemoteBranchArgumentList("fleet/42-child-flow", depth: 50);

        CollectionAssert.AreEqual(
            new[]
            {
                "fetch",
                "--depth",
                "50",
                "origin",
                "+refs/heads/fleet/42-child-flow:refs/remotes/origin/fleet/42-child-flow",
            },
            result.ToArray());
    }

    [TestMethod]
    public void BuildFetchRemoteBranchArgumentList_OmitsDepthForFullHistoryFetches()
    {
        var result = RepoSandbox.BuildFetchRemoteBranchArgumentList("fleet/42-child-flow");

        CollectionAssert.AreEqual(
            new[]
            {
                "fetch",
                "origin",
                "+refs/heads/fleet/42-child-flow:refs/remotes/origin/fleet/42-child-flow",
            },
            result.ToArray());
    }

    [TestMethod]
    public void BuildMergeBaseArgumentList_TargetsRemoteTrackingBranch()
    {
        var result = RepoSandbox.BuildMergeBaseArgumentList("fleet/42-child-flow");

        CollectionAssert.AreEqual(
            new[]
            {
                "merge-base",
                "HEAD",
                "refs/remotes/origin/fleet/42-child-flow",
            },
            result.ToArray());
    }

    [TestMethod]
    public void LooksLikeUnrelatedHistoriesMergeFailure_DetectsGitError()
    {
        Assert.IsTrue(RepoSandbox.LooksLikeUnrelatedHistoriesMergeFailure(
            "fatal: refusing to merge unrelated histories"));
        Assert.IsFalse(RepoSandbox.LooksLikeUnrelatedHistoriesMergeFailure(
            "fatal: merge conflict in src/App.tsx"));
    }

    [TestMethod]
    public void IsAlreadyCompleteHistoryFetchMessage_DetectsBenignUnshallowFailures()
    {
        Assert.IsTrue(RepoSandbox.IsAlreadyCompleteHistoryFetchMessage(
            "fatal: --unshallow on a complete repository does not make sense"));
        Assert.IsTrue(RepoSandbox.IsAlreadyCompleteHistoryFetchMessage(
            "fatal: repository is not a shallow repository"));
        Assert.IsFalse(RepoSandbox.IsAlreadyCompleteHistoryFetchMessage(
            "fatal: could not read Username for 'https://github.com'"));
    }

    [TestMethod]
    public void DetectGlobalToolchainMutation_BlocksGlobalNpmInstall()
    {
        var result = RepoSandbox.DetectGlobalToolchainMutation("npm", "install -g typescript");

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void DetectGlobalToolchainMutation_BlocksRedirectedPipInstall()
    {
        var result = RepoSandbox.DetectGlobalToolchainMutation("python", "-m pip install --user requests");

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void DetectGlobalToolchainMutation_AllowsRepoLocalInstalls()
    {
        Assert.IsNull(RepoSandbox.DetectGlobalToolchainMutation("npm", "install"));
        Assert.IsNull(RepoSandbox.DetectGlobalToolchainMutation("python", "-m pip install -r requirements.txt"));
    }

    [TestMethod]
    public void GetPythonVirtualEnvironmentRoot_UsesRepoLocalDotVenv()
    {
        var repoRoot = Path.Combine("tmp", "repo");

        var result = RepoSandbox.GetPythonVirtualEnvironmentRoot(repoRoot);

        Assert.AreEqual(Path.Combine(repoRoot, ".venv"), result);
    }

    [TestMethod]
    public void ApplySharedPythonToolingEnvironment_PrependsSharedSitePackages()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PYTHONPATH"] = "repo-tools",
        };

        RepoSandbox.ApplySharedPythonToolingEnvironment(environment, "/opt/fleet-python-tools");

        Assert.AreEqual(
            $"/opt/fleet-python-tools{Path.PathSeparator}repo-tools",
            environment["PYTHONPATH"]);
    }

    [TestMethod]
    public void ApplySharedNodeToolingEnvironment_PrependsSharedModulesAndBinPaths()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = "repo-bin",
            ["NODE_PATH"] = "repo-modules",
        };

        RepoSandbox.ApplySharedNodeToolingEnvironment(
            environment,
            sharedNodeModulesPath: "/opt/fleet-node-tools/node_modules",
            sharedNodeBinPath: "/opt/fleet-node-tools/node_modules/.bin");

        Assert.AreEqual(
            $"/opt/fleet-node-tools/node_modules/.bin{Path.PathSeparator}repo-bin",
            environment["PATH"]);
        Assert.AreEqual(
            $"/opt/fleet-node-tools/node_modules{Path.PathSeparator}repo-modules",
            environment["NODE_PATH"]);
    }

    [TestMethod]
    public void EnsureLocalGitIgnoreEntries_AppendsMissingEntriesOnce()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var repoRoot = Path.Combine(root, "repo");
        var gitInfoDirectory = Path.Combine(repoRoot, ".git", "info");
        var excludePath = Path.Combine(gitInfoDirectory, "exclude");

        try
        {
            Directory.CreateDirectory(gitInfoDirectory);
            File.WriteAllText(excludePath, "bin/\n");

            RepoSandbox.EnsureLocalGitIgnoreEntries(repoRoot, [".venv/", "node_modules/", ".venv/"]);

            var lines = File.ReadAllLines(excludePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "bin/", ".venv/", "node_modules/" },
                lines);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void CleanupStaleSandboxes_RemovesOldDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var staleSandbox = Path.Combine(root, "old-sandbox");

        try
        {
            Directory.CreateDirectory(staleSandbox);
            File.WriteAllText(Path.Combine(staleSandbox, "file.txt"), "test");
            Directory.SetLastWriteTimeUtc(staleSandbox, DateTime.UtcNow.AddHours(-24));

            var deleted = RepoSandbox.CleanupStaleSandboxes(root, TimeSpan.FromHours(6));

            Assert.AreEqual(1, deleted);
            Assert.IsFalse(Directory.Exists(staleSandbox));
        }
        finally
        {
            RepoSandbox.ReleaseActiveSandboxRoot(staleSandbox);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void CleanupStaleSandboxes_SkipsActiveDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "fleet-tests", Guid.NewGuid().ToString("N"));
        var activeSandbox = Path.Combine(root, "active-sandbox");

        try
        {
            Directory.CreateDirectory(activeSandbox);
            File.WriteAllText(Path.Combine(activeSandbox, "file.txt"), "test");
            Directory.SetLastWriteTimeUtc(activeSandbox, DateTime.UtcNow.AddHours(-24));
            RepoSandbox.TrackActiveSandboxRoot(activeSandbox);

            var deleted = RepoSandbox.CleanupStaleSandboxes(root, TimeSpan.FromHours(6));

            Assert.AreEqual(0, deleted);
            Assert.IsTrue(Directory.Exists(activeSandbox));
        }
        finally
        {
            RepoSandbox.ReleaseActiveSandboxRoot(activeSandbox);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
