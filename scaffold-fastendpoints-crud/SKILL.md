---
name: scaffold-fastendpoints-crud
description: Use when adding CRUD endpoints (Create/Get/Update/Delete/ListForDropdown/ListForDataTable) for a new or existing entity to a FastEndpoints + Dapper + SQL Server backend built on the WebPortalTutorial conventions (vertical-slice Features/ folders, soft delete, computed ConcurrencyKey, sp_getapplock uniqueness locks, COMB GUID ids, SelectListItem dropdowns). Triggers on requests like "add a CRUD endpoint for X", "scaffold the Product entity", "implement Create/Get/Update for Y", "add a child entity under Organization", or when working inside a Features/ folder that already contains Organization- or Contact-style endpoints. Not for the Svelte/Routify front end.
---

# Scaffolding a FastEndpoints CRUD entity

This skill generates a new entity's backend CRUD slice following the conventions taught across
`WebPortalTutorial/Tutorial` lessons 001-044 (Organization = root/tenant entity, Contact = child-of-Organization
entity). It produces C# source (Request/Context/Endpoint/Repository classes, model, DI registration), the SQL
DDL for the table + log table, and Bruno test stubs.

**Do not generate code from memory of "typical CRUD" patterns.** This codebase's conventions are specific and
consistently enforced (all-nullable requests, soft delete only, `ConcurrencyKey` as a computed column,
`sp_getapplock`-guarded uniqueness, `.AsList()` not `.ToList()`, `output inserted/deleted` instead of
select-then-mutate, cancellation tokens only on reads, etc.). Load the reference file for whichever endpoint
you're generating before writing it, and load `reference/conventions.md` first regardless of which endpoints
you're building — every endpoint depends on those rules.

## Step 0 — Detect the target project's actual style before writing anything

The tutorial shows **two versions** of this pattern: a hand-rolled version (lessons 007-026, `AllowAnonymous()`,
raw `FastEndpoints.ErrorResponse`) and a template-generated version (lessons 027+, `Policies("Master"/"User")` +
`ValidateUserOrganizationRoleAsync`/`ValidateMasterOrUserOrganizationRoleAsync`, `MyErrorResponse`,
`SearchQueryBuilder`). A real project will be using one or the other (almost certainly the template version) —
**never assume**. Before generating any file:

1. Open one existing `Features/{SomeEntity}/{Create,Get}...Endpoint.cs` in the target project (Organization or
   Contact's, if present) and copy whichever auth/error/search pattern it actually uses.
2. Check `Program.cs` / `StartupExtensions.cs` for `MyAddRepositories()`, `MyUseFastEndpoints()`, and whether
   `MyErrorResponse` exists — that tells you which error envelope and exception-handling middleware is in play.
3. Check `Enums/SqlQueryResult.cs` and `Enums/SortType.cs` for any project-specific values already added.
4. Check `Utilities/Toolbox.cs` (and any `ToolboxBasic.cs`/`ToolboxXxx.cs` partials) for existing validators
   (`IsValidEmail`, etc.) before writing a new one.
5. Match existing file/folder casing, connection-string property name, and namespace root exactly.

If no existing entity is present to copy from, ask the user which variant to use (hand-rolled vs. templated)
rather than guessing — the two produce meaningfully different endpoint code.

## Step 1 — Gather entity requirements

Ask (or infer from context) whatever isn't already obvious:

- **Entity name**, singular and plural (e.g. `Product` / `Products`). Drives every class/table/route name.
- **Root/tenant entity or child-of-Organization entity?** This is the single biggest branch point — it changes
  routes, request shape, permission checks, uniqueness scope, and every SQL query's join/filter. See the
  "root vs. child" table in `reference/conventions.md`.
  If child, is it a child of Organization directly, or of another child entity (grandchild)? Grandchildren
  follow the same pattern recursively (add the immediate parent's id to the route/request/joins).
- **Fields**: name, C# type, nullability, SQL type + length, whether user-editable (→ included in
  `ConcurrencyKey`), whether it's the natural-uniqueness column, whether it's the display/search column,
  max length, any custom format validation needed (email, postcode, etc. — check `Toolbox` first).
- **Derived/computed display fields** (e.g. Contact's `DisplayName`) — computed server-side in the repository,
  never accepted from the client, excluded from `ConcurrencyKey`.
- **Which list endpoints does it need?** ListForDropdown only makes sense if there's a displayable column.
  ListForDataTable only if the entity is browsed/managed in a paged table UI. Skip whichever isn't needed —
  don't generate unused endpoints.
- **File/image upload field?** If yes, this needs `ImageStorageHelpers`/`ImageStorageRepository` wiring, which
  is out of scope for this skill's base 6-endpoint shape — flag it and ask before improvising.
- **Does anything else need to validate this entity's existence before referencing it** (i.e. will a future
  child entity need `Is{Entity}ExistsAsync`)? Only add that repository method if there's a concrete consumer.

## Step 2 — Generate in this order

1. SQL DDL: `tbl{EntityPlural}` + `tbl{EntityPlural}_Log` + indexes → `reference/database-schema.md`.
2. `Models/{Entity}.cs` (and `Models/ForDataTable/{Entity}ForDataTable.cs` if the template-style slim
   projection is in use).
3. `Repositories/{EntityPlural}Repository.cs` — one method per endpoint, in Create → Get → Update →
   ListForDropdown → ListForDataTable → Delete order (matches the tutorial's own build order and keeps the
   uniqueness-lock/concurrency logic consistent across methods written in one pass).
4. `Features/{EntityPlural}/{Operation}/{Operation}Request.cs`, `...Context.cs`, `...Endpoint.cs` for each
   endpoint being built — see `reference/create.md`, `get.md`, `update.md`, `delete.md`, `list-dropdown.md`,
   `list-datatable.md`.
5. Register the repository in `StartupExtensions.MyAddRepositories()`.
6. If this is a child entity's Delete, or this new entity has children of its own, wire the cascade-delete
   TODO into the parent's/this entity's Delete method (see "Cascading deletes" in `reference/delete.md`).
7. Bruno test requests — see `reference/testing.md` for the per-endpoint request sequence to stub out.

If you need the reasoning behind a rule (e.g. to judge an edge case, or to explain "why isn't this a normal
REST API?"), see `reference/philosophy.md` — not required reading to generate correct code.

## Step 3 — Verify against the checklist in `reference/conventions.md`

Before calling the work done, re-check the generated code against the "cross-cutting rules" checklist (all
request properties nullable, trim/normalize before validating, `.AsList()`, cancellation tokens only on reads,
soft delete only, `ConcurrencyKey` byte-array comparison via `Toolbox.ByteArrayEqual`, error code naming
`error.{entity}.{camelCaseReason}`, status codes 200/204/400). These are the details most likely to drift if
generated from generic CRUD instinct instead of this project's actual pattern.
