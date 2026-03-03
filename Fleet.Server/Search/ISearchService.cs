using Fleet.Server.Models;

namespace Fleet.Server.Search;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultDto>> SearchAsync(string? query, string? type);
}
