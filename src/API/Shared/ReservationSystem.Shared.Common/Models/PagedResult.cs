namespace ReservationSystem.Shared.Common.Models;

/// <summary>
/// Wraps a page of results for list endpoints that support pagination.
///
/// The HTTP response body for a paginated endpoint should serialise this type
/// directly (via <c>OkJsonAsync</c>), giving clients all the metadata needed
/// to navigate pages without additional round-trips.
///
/// Example JSON response:
/// <code>
/// {
///   "items":      [ { … }, { … } ],
///   "totalCount": 142,
///   "page":       2,
///   "pageSize":   20,
///   "totalPages": 8,
///   "hasNextPage": true,
///   "hasPreviousPage": true
/// }
/// </code>
///
/// Example handler usage:
/// <code>
///   var paged  = PagedRequest.From(req);           // parses ?page=&amp;pageSize=
///   var items  = await _repo.GetPageAsync(paged);
///   var total  = await _repo.CountAsync();
///   var result = PagedResult&lt;OfferResponse&gt;.From(items, total, paged);
///   return await req.OkJsonAsync(result);
/// </code>
/// </summary>
/// <typeparam name="T">The DTO type of each item in the page.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>The items on the current page.</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Total number of matching records across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>The 1-based current page number.</summary>
    public int Page { get; init; }

    /// <summary>Maximum number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Derived total number of pages.</summary>
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling((double)TotalCount / PageSize)
        : 0;

    /// <summary>Whether there is a page after this one.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Whether there is a page before this one.</summary>
    public bool HasPreviousPage => Page > 1;

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="PagedResult{T}"/> from a page of items, the total
    /// record count, and the originating <see cref="PagedRequest"/>.
    /// </summary>
    public static PagedResult<T> From(
        IReadOnlyList<T> items,
        int totalCount,
        PagedRequest request)
        => new()
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = request.Page,
            PageSize   = request.PageSize
        };
}

/// <summary>
/// Captures the standard <c>page</c> and <c>pageSize</c> query parameters
/// from a list endpoint request, with safe defaults and clamping.
///
/// Use <see cref="From(Microsoft.Azure.Functions.Worker.Http.HttpRequestData)"/>
/// to parse directly from the incoming request URL, or construct manually in
/// tests.
///
/// SQL usage — pass <see cref="Offset"/> and <see cref="PageSize"/> to a Dapper
/// query:
/// <code>
///   SELECT * FROM [offer].[Offers]
///   ORDER BY CreatedAt DESC
///   OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
/// </code>
/// </summary>
public sealed record PagedRequest
{
    /// <summary>Default page size when the caller omits <c>pageSize</c>.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Hard upper limit on page size to prevent excessively large queries.</summary>
    public const int MaxPageSize = 100;

    /// <summary>1-based page number. Defaults to 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Items per page. Clamped to [1, <see cref="MaxPageSize"/>].</summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// 0-based row offset for SQL <c>OFFSET … ROWS</c> clauses.
    /// Derived from <see cref="Page"/> and <see cref="PageSize"/>.
    /// </summary>
    public int Offset => (Page - 1) * PageSize;

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses <c>?page=</c> and <c>?pageSize=</c> from the request URL query
    /// string with safe defaults and clamping.  Invalid or missing values fall
    /// back gracefully rather than throwing.
    /// </summary>
    /// <param name="req">The inbound Azure Functions HTTP request.</param>
    public static PagedRequest From(Microsoft.Azure.Functions.Worker.Http.HttpRequestData req)
    {
        // Parse ?key=value pairs manually — avoids a System.Web dependency in
        // the isolated worker host where System.Web is not available.
        var queryParams = req.Url.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(kv => kv.Length == 2)
            .ToDictionary(
                kv => Uri.UnescapeDataString(kv[0]),
                kv => Uri.UnescapeDataString(kv[1]),
                StringComparer.OrdinalIgnoreCase);

        int page = queryParams.TryGetValue("page", out var pageStr)
                   && int.TryParse(pageStr, out var p) && p > 0 ? p : 1;

        int pageSize = queryParams.TryGetValue("pageSize", out var psStr)
                       && int.TryParse(psStr, out var ps) && ps > 0
            ? Math.Min(ps, MaxPageSize)
            : DefaultPageSize;

        return new PagedRequest { Page = page, PageSize = pageSize };
    }
}
