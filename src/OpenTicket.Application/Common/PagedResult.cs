namespace OpenTicket.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record PageRequest(int Page, int PageSize)
{
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;
}
