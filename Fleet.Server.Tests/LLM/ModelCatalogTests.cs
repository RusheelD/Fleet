using Fleet.Server.LLM;
using Microsoft.Extensions.Options;
using Moq;

namespace Fleet.Server.Tests.LLM;

[TestClass]
public class ModelCatalogTests
{
    private static IOptions<ModelCatalogOptions> CreateOptions(Dictionary<string, string>? models = null)
    {
        var opts = new ModelCatalogOptions();
        if (models is not null)
        {
            opts.Models = models;
        }
        return Options.Create(opts);
    }

    [TestMethod]
    public void Defaults_PopulatedWhenOptionsEmpty()
    {
        var catalog = new ModelCatalog(CreateOptions());

        Assert.AreEqual("gpt-5.2-codex", catalog.Get("Fast"));
        Assert.AreEqual("gpt-5.2-codex", catalog.Get("Standard"));
        Assert.AreEqual("gpt-5.2-codex", catalog.Get("Premium"));
    }

    [TestMethod]
    public void Get_CaseInsensitive()
    {
        var catalog = new ModelCatalog(CreateOptions());

        Assert.AreEqual("gpt-5.2-codex", catalog.Get("standard"));
        Assert.AreEqual("gpt-5.2-codex", catalog.Get("STANDARD"));
    }

    [TestMethod]
    public void Get_CustomModelOverridesDefault()
    {
        var catalog = new ModelCatalog(CreateOptions(new Dictionary<string, string>
        {
            ["Standard"] = "my-custom-standard"
        }));

        Assert.AreEqual("my-custom-standard", catalog.Get("Standard"));
        // Other defaults still present
        Assert.AreEqual("gpt-5.2-codex", catalog.Get("Fast"));
    }

    [TestMethod]
    public void Get_UnknownKey_ThrowsKeyNotFound()
    {
        var catalog = new ModelCatalog(CreateOptions());

        Assert.ThrowsException<KeyNotFoundException>(() => catalog.Get("NonExistent"));
    }

    [TestMethod]
    public void Get_NullKey_ThrowsArgumentNull()
    {
        var catalog = new ModelCatalog(CreateOptions());

        Assert.ThrowsException<ArgumentNullException>(() => catalog.Get(null!));
    }

    [TestMethod]
    public void Models_ReturnsAllRegistered()
    {
        var catalog = new ModelCatalog(CreateOptions(new Dictionary<string, string>
        {
            ["Custom"] = "custom-model"
        }));

        var models = catalog.Models;

        Assert.IsTrue(models.ContainsKey("Custom"));
        Assert.IsTrue(models.ContainsKey("Fast"));
        Assert.IsTrue(models.ContainsKey("Standard"));
        Assert.IsTrue(models.ContainsKey("Premium"));
        Assert.AreEqual(4, models.Count);
    }
}

