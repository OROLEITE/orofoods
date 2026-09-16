namespace Orofoods.Web.Api;

public sealed class PageRequest
{
    public PageRequest(int page = 1, int pageSize = 20)
    {
        Page = Math.Max(1, page);
        PageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
    }

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
}
