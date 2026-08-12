# Delete{Entity} endpoint

Read `reference/conventions.md` first — this file only covers what's specific to Delete. Soft delete only,
always — never `delete from`.

## Request

```csharp
public sealed class Delete{Entity}Request
{
    public Guid? OrganizationId { get; set; }   // child entities only
    public Guid? id { get; set; }
    public byte[]? ConcurrencyKey { get; set; }
}
```

## Endpoint

```csharp
public sealed class Delete{Entity}Endpoint : Endpoint<Delete{Entity}Request>
{
    private readonly {EntityPlural}Repository _repo;

    public Delete{Entity}Endpoint({EntityPlural}Repository repo) => _repo = repo;

    public override void Configure()
    {
        Post("/{entityPluralLower}/delete");   // child: "/{entityPluralLower}/{organizationId}/delete"
        SerializerContext(Delete{Entity}Context.Default);
        AllowAnonymous();   // or Policies("Master") / ValidateUserOrganizationRoleAsync
    }

    public override async Task HandleAsync(Delete{Entity}Request req, CancellationToken ct)
    {
        ValidateInput(req);
        if (ValidationFailed) { await Send.ErrorsAsync(); return; }

        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        (Guid? userId, string? adminUserDisplayName) = User.GetIdAndName();

        SqlQueryResult queryResult =
            await _repo.Delete{Entity}Async(req, userId, adminUserDisplayName, remoteIpAddress);

        ValidateOutput(queryResult);
        if (ValidationFailed) { await Send.ErrorsAsync(); return; }

        await Send.NoContentAsync();
    }

    private void ValidateInput(Delete{Entity}Request req)
    {
        if (!req.id.HasValue)
            AddError(m => m.id!, "Id is required.", "error.{entityLower}.idIsRequired");

        if (req.ConcurrencyKey is null)
            AddError(m => m.ConcurrencyKey!, "Concurrency key is required.",
                "error.{entityLower}.concurrencyKeyIsRequired");
        else if (req.ConcurrencyKey.Length > 4)
            AddError(m => m.ConcurrencyKey!, "Concurrency key must be 4 bytes or less.",
                "error.{entityLower}.concurrencyKeyLength|{\"length\":\"4\"}");
    }

    private void ValidateOutput(SqlQueryResult queryResult)
    {
        switch (queryResult)
        {
            case SqlQueryResult.Ok:
                return;
            case SqlQueryResult.RecordDidNotExist:
                HttpContext.Items["FatalError"] = true;
                AddError("The {entityLower} was already deleted.", "error.{entityLower}.didNotExist");
                break;
            case SqlQueryResult.ConcurrencyKeyInvalid:
                AddError("The {entityLower}'s data has changed since you last accessed this page.",
                    "error.{entityLower}.concurrencyKeyInvalid");
                break;
            default:
                AddError("An unknown error occurred.", "error.unknown");
                break;
        }
    }
}
```

Only three `SqlQueryResult` cases apply — there's no `RecordAlreadyExists` for delete (nothing about
uniqueness is being changed).

## Repository method

No `sp_getapplock` needed (no uniqueness constraint touched).

```sql
declare @_result int = 0;
declare @_now datetime2(3) = sysutcdatetime();
declare @_data table (id uniqueidentifier, Name nvarchar(100));

update tbl{EntityPlural}
set Deleted = 1, UpdatedDateUtc = @_now
output inserted.id, inserted.Name into @_data
where Deleted = 0 and id = @id and ConcurrencyKey = @concurrencyKey
    {and OrganizationId = @organizationId};

if @@ROWCOUNT = 1
begin
    set @_result = 1;
    insert into tbl{EntityPlural}_Log
        (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
         {Entity}Id, Name, Deleted, OldName, OldDeleted, LogAction)
    select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, null,
           d.id, d.Name, 1, d.Name, 0, 'Delete'
    from @_data d;
end
else
    set @_result = 2;   -- disambiguate RecordDidNotExist vs ConcurrencyKeyInvalid same as Update, see conventions.md

select @_result;
```

`Old{Column}` = same value as `{Column}` for every column except `Deleted` (`OldDeleted = 0`,
`Deleted = 1`) — nothing else actually changed.

## Cascading deletes

When this entity is deleted, any of **its own** child entities (and their `_Log` tables) must also be
soft-deleted, in the same transaction, with `CascadeFrom = 'tbl{ThisEntityPlural}'` and `CascadeLogId` pointing
at this delete's own log row. This is a manual addition per relationship, not auto-generated — when scaffolding
a new child entity, go back and add its cascade into the **parent's** Delete method (build order:
child endpoints exist first, then wire the cascade into the parent, so this code is only ever written once per
relationship rather than revisited repeatedly as more children are added).

For cascading into a join/junction table where N affected rows each need a distinct sequential-GUID log id in
one round trip (no per-row loop), use two `ROW_NUMBER()`-ordered subqueries joined by row number to pair each
affected row with one of N pre-generated COMB GUIDs — see the Organization delete cascade into
`tblUserOrganizationJoin`/`tblUserOrganizationJoinHistories` (lessons 023-025, 033) for the concrete pattern if
the target project is template-based.

If this new entity itself has no children yet, still leave a `-- TODO: cascade-delete child records here once
they exist` comment at the point in the SQL where the cascade will go, matching the tutorial's own convention.
