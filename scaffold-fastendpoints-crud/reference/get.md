# Get{Entity} endpoint

Read `reference/conventions.md` first — this file only covers what's specific to Get.

Get endpoints are read-only and **never write to the `_Log` table** — no audit trail for reads, only for
mutations. No IP address / user-id / display-name plumbing is needed.

## Request

```csharp
public sealed class Get{Entity}Request
{
    public Guid? OrganizationId { get; set; }   // child entities only, bound from the route
    public Guid? id { get; set; }               // bound from the route
}
```

## Context

```csharp
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Get{Entity}Request))]
[JsonSerializable(typeof({Entity}))]
[JsonSerializable(typeof(FastEndpoints.ErrorResponse))]     // or MyErrorResponse — match Step 0's detection
internal sealed partial class Get{Entity}Context : JsonSerializerContext { }
```

## Endpoint

```csharp
public sealed class Get{Entity}Endpoint : Endpoint<Get{Entity}Request, {Entity}>
{
    private readonly {EntityPlural}Repository _repo;

    public Get{Entity}Endpoint({EntityPlural}Repository repo) => _repo = repo;

    public override void Configure()
    {
        Get("/{entityPluralLower}/get/{id}");     // child: "/{entityPluralLower}/{organizationId}/get/{id}"
        SerializerContext(Get{Entity}Context.Default);
        AllowAnonymous();   // or Policies("User") + ValidateMasterOrUserOrganizationRoleAsync / ValidateUserOrganizationRoleAsync
    }

    public override async Task HandleAsync(Get{Entity}Request req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed) { await Send.ErrorsAsync(); return; }

        {Entity}? entity = await _repo.Get{Entity}Async(
            /* req.OrganizationId!.Value, if child */ req.id!.Value, ct);

        ValidateOutput(entity);
        if (ValidationFailed) { await Send.ErrorsAsync(); return; }

        await Send.OkAsync(entity!);
    }

    private void ValidateInput(Get{Entity}Request req)
    {
        if (!req.id.HasValue)
            AddError(m => m.id!, "Id is required.", "error.{entityLower}.idIsRequired");
        // child entities: same check for req.OrganizationId
    }

    private void ValidateOutput({Entity}? entity)
    {
        if (entity is null)
        {
            HttpContext.Items["FatalError"] = true;   // template-style only — front end redirects away
            AddError("The selected {entityLower} did not exist.", "error.{entityLower}.didNotExist");
        }
    }
}
```

## Repository method

```csharp
public async Task<{Entity}?> Get{Entity}Async(Guid id, CancellationToken cancellationToken = default)
{
    using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.{Name});

    // lang=sql
    string sql = @"
select {Entity's columns}
from tbl{EntityPlural}
-- child entity: inner join tblOrganizations o on o.id = tbl{EntityPlural}.OrganizationId and o.Deleted = 0
where tbl{EntityPlural}.Deleted = 0
  and tbl{EntityPlural}.id = @id
  -- child entity: and tbl{EntityPlural}.OrganizationId = @organizationId
";

    DynamicParameters parameters = new();
    parameters.Add("@id", id, DbType.Guid);
    // child entities: parameters.Add("@organizationId", organizationId, DbType.Guid);

    CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
    return await sqlConnection.QueryFirstOrDefaultAsync<{Entity}>(cmd);
}
```

Notes:
- Cancellation-token-aware — this is a read.
- No `SqlQueryResult` tuple — plain nullable return. `null` covers "didn't exist", "was soft-deleted", and for
  child entities "its parent was soft-deleted or didn't exist" all at once (the join handles the last case).
- Double-check every column exposed in the front-end form/detail view is present in the `select` list —
  a missed column here is the most common Get-endpoint bug, and is easy to catch during Bruno testing by
  diffing the JSON response against the table's columns in SSMS.
