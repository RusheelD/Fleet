using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Fleet.Server.LLM;

public class ModelCatalog : IModelCatalog
{
    private readonly Dictionary<string, string> _models;

    public ModelCatalog(IOptions<ModelCatalogOptions> options)
    {
        _models = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in options.Value.Models)
        {
            _models[pair.Key] = pair.Value;
        }

        // Ensure defaults if configuration doesn't provide them.
        SetDefault("Haiku", "gpt-5.2-codex");
        SetDefault("Sonnet", "gpt-5.2-codex");
        SetDefault("Opus", "gpt-5.2-codex");
    }

    private void SetDefault(string key, string modelName)
    {
        if (!_models.ContainsKey(key))
        {
            _models[key] = modelName;
        }
    }

    public string Get(string key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (!_models.TryGetValue(key, out var modelName))
        {
            throw new KeyNotFoundException($"Model key '{key}' is not registered in the catalog.");
        }

        return modelName;
    }

    public IReadOnlyDictionary<string, string> Models => _models;
}

