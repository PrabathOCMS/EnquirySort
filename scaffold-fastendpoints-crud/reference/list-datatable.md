# List{EntityPlural}ForDataTable endpoint

Read `reference/conventions.md` first. Only scaffold this endpoint if the entity is browsed/managed in a
paged table UI.

## Request

```csharp
public sealed class List{EntityPlural}ForDataTableRequest
{
    public Guid? OrganizationId { get; set; }   // child entities only
    // template-style only, optional:
    // public {EntityPlural}ForDataTableRequest_Filter? Filter { get; set; }
    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
    public SortType? Sort { get; set; }         // or a bespoke {Entity}SortType if this table has custom sortable columns
    public string? Search { get; set; }

    [FromHeader(headerName: "X-Request-Counter", isRequired: false)]
    public long? RequestCounter { get; set; }
}
```

Use the shared `SortType` enum (`Unsorted, Updated, Created, Name`) unless this entity needs extra sortable
columns (e.g. a Contact-specific sort by Postcode/State) — in that case define a bespoke `{Entity}SortType`
enum rather than extending the shared one for a one-off column.

## Response type

`DataTableResponse<{Entity}>` (or `DataTableResponse<{Entity}ForDataTable>` if the project uses the
template-style slim projection model that drops columns not shown in the table — check `Models/ForDataTable/`
for precedent before deciding). `DataTableResponse<T>` = `{ RequestCounter, Records, PageNumber, PageSize,
TotalCount }`.

## Endpoint

```csharp
public sealed class List{EntityPlural}ForDataTableEndpoint
    : Endpoint<List{EntityPlural}ForDataTableRequest, DataTableResponse<{Entity}>>
{
    private readonly {EntityPlural}Repository _repo;

    public List{EntityPlural}ForDataTableEndpoint({EntityPlural}Repository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/{entityPluralLower}/listForDataTable");   // child: "/{entityPluralLower}/{organizationId}/listForDataTable"
        SerializerContext(List{EntityPlural}ForDataTableContext.Default);
        AllowAnonymous();   // or Policies("Master"/"User") + org-role check
    }

    public override async Task HandleAsync(List{EntityPlural}ForDataTableRequest req, CancellationToken ct)
    {
        ValidateInput(req);   // defaulting only — nothing here can produce a client-visible error

        DataTableResponse<{Entity}> response = await _repo.List{EntityPlural}ForDataTableAsync(
            req.PageNumber!.Value, req.PageSize!.Value, req.Sort!.Value, req.RequestCounter, req.Search, ct);

        // never error on an out-of-range page — re-query page 1 instead
        if (1 + (response.PageNumber - 1) * response.PageSize > response.TotalCount)
        {
            response = await _repo.List{EntityPlural}ForDataTableAsync(
                1, req.PageSize!.Value, req.Sort!.Value, req.RequestCounter, req.Search, ct);
        }

        await Send.OkAsync(response);
    }

    private void ValidateInput(List{EntityPlural}ForDataTableRequest req)
    {
        req.PageNumber ??= 1;
        req.PageSize ??= 30;
        if (req.PageSize is < 1 or > 200) req.PageSize = 30;
        if (req.Sort is null or SortType.Unsorted) req.Sort = SortType.Name;
    }
}
```

No `ValidateOutput` — same reasoning as ListForDropdown.

## Sorting rule

Sort direction is **fixed per column server-side** — there's no bidirectional/toggle sort. Pick whichever
direction matches an existing index so no extra descending index is needed: `Name` → ascending,
`UpdatedDateUtc`/created → descending. For "created" order, sort by `order by id desc` rather than a
timestamp column — the COMB-generated `id` is chronologically sequential and is the clustered index, so
sorting by it is the cheapest option.

## Repository method

Two-result-set pattern (count, then page), same shape as ListForDropdown with pagination added:

```csharp
public async Task<DataTableResponse<{Entity}>> List{EntityPlural}ForDataTableAsync(
    int pageNumber, int pageSize, SortType sort, long? requestCounter, string? searchTerm,
    CancellationToken ct = default)
{
    using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.{Name});

    DynamicParameters parameters = new();
    parameters.Add("@pageSize", pageSize, DbType.Int32);
    parameters.Add("@pageNumber", pageNumber, DbType.Int32);
    // child entities: parameters.Add("@organizationId", organizationId, DbType.Guid);

    string whereQuery = "";
    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
        whereQuery = " and Name like @search";
        parameters.Add("@search", $"%{searchTerm}%", DbType.String, size: 100);
        // template-style: SearchQueryBuilder.BuildSearchSqlStringWithParams(...) instead
    }

    string sortColumn = sort switch
    {
        SortType.Updated => "UpdatedDateUtc desc",
        SortType.Created => "id desc",
        _ => "Name asc"
    };

    // lang=sql
    string sql = $@"
select count(*) from tbl{EntityPlural} where Deleted = 0 {"{and OrganizationId = @organizationId}"}{whereQuery};

select {Entity's columns}
from tbl{EntityPlural}
where Deleted = 0 {"{and OrganizationId = @organizationId}"}{whereQuery}
order by {sortColumn}
offset @pageSize * (@pageNumber - 1) rows
fetch next @pageSize rows only;";

    using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(
        new CommandDefinition(sql, parameters, cancellationToken: ct));

    int totalCount = await gridReader.ReadFirstAsync<int>();
    List<{Entity}> records = (await gridReader.ReadAsync<{Entity}>()).AsList();

    return new DataTableResponse<{Entity}>
    {
        RequestCounter = requestCounter,
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalCount = totalCount,
        Records = records
    };
}
```

Notes:
- Cancellation-token-aware — read-only.
- Template-style perf optimization, only worth adding if the target project already does this elsewhere: when
  there's no search term and no filter, replace the `count(*)` with an O(1) lookup against
  `sys.partitions`/`IndexProperty` for the relevant filtered index's cached row count, falling back to
  `count(*) option (recompile)` whenever a filter/search is present (avoids a bad cached plan from parameter
  sniffing across differently-filtered calls). Don't add this speculatively for a brand-new entity with an
  unproven row count — start with plain `count(*)` and revisit only if it becomes a real cost.
