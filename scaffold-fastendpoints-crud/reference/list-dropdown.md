# List{EntityPlural}ForDropdown endpoint

Read `reference/conventions.md` first. Only scaffold this endpoint if the entity has a displayable
name/label column — skip it otherwise.

## Request

```csharp
public sealed class List{EntityPlural}ForDropdownRequest
{
    public Guid? OrganizationId { get; set; }   // child entities only
    public string? Search { get; set; }
    // template-style only, optional:
    // public {EntityPlural}ForDropdownRequest_Filter? Filter { get; set; }

    [FromHeader(headerName: "X-Request-Counter", isRequired: false)]
    public long? RequestCounter { get; set; }
}
```

`RequestCounter` exists purely so the front end can discard out-of-order responses (e.g. a fast second
keystroke's response arriving before the first's) — the back end just echoes it back unchanged. No validation
needed on this request beyond whatever permission check the project's style requires — `Search` and
`RequestCounter` are both optional and unrestricted.

## Response type

`ListResponse<SelectListItemGuid>` for a plain `{Value, Text}` dropdown, or
`ListResponse<SelectListItemGuidWithImage>` (`Value`, `Text`, `SecondaryText`, optionally an image url) when a
secondary line of text (or an image) is useful — e.g. showing an email address under a contact's display name.
Pick whichever the target project already uses for similar dropdowns.

## Endpoint

```csharp
public sealed class List{EntityPlural}ForDropdownEndpoint
    : Endpoint<List{EntityPlural}ForDropdownRequest, ListResponse<SelectListItemGuid>>
{
    private readonly {EntityPlural}Repository _repo;

    public List{EntityPlural}ForDropdownEndpoint({EntityPlural}Repository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/{entityPluralLower}/listForDropdown");   // child: "/{entityPluralLower}/{organizationId}/listForDropdown"
        SerializerContext(List{EntityPlural}ForDropdownContext.Default);
        AllowAnonymous();   // or Policies("Master"/"User") + org-role check
    }

    public override async Task HandleAsync(List{EntityPlural}ForDropdownRequest req, CancellationToken ct)
    {
        ListResponse<SelectListItemGuid> response =
            await _repo.List{EntityPlural}ForDropdownAsync(req.Search, req.RequestCounter, ct);

        await Send.OkAsync(response);
    }
}
```

No `ValidateOutput` — nothing can fail here beyond permission, which (if applicable) is handled in
`ValidateInputAsync` alongside the org-role check for child entities.

## Repository method

```csharp
public async Task<ListResponse<SelectListItemGuid>> List{EntityPlural}ForDropdownAsync(
    string? searchTerm, long? requestCounter, CancellationToken ct = default)
{
    using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.{Name});

    DynamicParameters parameters = new();
    // child entities: parameters.Add("@organizationId", organizationId, DbType.Guid);

    string whereQuery = "";
    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
        whereQuery = " and Name like @search";
        parameters.Add("@search", $"%{searchTerm}%", DbType.String, size: 100);
        // template-style: use SearchQueryBuilder.BuildSearchSqlStringWithParams(...) instead —
        // splits the search term into words and matches them out-of-order across configured columns
    }

    // lang=sql
    string sql = $@"
select id as Value, Name as Text
from tbl{EntityPlural}
where Deleted = 0 {"{and OrganizationId = @organizationId}"}{whereQuery}
order by Name";

    CommandDefinition cmd = new(sql, parameters, cancellationToken: ct);
    return new ListResponse<SelectListItemGuid>
    {
        RequestCounter = requestCounter,
        Records = (await sqlConnection.QueryAsync<SelectListItemGuid>(cmd)).AsList()
    };
}
```

Notes:
- Cancellation-token-aware — read-only.
- Always has an explicit `order by` — SQL Server doesn't guarantee row order without one.
- No pagination — dropdown lists return the full matching set (bounded in practice by expected dataset size,
  not by an explicit page size).
- If using `SelectListItemGuidWithImage`, select the secondary-text/image columns too and map them onto
  `SecondaryText`/the image property.
