# Frontend source-of-truth patterns

## Canonical pages

Read these files before implementing related work.

### Without concurrency

- `AiAgentSipTrunkManagementWeb/src/pages/sip-providers/create.svelte`
- `AiAgentSipTrunkManagementWeb/src/pages/sip-providers/index.svelte`
- `AiAgentSipTrunkManagementWeb/src/pages/sip-providers/[id]/index.svelte`
- `AiAgentSipTrunkManagementWeb/src/pages/sip-providers/[id]/update.svelte`

The SIP Provider pages are the approved example for create, list, detail, update, and delete workflows without a concurrency key. Concurrency does not determine whether a page needs an editable table.

### With concurrency

- `AiAgentSipTrunkManagementWeb/src/pages/master/inbound-sip-trunks/create.svelte`
- `AiAgentSipTrunkManagementWeb/src/pages/master/inbound-sip-trunks/index.svelte`
- `AiAgentSipTrunkManagementWeb/src/pages/master/inbound-sip-trunks/[id]/view/index.svelte`
- `AiAgentSipTrunkManagementWeb/src/pages/master/inbound-sip-trunks/[id]/edit/index.svelte`

The Inbound SIP Trunk pages are the approved example for page-local concurrency handling. Study both update and delete because each presents changed server data and manages the replacement concurrency key locally.

### Same-page one-to-many relationship editor

- `AiAgentSipTrunkManagementWeb/src/pages/sip-providers/_components/AllowedCidrTable.svelte`
- `AiAgentSipTrunkManagementWeb/src/pages/sip-providers/create.svelte`
- `AiAgentSipTrunkManagementWeb/src/pages/sip-providers/[id]/update.svelte`

Use the Provider Allowed CIDR UI as the approved example when one parent record owns multiple child records that users add, edit, or remove within the same create or update page. Here, `tblSIPProviders` has a one-to-many relationship with `tblSIPProviderAllowedCIDRs`. This relationship is why the page uses a table; the absence of a concurrency key is unrelated.

For other same-page editable one-to-many relationships, follow the same table behavior using existing table, input, and button components. Do not add a table for ordinary scalar fields, one-to-one data, or merely because the page has no concurrency key. Do not modify, reuse as a generic abstraction, or create a component without explicit user approval.

## Page construction

- Set `$appData.pageTitle` and `$appData.breadcrumbs`.
- For record-dependent detail and update metadata, prefer a separate page-local `buildBreadCrumbsAndPageTitle` function called after the record loads; keep this construction out of `loadData`.
- Use `ContentCard` and `PageHeading` for the main page structure.
- Use existing `TextInput`, `CustomSelectInput`, `Toggle`, `Button`, `ButtonWithTooltip`, and `Alert` components for forms.
- Use the existing `Table` family, `SearchInput`, `Pagination`, `PaginationPageNumberText`, `Preloader`, `PageLoadError`, and empty-state components for lists.
- Use `DescriptionList`, `DescriptionListSection`, `DescriptionListItem`, `Badge`, and `DateTimeWithTimezoneIndicator` for detail and concurrency comparison displays.
- Use existing modal and notification components.
- Put delete actions on detail/view pages when deletion is supported. Reuse the established list-page delete confirmation, warning, and concurrency workflow.
- Use `ButtonWithTooltip` for every create/update save action, call the page's existing `handleSubmit` from `on:click`, and display `Ctrl + S` in the `tooltip` slot.
- Use `<form on:submit|preventDefault>` without attaching `handleSubmit` to the form. Only the save button and Ctrl+S handler initiate submission.
- Use explicit curly-brace blocks for all JavaScript control flow; do not write one-line `if`, `else`, loop, or similar statements without braces.
- Add a page-local `handleKeyDown` and `<svelte:window on:keydown={handleKeyDown} />`. Match the SIP Provider and Inbound SIP Trunk handler: ignore repeat, Alt, and Meta; require Ctrl plus `event.code === "KeyS"`; prevent the browser default; and call the same `handleSubmit` function.

Write direct HTML only when no existing project component covers the requirement. If satisfying the requirement appears to require changing a component, stop and ask the user.

## Form pattern

- Keep a page-local `form` object with API-facing field names.
- Every `CustomSelectInput` must bind its exposed component functions. Use `on:itemselected` to assign `event.detail` to its actual form/page value; `bind:value` alone is not reliable.
- After fetching or otherwise replacing a `CustomSelectInput` item list, call `functions.refreshItems(newItems, keepValue)`. Updating only the page's items array can leave the component's visible options stale until the user opens, closes, and reopens the dropdown.
- Use `functions.setValue(newValue)` whenever code, rather than the user, chooses the selection. After refreshing items, call `setValue(oldValue)` when the old value still exists in the new list and should remain visibly selected.
- Keep a page-local `validations` object containing `touched`, `valid`, `errorMessage`, and component function bindings where required.
- Implement local `validate(setTouched)` and `clearErrors()` functions.
- Mark all fields touched on submit.
- Apply backend field errors to matching validations.
- Keep `formDisabled` and form-level error state explicit.
- For required startup data, use page-level `loading`, `done`, `forbidden`, and `error` states plus a page-local abort controller. Render `Error403` for 401/403 and retryable `PageLoadError` for other fatal startup failures; do not render a usable form without its required data.
- Use a `finally` block to restore disabled/loading state.
- Follow the reference wording and capitalization for visible labels and notifications.

## Same-page one-to-many form pattern

Use an editable table when the form manages a parent and a collection of child records together:

- Display each child as a table row.
- Provide add, edit, and remove actions within the page.
- Keep the child collection and its validation in page-local state.
- Submit the parent and child collection in the API shape defined by the backend.
- Reuse the project's existing table, input, and button components.
- Treat this choice independently from concurrency handling. A page may have a one-to-many table with or without a concurrency key.
- Ask before creating or changing any component.

## List pattern

- Keep page number, page size, total record count, search, sort, request counter, and abort controller explicit.
- Synchronize supported list state into the route query string when the approved page does.
- Abort obsolete requests during new loads and route changes.
- Use server-side paging and the existing pagination components.
- Configure sortable headers with one of the approved `SortType` values:
  - `unsorted`
  - `updated`
  - `created`
  - `name`
  - `email`
  - `lastAccessTimestamp`
  - `organization`
  - `sipTrunkName`
  - `phoneNumber`
- Send sort order as `asc` or `desc`.
- Follow the established fixed direction per column: name/text columns use `asc`, date columns use `desc`, and the `switch`-case handler sets both values. Do not make headers bidirectional unless explicitly requested.
- Keep `sortValue` and `sortOrder` explicit, include both in the API request and synchronized URL, restore supported sort state from route parameters, reset to page 1 when sorting, and wire every sortable header to the switch-case handler.
- If another sort is required, report it and wait for the backend enum to be extended.

## Request pattern

Prefer the approved direct pattern:

1. Construct the path from `$appData.apiUrl`.
2. Call `fetch`.
3. Add `Authorization: Bearer ${$appData.authToken}`.
4. Add `Content-Type: application/json` for JSON request bodies.
5. Call `parseResponse`.
6. Choose the error strategy before continuing:
   - Use `throwOnFetchError` only when all failed HTTP statuses should produce the same generic error.
   - When behavior depends on the status code, do not use `throwOnFetchError` for that response path. Inspect the status and payload explicitly. For example, on a form response with status 400, map each request-object key in the error payload to the matching field validation.
   - Handle concurrency responses explicitly before any generic-error path.
7. Show an existing notification and navigate only on success.

Do not replace deliberate local request code with a shared wrapper. Sparse existing `apiRequest` calls may remain.

When inspecting backend response contracts, do not treat necessary column aliases or temporary/declared table aliases as violations. Avoid unnecessary aliases for real database tables. A mapping such as `PhoneNumberE164 as PhoneNumber` is acceptable when the response model intentionally exposes `PhoneNumber`.

## Concurrency details

The backend exposes the current record through error `additionalData` when `concurrencyKeyInvalid` is true.

For update:

- Load the record and store its key.
- Submit the form together with the record ID and stored key.
- Parse `additionalData` when the key is invalid.
- Present current server values and highlight changes.
- Update the stored key from the current server record.
- Retry only when the user chooses the modal action.

For delete:

- Copy the selected row and its key into delete-modal state.
- Submit the ID and modal key.
- Parse and display `additionalData` on a concurrency conflict.
- Replace the modal key with the current server key.
- Keep the modal open so the user can review and explicitly retry.

## Review boundaries

Copy cleanup may correct headings, subtext, notification text, modal labels, grammar, capitalization, and mojibake. It must not modify:

- imports or unused code;
- endpoint paths;
- request or response mapping;
- component props;
- validation behavior;
- route targets;
- concurrency behavior;
- shared or feature components.

Report such non-copy findings separately instead of fixing them without permission.

For every actionable API or backend finding, provide the exact file and tight line number where the correction belongs whenever determinable. State what is currently there and what must change.
