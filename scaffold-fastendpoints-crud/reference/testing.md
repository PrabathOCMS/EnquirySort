# Bruno test stubs

Tests live in the project's existing Bruno collection (one collection per API project, checked into git) —
add one `.bru` request per endpoint, in the same folder style as existing entities' requests. Don't create a
new collection unless one doesn't already exist.

For each endpoint, stub out this request sequence (mirrors what every existing endpoint's tests do):

## Create

1. Empty `{}` body → expect a 400 with a required-field error per mandatory property.
2. Explicit `null` / empty-string values for each field → same required-field errors.
3. A value exceeding the field's max length → a length error for that field.
4. A valid, fully-populated request → 200 + the created entity in the response body.
5. Resend the identical valid request → a "record already exists" error on the unique field.
6. (Manual/documented step, not a `.bru` assertion) Cross-check the new row in `tbl{Entity}` and the inserted
   row in `tbl{Entity}_Log` directly in SSMS.

## Get

1. A known-valid id → 200 + the full row, matching every column in the table.
2. An id with one character mutated (or a random GUID) → the "did not exist" error.

## Update

1. Empty `{}` body → required-field errors, including `concurrencyKey`.
2. Oversized `Name` / oversized `ConcurrencyKey` → length errors.
3. A valid update with the current concurrency key → 200 + the entity with its new `ConcurrencyKey`.
4. Resend the exact same request (now-stale concurrency key) → `ConcurrencyKeyInvalid` error.
5. Create a second record, then try renaming the first to match the second's unique value →
   `RecordAlreadyExists` error.

## Delete

1. Empty `{}` body → required-field errors.
2. Wrong/oversized `ConcurrencyKey` → the relevant error.
3. A valid delete → `204 No Content`.
4. (Manual, SSMS) Confirm `Deleted = 1` in `tbl{Entity}` and a `Deleted=1`/`OldDeleted=0` row was written to
   `tbl{Entity}_Log`.

## ListForDropdown

1. No query params → 200 + all active records, ordered by the display column.
2. With an `X-Request-Counter` header → the same value echoed back in the response.
3. With a `search` param matching an existing record → the filtered subset only.
4. With a nonsense `search` param → an empty `Records` list.

## ListForDataTable

1. No query params → page 1 of the default page size, ordered by the default sort.
2. With `X-Request-Counter` → echoed back.
3. With `search` → the filtered subset, `TotalCount` reflecting the filtered total (not the whole table).
4. With `pageSize=1&pageNumber=2` → exactly the second record by the current sort order.
5. A `pageNumber` beyond the last page → back to page 1's results, not an error.
