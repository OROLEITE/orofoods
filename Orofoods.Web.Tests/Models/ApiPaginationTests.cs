using Orofoods.Web.Api;

namespace Orofoods.Web.Tests.Models;

public class ApiPaginationTests
{
    [Theory]
    [InlineData(0, 0, 1, 20)]
    [InlineData(3, 500, 3, 100)]
    public void Page_request_normalizes_invalid_boundaries(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var request = new PageRequest(page, pageSize);

        Assert.Equal(expectedPage, request.Page);
        Assert.Equal(expectedPageSize, request.PageSize);
    }

    [Fact]
    public void Paged_result_calculates_total_pages()
    {
        var result = new PagedResult<int>([1, 2], 2, 20, 41);

        Assert.Equal(3, result.TotalPages);
    }
}
