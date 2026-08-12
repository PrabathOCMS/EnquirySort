# Cross-cutting conventions (apply to every endpoint)

These rules are enforced consistently across every lesson. Violating one of them is the most likely way
generated code will look "off" compared to the rest of the codebase.

## Naming and folder layout

```
/Features
  /{EntityPlural}
    /Create{Entity}
      Create{Entity}Context.cs
      Create{Entity}Endpoint.cs
      Create{Entity}Request.cs
    /Get{Entity}
      Get{Entity}Context.cs
      Get{Entity}Endpoint.cs
      Get{Entity}Request.cs
    /Update{Entity}
      Update{Entity}Context.cs
      Update{Entity}Endpoint.cs
      Update{Entity}Request.cs
    /List{EntityPlural}ForDropdown
      List{EntityPlural}ForDropdownContext.cs
      List{EntityPlural}ForDropdownEndpoint.cs
      List{EntityPlural}ForDropdownRequest.cs
    /List{EntityPlural}ForDataTable
      List{EntityPlural}ForDataTableContext.cs
      List{EntityPlural}ForDataTableEndpoint.cs
      List{EntityPlural}ForDataTableRequest.cs
    /Delete{Entity}
      Delete{Entity}Context.cs
      Delete{Entity}Endpoint.cs
      Delete{Entity}Request.cs

/Models
  {Entity}.cs
  /ForDataTable/{Entity}ForDataTable.cs      -- template-style slim projection, only if used elsewhere

/Repositories
  {EntityPlural}Repository.cs                -- one class, one DI singleton, every query for this table
```

Namespace per feature folder: `{Project}.Features.{EntityPlural}.{Operation}{Entity}` (or
`{Project}.Features.{EntityPlural}.List{EntityPlural}ForDropdown` / `ForDataTable`).

Every new entity touches exactly one shared file: `StartupExtensions.MyAddRepositories()`, to register
`{EntityPlural}Repository` as a DI singleton. Don't touch anything else shared unless the entity introduces a
new cross-cutting enum value or `Toolbox` validator.

## Root entity vs. child-of-Organization entity

This is the primary branch point for every endpoint. Decide it first.

| Aspect | Root/tenant entity (like Organization) | Child entity (like Contact) |
|---|---|---|
| Route shape | `/{entityPlural}/create`, `/{entityPlural}/get/{id}` | `/{entityPlural}/{organizationId}/create`, `/{entityPlural}/{organizationId}/get/{id}` |
| Request extra field | none | `OrganizationId` present in the JSON body too (redundant with the route segment, but needed for permission checks and as a Dapper parameter) |
| Permission policy | `Policies("Master")` for mutations/lists, `Policies("User")` for Get, via `ValidateMasterOrUserOrganizationRoleAsync` | `Policies("User")` everywhere, via `ValidateUserOrganizationRoleAsync(organizationId, userId, UserOrganizationRole.X, _authCacheService, ct)` |
| Uniqueness scope | unique globally (`where Deleted=0 and Name=@name`) | unique **per organization** (`where Deleted=0 and OrganizationId=@organizationId and Email=@email`) |
| `sp_getapplock` resource name | `"tbl{EntityPlural}_{Column}_{sha1(upper(value))}"` | `"tbl{EntityPlural}_{Column}_{organizationId}_{sha1(upper(value))}"` — always embed the parent id so locks don't serialize across tenants |
| Query joins | none extra | every `select` also inner-joins the parent table and requires the parent's `Deleted = 0`, so a soft-deleted parent immediately hides its children even before cascade-delete runs |
| Grandchildren | n/a | same pattern recursively — a child-of-Contact would carry both `OrganizationId` and `ContactId` |

If asked to scaffold a child entity whose parent isn't Organization but another child entity, apply this same
table using the immediate parent instead of Organization.

## The fixed `HandleAsync` shape

Every mutating endpoint (Create/Update/Delete) and Get follow:

```csharp
public override async Task HandleAsync({Op}{Entity}Request req, CancellationToken ct)
{
    ValidateInput(req);                 // or ValidateInputAsync when it awaits a permission check
    if (ValidationFailed) { await Send.ErrorsAsync(); return; }

    // ... call repository ...

    ValidateOutput(queryResult, entity); // Get/mutations only — List endpoints skip this
    if (ValidationFailed) { await Send.ErrorsAsync(); return; }

    await Send.OkAsync(entity!);         // or Send.NoContentAsync() for Delete
}
```

List endpoints (Dropdown/DataTable) skip `ValidateOutput` entirely — once permission is granted, nothing about
the query itself can fail in a way the caller needs to know about.

## Validation and normalization

- **Every request property is nullable, always, no exceptions.** The back end must never trust model binding
  to reject malformed/missing JSON — all real validation happens in C#.
- Trim/normalize **before** checking for required/length: `.Trim()` for free-text names, `.Trim().ToLower()`
  for emails, uppercase for fixed-format codes. Do this at the top of `ValidateInput`, before the null checks.
- Optional fields: if blank after trimming, null them out rather than storing empty strings
  (`if (string.IsNullOrWhiteSpace(req.Notes)) req.Notes = null;`), then only validate length if non-null.
- `AddError(m => m.Field!, message, errorCode)` ties an error to a specific request field. `AddError(message,
  errorCode)` / `AddError(message)` go to general errors (used for "unknown error" and permission failures).
- Error codes are dot-namespaced: `error.{entity}.{camelCaseReason}` (e.g. `error.contact.emailIsRequired`,
  `error.contact.emailLength`). A `|{"placeholder":"value"}` JSON suffix carries template parameters for
  front-end i18n messages (split on the first `|`).
- Domain-specific format validators (email, postcode, state abbreviation, etc.) belong in `Toolbox` — check it
  for an existing one before writing a new one; only add project-specific ones there, never inline in the
  endpoint.

## `SqlQueryResult` and repository return shape

`Enums/SqlQueryResult.cs`: `UnknownError, Ok, RecordDidNotExist, RecordAlreadyExists, ConcurrencyKeyInvalid`.
Reused by nearly every mutating repository method. Only add a new value if an endpoint has a genuinely new
failure mode that doesn't fit these — don't invent a parallel enum per entity.

Create/Update/Delete repository methods return `(SqlQueryResult, {Entity}?)` (or just `SqlQueryResult` for
Delete, since it has nothing to send back). Get returns a plain nullable `{Entity}?` — no result-code tuple —
because `null` unambiguously means "didn't exist, was deleted, or (for child entities) its parent was deleted".

## `ConcurrencyKey`

- Always `byte[]` in C#, `varbinary(4)` in SQL, a **persisted computed column**:
  `CONVERT(varbinary(4), binary_checksum([col1],[col2],...))` over exactly the user-editable columns exposed on
  the Update form — never internal-only columns, never derived/computed display columns (e.g. Contact's
  `DisplayName` is excluded).
- Validated in Update/Delete requests: required, `Length <= 4`.
- Compared with `Toolbox.ByteArrayEqual(a, b)` — never `==` (byte arrays don't structurally compare in C#).
- Update's `where` clause always includes `and ConcurrencyKey = @concurrencyKey` alongside `id` (and the
  parent-id join/filter for child entities).

## Determining *why* a 0-row Update/Delete failed (fixed priority order, no extra round trip)

Use `output inserted.*, deleted.*` into a table variable during the update/delete statement, then after it:

1. Re-`select` the current row **without** the `ConcurrencyKey` filter (but still `Deleted = 0` for update,
   or without `Deleted=0` for the delete's not-found check as appropriate). Nothing comes back →
   `RecordDidNotExist`.
2. Else if `!Toolbox.ByteArrayEqual(currentRow.ConcurrencyKey, request.ConcurrencyKey)` → `ConcurrencyKeyInvalid`.
3. Else (by elimination, Update only) → `RecordAlreadyExists` — someone else claimed the unique value first.

Never do select-then-update-then-select as three separate round trips — use `output` to capture before/after
atomically in the same statement as the mutation.

## Uniqueness locking (Create, and Update when renaming the unique column)

```sql
declare @_lockResult int;
exec @_lockResult = sp_getapplock
    @Resource = @lockResourceName,   -- see naming rule in the root-vs-child table above
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 0;                -- fail fast, never wait — a wait can only ever end in failure here
```
Owned by `Transaction` so it auto-releases on `commit`/`rollback`. Combined with an
`insert/update ... where not exists (select * from tbl... where Deleted=0 and {uniqueCol}=@value [and
OrganizationId=@organizationId] [and id != @id for Update's self-exclusion])` guard. Apply this to every
Create and to Update whenever the unique/display column can be changed — don't skip it on Update just because
one tutorial example (Contact) happened to omit it; the Organization example includes it and that's the
pattern to follow for consistency and correctness.

## SQL / Dapper conventions

- One `SqlConnection` per repository method (`using`), connection string from injected `AppSettings`.
- Raw SQL string with a `// lang=sql` leading comment (IDE syntax highlighting), executed via Dapper.
- **Always** `DynamicParameters` with an explicit `DbType` and length per parameter matching the target column
  — never anonymous-object parameters (breaks SARGability, bloats the plan cache with per-call variants).
- `declare @_now datetime2(3) = sysutcdatetime()` once per statement batch, reused everywhere a timestamp is
  needed in that call, rather than calling `sysutcdatetime()` multiple times.
- `.AsList()`, never `.ToList()`, when materializing a Dapper `IEnumerable<T>` result — `AsList()` casts if
  already a list, `ToList()` always allocates a copy.
- `CancellationToken` is passed into Dapper's `CommandDefinition` **only for read-only queries** (Get,
  ListForDropdown, ListForDataTable, any `IsXExistsAsync`) so SQL Server can abort server-side work if the
  client disconnects. **Never** thread it through Create/Update/Delete — a disconnecting client must not abort
  a write already in flight.
- Multi-result-set reads (Create's result-code + created-row; ListForDataTable's count + page) use
  `QueryMultipleAsync` and a `SqlMapper.GridReader`, guarding subsequent reads with `!gridReader.IsConsumed`
  where the second result set is conditional on the first.

## COMB sequential GUID generation

Every `id` (and every other generated GUID, e.g. `_Log` row ids) is created in C# via
`RT.Comb.EnsureOrderedProvider.Sql.Create()` — **never** `Guid.NewGuid()` or `Guid.CreateVersion7()`. A random
GUID as a clustered PK causes page splits/fragmentation because SQL Server physically orders storage by the
clustered key. `Guid.CreateVersion7()` (.NET 9+) is specifically wrong here too: it puts its timestamp in the
*first* 6 bytes (correct for Postgres/RFC byte order), but SQL Server sorts `uniqueidentifier` by treating the
*last* 6 bytes as most significant — so `CreateVersion7()` still produces effectively-random insert order on
this database engine.

If a project doesn't yet have this wired up, it's a one-time addition — add this file once, exactly as-is, and
generate every future id through it:

```csharp
namespace RT.Comb;

public static class EnsureOrderedProvider
{
    private static readonly CustomNoRepeatTimestampProvider SqlNoDupeProvider = new CustomNoRepeatTimestampProvider(4);
    private static readonly CustomNoRepeatTimestampProvider UnixNoDupeProvider = new CustomNoRepeatTimestampProvider(1);
    public static readonly ICombProvider Legacy = new SqlCombProvider(new SqlDateTimeStrategy(), customTimestampProvider: SqlNoDupeProvider.GetTimestamp);
    public static readonly ICombProvider Sql = new SqlCombProvider(new UnixDateTimeStrategy(), customTimestampProvider: UnixNoDupeProvider.GetTimestamp);
    public static readonly ICombProvider PostgreSql = new PostgreSqlCombProvider(new UnixDateTimeStrategy(), customTimestampProvider: UnixNoDupeProvider.GetTimestamp);
}

public sealed class CustomNoRepeatTimestampProvider
{
    private DateTime _lastValue = DateTime.MinValue;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    public double IncrementMs { get; set; }

    public CustomNoRepeatTimestampProvider(double incrementMs = 4)
    {
        IncrementMs = incrementMs;
    }

    public DateTime GetTimestamp()
    {
        DateTime now = DateTime.UtcNow;
        _semaphore.Wait();
        try
        {
            if ((now - _lastValue).TotalMilliseconds < IncrementMs)
            {
                now = _lastValue.AddMilliseconds(IncrementMs);
            }
            _lastValue = now;
        }
        finally
        {
            _semaphore.Release();
        }
        return now;
    }
}
```

The no-repeat-timestamp wrapper on top of stock `RT.Comb` guarantees strict ordering even for GUIDs generated
within the same millisecond (relevant under load or bulk operations).

## Soft delete, everywhere

`Deleted bit`, default `0`. **Never** `delete from` — only `update ... set Deleted = 1`. Every `select`
anywhere filters `Deleted = 0` on the entity's own table, and (for child entities) on the joined parent table
too.

## Status codes and error envelope

- `200 OK` — Create/Get/Update success, with the entity as body.
- `204 No Content` — Delete success, no body.
- `400 Bad Request` — any validation/business failure, via `Send.ErrorsAsync()`.
- `403 Forbidden` — `Send.ForbiddenAsync()` for an authenticated call with no resolvable user id (edge case,
  template-style projects only).
- `500` — unhandled exceptions (template-style projects only, via `MyExceptionHandler`).
- Error body shape depends on which variant the project uses (see Step 0 in SKILL.md): raw
  `FastEndpoints.ErrorResponse` (`errors: { camelCaseField: [messages] }`) for the hand-rolled style, or
  `MyErrorResponse` (`errorMessages: { PascalField: [{message, errorCode}] }`, plus `fatalError`,
  `concurrencyKeyInvalid`, `additionalData`, `traceId`) for the template style. In the template style, an
  endpoint signals those extra flags via `HttpContext.Items["FatalError"]`, `["ConcurrencyKeyInvalid"]`, and
  `["ErrorAdditionalData"]` (JSON-serialized current entity) rather than setting them directly on the response.

## `JsonSerializerContext` per endpoint

Every endpoint gets its own `{Operation}{Entity}Context : JsonSerializerContext` with
`[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]` and a `[JsonSerializable]`
attribute for every type sent or received by that endpoint — the request type, the response/entity type, and
whichever error-response type(s) the project uses. Missing one causes a runtime `NotSupportedException` on
serialization, not a compile error — double-check this list is complete for each generated endpoint.

## `IsXExistsAsync`

Only add a reusable `Is{Entity}ExistsAsync(id, ct)` repository method when another entity's Create/Update will
actually need to validate a foreign reference before inserting (e.g. validating a `ContactId` on some future
`Meeting` entity). Don't add it speculatively for a root entity whose existence is already implied by the
permission check against it.
