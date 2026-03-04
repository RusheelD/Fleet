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

        Assert.AreEqual("claude-haiku-4-5-20251001", catalog.Get("Haiku"));
        Assert.AreEqual("claude-sonnet-4-6", catalog.Get("Sonnet"));
        Assert.AreEqual("claude-opus-4-6", catalog.Get("Opus"));
    }

    [TestMethod]
    public void Get_CaseInsensitive()
    {
        var catalog = new ModelCatalog(CreateOptions());

        Assert.AreEqual("claude-sonnet-4-6", catalog.Get("sonnet"));
        Assert.AreEqual("claude-sonnet-4-6", catalog.Get("SONNET"));
    }

    [TestMethod]
    public void Get_CustomModelOverridesDefault()
    {
        var catalog = new ModelCatalog(CreateOptions(new Dictionary<string, string>
        {
            ["Sonnet"] = "my-custom-sonnet"
        }));

        Assert.AreEqual("my-custom-sonnet", catalog.Get("Sonnet"));
        // Other defaults still present
        Assert.AreEqual("claude-haiku-4-5-20251001", catalog.Get("Haiku"));
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
        Assert.IsTrue(models.ContainsKey("Haiku"));
        Assert.IsTrue(models.ContainsKey("Sonnet"));
        Assert.IsTrue(models.ContainsKey("Opus"));
        Assert.AreEqual(4, models.Count);
    }
}
