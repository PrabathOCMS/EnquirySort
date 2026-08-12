# Database schema conventions

## Table shape

Every entity gets two tables: `tbl{EntityPlural}` and `tbl{EntityPlural}_Log`.

```sql
create table tbl{EntityPlural}
(
    id                  uniqueidentifier not null
                            constraint DF_tbl{EntityPlural}_id default (newid())
                            constraint PK_tbl{EntityPlural} primary key clustered,
    -- child entities only:
    -- OrganizationId  uniqueidentifier not null,
    {UserEditableColumns},           -- the fields exposed on the Create/Update forms
    {DerivedColumns},                -- e.g. DisplayName, computed server-side, NOT part of ConcurrencyKey
    InsertDateUtc       datetime2(3) not null
                            constraint DF_tbl{EntityPlural}_InsertDateUtc default (sysutcdatetime()),
    UpdatedDateUtc      datetime2(3) not null
                            constraint DF_tbl{EntityPlural}_UpdatedDateUtc default (sysutcdatetime()),
    Deleted             bit not null
                            constraint DF_tbl{EntityPlural}_Deleted default (0),
    ConcurrencyKey      as convert(varbinary(4), binary_checksum({UserEditableColumns list, comma-separated}))
                            persisted not null
);
```

Rules:
- `id` is `uniqueidentifier`, clustered PK. Generated in C# using the COMB sequential-GUID provider
  (`RT.Comb.EnsureOrderedProvider.Sql.Create()`), **not** `Guid.NewGuid()`/`Guid.CreateVersion7()` — SQL Server
  sorts GUIDs by their last 6 bytes, so a COMB generator that accounts for that ordering avoids clustered-index
  page-split fragmentation that plain random or RFC-order GUIDs would cause.
- `ConcurrencyKey` is **always** a persisted computed column over exactly the columns the Update endpoint lets
  the client change — never internal/system columns (`InsertDateUtc`, `Deleted`, etc.), never derived display
  columns computed server-side.
- `nvarchar` for free-text user input (names, notes). `varchar` for strictly-constrained ASCII input
  (postcodes, state/country codes).
- Child entities add `OrganizationId uniqueidentifier not null` (or the immediate parent's id column) as the
  **leading** column of every index that needs it — no FK constraint (see below).
- **No foreign key constraints anywhere**, deliberately — kept flexible for archival/import/repair scenarios.
  Referential integrity is enforced in application code (joins filtering on `Deleted = 0`, and optionally an
  `Is{Parent}ExistsAsync` check before insert).
- All timestamps are stored in UTC, with the column suffixed `Utc` (`InsertDateUtc`, `UpdatedDateUtc`). If local
  time is also genuinely needed for application logic (not just display formatting), store it as a **separate**
  `XxxLocal` column rather than converting on the fly.

## Schema-editing gotchas (SSMS)

Worth checking before the first table-design session on a project, and worth remembering any time an existing
table is altered:

- **`Tools → Options → Designers → Table and Database Designers → Prevent saving changes that require table
  re-creation`** — turn this OFF. Left on, SSMS's table designer silently refuses to save ordinary schema
  changes (like adding a computed column) that require the table to be recreated under the hood.
- **Editing a computed column's formula via the table designer silently drops it from any index it was part
  of** — computed columns can't be updated while indexed, and SSMS does not re-add it afterward. After editing
  a `ConcurrencyKey` (or any computed column) formula, manually re-check and re-add it to its index. The same
  applies whenever a new column is added generally — remember to add it to whichever existing indexes should
  cover it.

## Indexes

- A unique **filtered** index on the natural-uniqueness column(s), scoped to `Deleted = 0` (and to
  `OrganizationId` for child tables, as the leading key column) — this allows soft-deleted duplicates to
  coexist while still enforcing "no duplicate active record":
  ```sql
  create unique nonclustered index UX_tbl{EntityPlural}_{UniqueColumn}
      on tbl{EntityPlural} ({OrganizationId, }{UniqueColumn})
      where Deleted = 0;
  ```
- A separate non-unique filtered index on whichever column drives the default display/dropdown sort order
  (typically the same or a related column), also scoped by `Deleted = 0` (and `OrganizationId` for child
  tables):
  ```sql
  create nonclustered index IX_tbl{EntityPlural}_{SortColumn}
      on tbl{EntityPlural} ({OrganizationId, }{SortColumn})
      where Deleted = 0;
  ```

## Log table

```sql
create table tbl{EntityPlural}_Log
(
    id                      uniqueidentifier not null
                                constraint PK_tbl{EntityPlural}_Log primary key clustered,
    InsertDateUtc           datetime2(3) not null
                                constraint DF_tbl{EntityPlural}_Log_InsertDateUtc default (sysutcdatetime()),
    UpdatedByUid            uniqueidentifier null,
    UpdatedByDisplayName    nvarchar(200) null,
    UpdatedByIpAddress      varchar(45) null,
    LogDescription          nvarchar(max) null,
    {Entity}Id              uniqueidentifier not null,   -- no FK constraint, same "no FKs" rule as above
    {CurrentValueColumns},                                -- mirrors the parent table's mutable + Deleted columns
    {OldValueColumns},                                    -- Old{Column} for every mutable column above, plus OldDeleted
    LogAction               varchar(6) not null,           -- 'Insert' | 'Update' | 'Delete'
    CascadeFrom             varchar(128) null,             -- set when this log row was written by a parent's cascade delete
    CascadeLogId            uniqueidentifier null          -- points at the parent log row that triggered the cascade
);
```

- `Insert`: `{Column}` = the just-inserted value, `Old{Column}` = `null` for everything (or omit old-columns
  entirely if the project's convention leaves them null on insert — check an existing `_Log` table for the
  actual pattern in use).
- `Update`: `{Column}` = new value, `Old{Column}` = value before the update (captured via `output
  inserted.X ..., deleted.X ...` in the same statement as the update — never a separate select).
- `Delete`: `Deleted` = `1`, `OldDeleted` = `0`, all other `{Column}`/`Old{Column}` pairs equal (nothing else
  changed).
- Populate `CascadeFrom`/`CascadeLogId` only on log rows written as a side effect of a parent entity's delete
  cascading down to this entity — see `reference/delete.md`.

## Migration files

Follow whatever migration convention the target project already uses (e.g. `InitialSchema.sql` +
`MigrationGuardSetup.sql` in the template-based projects). Add the new table(s)/index(es) as a new migration
script rather than editing an already-applied one.
