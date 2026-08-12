# Create{Entity} endpoint

Read `reference/conventions.md` first — this file only covers what's specific to Create.

## Request

Plain POCO, every property nullable:

```csharp
public sealed class Create{Entity}Request
{
    public Guid? OrganizationId { get; set; }   // child entities only — also present in the route
    public string? Name { get; set; }           // ...one property per user-editable field
}
```

## Context

```csharp
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Create{Entity}Request))]
[JsonSerializable(typeof({Entity}))]
[JsonSerializable(typeof(FastEndpoints.ErrorResponse))]     // or MyErrorResponse — match Step 0's detection
internal sealed partial class Create{Entity}Context : JsonSerializerContext { }
```

## Endpoint

```csharp
public sealed class Create{Entity}Endpoint : Endpoint<Create{Entity}Request, {Entity}>
{
    private readonly {EntityPlural}Repository _repo;

    public Create{Entity}Endpoint({EntityPlural}Repository repo) => _repo = repo;

    public override void Configure()
    {
        Post("/{entityPluralLower}/create");                       // child: "/{entityPluralLower}/{organizationId}/create"
        SerializerContext(Create{Entity}Context.Default);
        AllowAnonymous();   // or Policies("Master"/"User") + the org-role check — match Step 0's detection
    }

    public override async Task HandleAsync(Create{Entity}Request req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed) { await Send.ErrorsAsync(); return; }

        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        (Guid? userId, string? adminUserDisplayName) = User.GetIdAndName();   // template-style only

        (SqlQueryResult queryResult, {Entity}? entity) =
            await _repo.Create{Entity}Async(req, userId, adminUserDisplayName, remoteIpAddress);

        ValidateOutput(queryResult, entity);
        if (ValidationFailed) { await Send.ErrorsAsync(); return; }

        await Send.OkAsync(entity!);
    }

    private void ValidateInput(Create{Entity}Request req)
    {
        req.Name = req.Name?.Trim();
        if (string.IsNullOrWhiteSpace(req.Name))
            AddError(m => m.Name!, "Name is required.", "error.{entityLower}.nameIsRequired");
        else if (req.Name.Length > 100)
            AddError(m => m.Name!, "Name must be 100 characters or less.",
                "error.{entityLower}.nameLength|{\"length\":\"100\"}");
        // repeat trim + required/length/format checks for every other user-editable field
    }

    private void ValidateOutput(SqlQueryResult queryResult, {Entity}? entity)
    {
        switch (queryResult)
        {
            case SqlQueryResult.Ok:
                if (entity is null) AddError("An unknown error occurred.", "error.unknown");
                return;
            case SqlQueryResult.RecordAlreadyExists:
                AddError(m => m.Name!, "Another {entityLower} already exists with the specified name.",
                    "error.{entityLower}.nameExists");
                break;
            default:
                AddError("An unknown error occurred.", "error.unknown");
                break;
        }
    }
}
```

## Repository method

```csharp
public async Task<(SqlQueryResult, {Entity}?)> Create{Entity}Async(
    Create{Entity}Request req, Guid? adminUserUid, string? adminUserDisplayName, string? remoteIpAddress)
{
    using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.{Name});

    Guid id = _combProvider.Create();   // RT.Comb sequential-GUID generator, injected/available on the repository

    // lang=sql
    string sql = @"
declare @_result int = 0;
declare @_now datetime2(3) = sysutcdatetime();
declare @_lockResult int;
declare @_data table (id uniqueidentifier, Name nvarchar(100), ConcurrencyKey varbinary(4));

begin transaction;

exec @_lockResult = sp_getapplock
    @Resource = @lockResourceName, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;

if @_lockResult < 0
begin
    set @_result = 2;
    rollback transaction;
end
else
begin
    insert into tbl{EntityPlural} (id, {OrganizationIdIfChild}Name, InsertDateUtc, UpdatedDateUtc)
    output inserted.id, inserted.Name, inserted.ConcurrencyKey into @_data
    select @id, {@organizationId, }@name, @_now, @_now
    where not exists (
        select * from tbl{EntityPlural}
        where Deleted = 0 {and OrganizationId = @organizationId} and Name = @name);

    if @@ROWCOUNT = 1
    begin
        set @_result = 1;
        insert into tbl{EntityPlural}_Log
            (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
             {Entity}Id, Name, LogAction)
        select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, null,
               d.id, d.Name, 'Insert'
        from @_data d;
    end
    else
        set @_result = 2;

    commit transaction;
end

select @_result;
select * from @_data;";

    DynamicParameters parameters = new();
    parameters.Add("@id", id, DbType.Guid);
    parameters.Add("@logId", _combProvider.Create(), DbType.Guid);
    parameters.Add("@name", req.Name, DbType.String, size: 100);
    parameters.Add("@lockResourceName",
        $"tbl{EntityPlural}_Name_{Toolbox.Sha1Upper(req.Name!)}", DbType.AnsiString, size: 200);
    parameters.Add("@adminUserUid", adminUserUid, DbType.Guid);
    parameters.Add("@adminUserDisplayName", adminUserDisplayName, DbType.String, size: 200);
    parameters.Add("@remoteIpAddress", remoteIpAddress, DbType.AnsiString, size: 45);
    // child entities: parameters.Add("@organizationId", req.OrganizationId, DbType.Guid);
    // and fold OrganizationId into the lock resource name too

    using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(sql, parameters);
    int resultCode = await gridReader.ReadFirstAsync<int>();
    {Entity}? entity = !gridReader.IsConsumed
        ? await gridReader.ReadFirstOrDefaultAsync<{Entity}>()
        : null;

    SqlQueryResult result = resultCode switch
    {
        1 => SqlQueryResult.Ok,
        2 => SqlQueryResult.RecordAlreadyExists,
        _ => SqlQueryResult.UnknownError
    };

    return (result, entity);
}
```

Notes:
- Not cancellation-token-aware — this is a write.
- Only returns the entity on success; `null` on any failure path.
- For a child entity, the `not exists` guard and the lock resource name both fold in `OrganizationId`; the
  insert also stores `OrganizationId`.
- If the entity has a derived display column (like Contact's `DisplayName`), compute it in this SQL (or in a
  computed column at the schema level) — never accept it from the request.

## DI registration

Add to `StartupExtensions.MyAddRepositories()`:
```csharp
services.AddSingleton<{EntityPlural}Repository>();
```
(Only needs doing once per entity — shared across all six endpoint methods on the same repository class.)
