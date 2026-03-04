using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.Search;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class SearchControllerTests
{
    private Mock<ISearchService> _searchService = null!;
    private Mock<IAuthService> _authService = null!;
    private SearchController _sut = null!;

    private const int UserId = 42;

    [TestInitialize]
    public void Setup()
    {
        _searchService = new Mock<ISearchService>();
        _authService = new Mock<IAuthService>();
        _authService.Setup(a => a.GetCurrentUserIdAsync()).ReturnsAsync(UserId);
        _sut = new SearchController(_searchService.Object, _authService.Object);
    }

    [TestMethod]
    public async Task Search_ReturnsOkWithResults()
    {
        var results = new List<SearchResultDto>
        {
            new("project", "Fleet", "Main project", "1 work item", "fleet")
        };
        _searchService.Setup(s => s.SearchAsync("42", "Fleet", null)).ReturnsAsync(results);

        var result = await _sut.Search("Fleet", null);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(results, ok.Value);
    }

    [TestMethod]
    public async Task Search_WithTypeFilter_PassesType()
    {
        _searchService.Setup(s => s.SearchAsync("42", "test", "workitem"))
            .ReturnsAsync(new List<SearchResultDto>());

        var result = await _sut.Search("test", "workitem");

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _searchService.Verify(s => s.SearchAsync("42", "test", "workitem"), Times.Once);
    }

    [TestMethod]
    public async Task Search_NullQuery_StillCallsService()
    {
        _searchService.Setup(s => s.SearchAsync("42", null, null))
            .ReturnsAsync(new List<SearchResultDto>());

        var result = await _sut.Search(null, null);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }
}
