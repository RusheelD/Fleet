using Fleet.Server.Mcp;

namespace Fleet.Server.Tests.Mcp;

[TestClass]
public class McpToolSessionFactoryTests
{
    [TestMethod]
    public void MergeConfigs_UserConfigOverridesSystemConfigWithSameName()
    {
        var systemConfig = CreateConfig(id: -1, name: "Playwright", command: "npx");
        var userConfig = CreateConfig(id: 5, name: "Playwright", command: "custom-playwright");

        var merged = McpToolSessionFactory.MergeConfigs([systemConfig], [userConfig]);

        Assert.AreEqual(1, merged.Count);
        Assert.AreEqual(5, merged[0].Id);
        Assert.AreEqual("custom-playwright", merged[0].Command);
    }

    [TestMethod]
    public void MergeConfigs_PreservesUniqueSystemAndUserConfigs()
    {
        var systemConfig = CreateConfig(id: -1, name: "Playwright", command: "npx");
        var userConfig = CreateConfig(id: 6, name: "Brave", command: "npx");

        var merged = McpToolSessionFactory.MergeConfigs([systemConfig], [userConfig]);

        Assert.AreEqual(2, merged.Count);
        CollectionAssert.AreEqual(new[] { "Playwright", "Brave" }, merged.Select(config => config.Name).ToArray());
    }

    private static McpServerRuntimeConfig CreateConfig(int id, string name, string command)
        => new(
            id,
            name,
            "stdio",
            command,
            [],
            null,
            null,
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>());
}
