namespace Fleet.Server.LLM;

public interface IModelCatalog
{
    string Get(string key);
    IReadOnlyDictionary<string, string> Models { get; }
}
