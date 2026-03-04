using System.Collections.Generic;

namespace Fleet.Server.LLM;

public class ModelCatalogOptions
{
    public const string SectionName = "Models";

    public Dictionary<string, string> Models { get; set; } = new();
}
