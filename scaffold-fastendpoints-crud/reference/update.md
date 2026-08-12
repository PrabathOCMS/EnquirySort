# Update{Entity} endpoint

Read `reference/conventions.md` first, especially the `ConcurrencyKey` and "determining why a 0-row
Update/Delete failed" sections — this file only covers what's specific to Update.

## Request

Create's request plus `id` and `ConcurrencyKey`:

```csharp
public sealed class Update{Entity}Request
{
    public Guid? OrganizationId { get; set; }   // child entities only
    public Guid? id { get; set; }
    public string? Name { get; set; }           // ...all of Create's editable fields repeated
    public byte[]? ConcurrencyKey { get; set; }
}
```

## Context

Same shape as Create's — `[JsonSerializable]` for `Update{Entity}Request`, `{Entity}`, and the project's error
response type(s).

## Endpoint

```csharp
public sealed class Update{Entity}Endpoint : Endpoint<Update{Entity}Request, {Entity}>
{
    private readonly {EntityPlural}Repository _repo;

    public Update{Entity}Endpoint({EntityPlural}Repository repo) => _repo = repo;

    public override void Configure()
    {
        Post("/{entityPluralLower}/update");     // child: "/{entityPluralLower}/{organizationId}/update"
        SerializerContext(Update{Entity}Context.Default);
        AllowAnonymous();   // or Policies("Master"/"User") + org-role check
    }

    public override async Task HandleAsync(Update{Entity}Request req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed) { await Send.ErrorsAsync(); return; }

        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        (Guid? userId, string? adminUserDisplayName) = User.GetIdAndName();

        (SqlQueryResult queryResult, {Entity}? entity) =
            await _repo.Update{Entity}Async(req, userId, adminUserDisplayName, remoteIpAddress);

        ValidateOutput(queryResult, entity);
        if (ValidationFailed) { await Send.ErrorsAsync(); return; }

        await Send.OkAsync(entity!);
    }

    private void ValidateInput(Update{Entity}Request req)
    {
        if (!req.id.HasValue)
            AddError(m => m.id!, "Id is required.", "error.{entityLower}.idIsRequired");

        req.Name = req.Name?.Trim();
        if (string.IsNullOrWhiteSpace(req.Name))
            AddError(m => m.Name!, "Name is required.", "error.{entityLower}.nameIsRequired");
        else if (req.Name.Length > 100)
            AddError(m => m.Name!, "Name must be 100 characters or less.",
                "error.{entityLower}.nameLength|{\"length\":\"100\"}");
        // repeat for every other editable field

        if (req.ConcurrencyKey is null)
            AddError(m => m.ConcurrencyKey!, "Concurrency key is required.",
                "error.{entityLower}.concurrencyKeyIsRequired");
        else if (req.ConcurrencyKey.Length > 4)
            AddError(m => m.ConcurrencyKey!, "Concurrency key must be 4 bytes or less.",
                "error.{entityLower}.concurrencyKeyLength|{\"length\":\"4\"}");
    }

    private void ValidateOutput(SqlQueryResult queryResult, {Entity}? entity)
    {
        switch (queryResult)
        {
            case SqlQueryResult.Ok:
                if (entity is null) AddError("An unknown error occurred.", "error.unknown");
                return;
            case SqlQueryResult.RecordDidNotExist:
                HttpContext.Items["FatalError"] = true;
                AddError("The {entityLower} was deleted since you last accessed this page.",
                    "error.{entityLower}.deletedSinceAccessedPage");
                break;
            case SqlQueryResult.RecordAlreadyExists:
                AddError(m => m.Name!, "Another {entityLower} already exists with the specified name.",
                    "error.{entityLower}.nameExists");
                break;
            case SqlQueryResult.ConcurrencyKeyInvalid:
                HttpContext.Items["ConcurrencyKeyInvalid"] = true;
                HttpContext.Items["ErrorAdditionalData"] =
                    JsonSerializer.Serialize(entity, Update{Entity}Context.Default.{Entity});
                AddError(
                    "The {entityLower}'s data has changed since you last accessed this page. Please review " +
                    "the current updated version of the data below, then submit your changes again if you " +
                    "wish to overwrite.",
                    "error.{entityLower}.concurrencyKeyInvalid");
                break;
            default:
                AddError("An unknown error occurred.", "error.unknown");
                break;
        }
    }
}
```

Update always returns the **current row** whether it succeeded or failed (so the front end can render a
"here's what actually changed" comparison on a concurrency conflict) — unlike Create, which only returns a
row on success.

## Repository method

Same overall shape as Create's (see `reference/create.md`), with these differences:

1. `where` clause on the update statement includes `and ConcurrencyKey = @concurrencyKey` in addition to `id`
   `and Deleted = 0` (and the parent-id filter/join for child entities).
2. The uniqueness guard adds `and id != @id` (self-exclusion) so renaming a record without actually changing
   its unique value doesn't look like a collision with itself:
   ```sql
   where not exists (
       select * from tbl{EntityPlural}
       where Deleted = 0 {and OrganizationId = @organizationId} and Name = @name and id != @id)
   ```
3. Use `sp_getapplock` guarding this update exactly as in Create (same resource-name convention) whenever the
   unique/display column can change via this endpoint — apply this even though the tutorial's Contact example
   omitted it, since the Organization example (the more complete reference) includes it and it's the safer,
   consistent choice.
4. Capture before/after atomically with `output inserted.*, deleted.*` into a table variable, in the same
   statement as the `update`.
5. After the update, if `@@ROWCOUNT = 0`, run the disambiguation query from `reference/conventions.md`
   ("Determining why a 0-row Update/Delete failed") to resolve `RecordDidNotExist` vs. `ConcurrencyKeyInvalid`
   vs. `RecordAlreadyExists`, and re-select the current row regardless of outcome so the endpoint can always
   return it.
6. Write a `tbl{EntityPlural}_Log` row with `LogAction = 'Update'`, `{Column}` = new value, `Old{Column}` =
   value captured from `deleted.*`.

```csharp
public async Task<(SqlQueryResult, {Entity}?)> Update{Entity}Async(
    Update{Entity}Request req, Guid? adminUserUid, string? adminUserDisplayName, string? remoteIpAddress)
{
    using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.{Name});

    // lang=sql
    string sql = @"
declare @_result int = 0;
declare @_now datetime2(3) = sysutcdatetime();
declare @_lockResult int;
declare @_data table (id uniqueidentifier, Name nvarchar(100), OldName nvarchar(100), ConcurrencyKey varbinary(4));

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
    update tbl{EntityPlural}
    set Name = @name, UpdatedDateUtc = @_now
    output inserted.id, inserted.Name, deleted.Name, inserted.ConcurrencyKey into @_data
    where Deleted = 0 and id = @id and ConcurrencyKey = @concurrencyKey
        {and OrganizationId = @organizationId}
        and not exists (
            select * from tbl{EntityPlural}
            where Deleted = 0 {and OrganizationId = @organizationId} and Name = @name and id != @id);

    if @@ROWCOUNT = 1
    begin
        set @_result = 1;
        insert into tbl{EntityPlural}_Log (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress,
            LogDescription, {Entity}Id, Name, OldName, LogAction)
        select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, null,
               d.id, d.Name, d.OldName, 'Update'
        from @_data d;
    end
    else
        set @_result = 2;   -- resolved to a specific SqlQueryResult below by the disambiguation select

    commit transaction;
end

select @_result;
-- disambiguation + current-row select (see conventions.md) when @_result <> 1
select * from tbl{EntityPlural} where Deleted = 0 and id = @id {and OrganizationId = @organizationId};
";

    // ... DynamicParameters as in Create, plus @concurrencyKey (DbType.Binary, size 4) ...
    // ... map result code + disambiguate 0-row case against the current-row select as in conventions.md ...
}
```

Fill in the disambiguation logic (RecordDidNotExist / ConcurrencyKeyInvalid / RecordAlreadyExists) in C# against
the current-row result set, per the priority order in `reference/conventions.md`.
