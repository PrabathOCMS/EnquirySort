---
name: ai-agent-platform-frontend
description: Implement, migrate, review, or repair Svelte frontend pages in AiAgentSipTrunkManagementWeb using the project's approved page structure. Use for forms, detail pages, data tables, editable one-to-many relationships, routing, API integration, validation, concurrency-key handling, and copy cleanup in this frontend. Treat the completed SIP Provider pages as the non-concurrency reference, the completed Inbound SIP Trunk pages as the concurrency reference, and the Provider Allowed CIDR table as the same-page one-to-many editor reference.
---

# AI Agent Platform Frontend

Follow the established frontend patterns in `AiAgentSipTrunkManagementWeb`. Preserve deliberate page-local repetition and maximize reuse of existing components.

## Workflow

1. Read the exact neighboring page and the relevant canonical reference before proposing or editing code.
2. Inspect the matching `AiAgentPlatformAPI` controller, request, response, model, and repository definitions. Report missing or incompatible contracts with the exact file and tight line number to change whenever the location can be determined; do not implement backend changes.
3. Write and maintain a checklist for multi-page migrations.
4. Confirm the route scope:
   - Cross-organization Dispatch Rules, Inbound SIP Trunks, and Outbound SIP Trunks belong under `/master`.
   - SIP Providers and SIP Provider Numbers belong in the top-level pages area, using the established routes such as `/sip-providers` and `/sip-provider-numbers`; do not add a literal `/page` URL prefix.
   - Follow the currently approved product decision for Organization Assigned Numbers; do not infer a location from older code.
5. Reuse existing layout, form, table, modal, notification, loading, error, and description-list components.
   - Include a delete action on detail/view pages when the API supports deletion, using the same confirmation and concurrency behavior as the list delete flow.
6. Keep request, validation, loading, and concurrency state explicit in the page when the canonical page does so.
7. Verify the frontend production build and inspect the final diff for unintended logic or component changes.

## Non-negotiable constraints

- Do not modify any shared or feature component unless the user explicitly authorizes that component change. Ask first if a component appears to need modification.
- Do not introduce a new component, helper, abstraction, or internal DTO merely to remove repetition.
- Interpret the no-alias preference narrowly: avoid unnecessary aliases for real database tables. Temporary or declared table aliases and column aliases are acceptable when necessary, including mappings such as `PhoneNumberE164 as PhoneNumber`.
- Prefer the direct `fetch` pattern used by the canonical pages. `apiRequest` is permitted only when the local approved page already uses it or the user asks for it; never refactor working direct requests to use it.
- Keep concurrency-key handling in the page function. Do not hide it in a generic request helper.
- Expose and transmit concurrency keys as ordinary consequent-update detection values. Do not treat them as cryptographic secrets.
- Do not make backend, endpoint, database, or contract changes. Flag missing endpoints or fields for the user.
- For a copy-only task, change only user-visible strings. Do not clean imports, props, control flow, routing, data mapping, or other logic.
- Preserve existing code style and intentional repetition even when a more abstract implementation seems possible.
- Always use explicit curly-brace blocks for `if`, `else`, `for`, `while`, and similar control flow. Do not use single-line control-flow statements without braces.

## Choose patterns independently

Select the concurrency pattern and relationship-editing pattern separately:

- No concurrency key: read the completed SIP Provider pages.
- With concurrency key: read the completed Inbound SIP Trunk pages, especially update and delete.
- Editable one-to-many child collection on the same parent page: use a table following the Provider Allowed CIDR example. A SIP Provider has many Allowed CIDRs, represented by `tblSIPProviders` to `tblSIPProviderAllowedCIDRs`.
- Scalar fields or relationships that are not edited as a same-page child collection: do not add a table merely because the page has no concurrency key.
- Use existing table, input, and button components for the one-to-many editor. Ask before creating, modifying, or generalizing a component.
- For exact file paths and implementation details, read [references/frontend-patterns.md](references/frontend-patterns.md).

## API and state rules

- Build API paths from `$appData.apiUrl`.
- Define frontend representations of backend enums in `AiAgentSipTrunkManagementWeb/src/helpers/constants.js` as uppercase symbolic keys mapped to the backend numeric values, for example `{ EXTERNAL: 1, INTERNAL: 2 }`. Reuse those constants in form defaults, validation, comparisons, dropdown items, and display mapping. Do not reverse the object into numeric keys mapped to labels or repeat enum values as magic numbers in pages.
- For every `CustomSelectInput`, bind its component functions and handle all three synchronization paths:
  - Handle `on:itemselected` and explicitly assign `event.detail` to the page or form value; do not rely on `bind:value` alone.
  - After asynchronously loading or replacing the dropdown items, call the component's `refreshItems(newItems, keepValue)` function. Assigning the page's items array alone does not reliably refresh the visible dropdown; without `refreshItems`, the new options may not appear until the dropdown is opened, closed, and opened again.
  - When selecting a value programmatically, call the component's `setValue(newValue)` function so the visible selection updates too. For example, after refreshing a list, use `setValue(oldValue)` when the old value remains present in the new list and should stay selected.
- On every create/update form, use `ButtonWithTooltip` for the save action, show `Ctrl + S` in its `tooltip` slot, and invoke the page's existing `handleSubmit` from both the button and a page-local `handleKeyDown` registered with `<svelte:window on:keydown={handleKeyDown} />`.
- The form itself must use `on:submit|preventDefault` without calling `handleSubmit`. Submission is initiated only by the `ButtonWithTooltip` click or the approved Ctrl+S handler.
- Implement the Ctrl+S handler exactly like the approved SIP Provider and Inbound SIP Trunk pages: ignore repeated keys, reject Alt/Meta or missing Ctrl, require `event.code === "KeyS"`, call `event.preventDefault()`, then call the same `handleSubmit`. Do not add or modify a component for this behavior.
- Send the bearer token from `$appData.authToken`.
- Always call `parseResponse`, then choose one error path: use `throwOnFetchError` only when every failed HTTP status should become the same generic error; when behavior depends on the status code, handle that response explicitly and do not use `throwOnFetchError` for that branch. For example, inspect a form response with status 400 and map each request-object key to its matching validation error.
- Map backend field errors directly into the page validation object without inventing frontend mapping layers or DTOs.
- Keep loading states, abort controllers, progress-store calls, request counters, form-disabled state, and page error state local when the reference page does.
- Treat data required before a page or form can continue as fatal startup data. Use `pageLoading` states (`loading`, `done`, `forbidden`, `error`), a page-local abort controller, `Error403` for 401/403 responses, and `PageLoadError` with the startup loader as `reloadFunc` for other failures. Do not downgrade failure of required startup data to an inline form or table error.
- Use only these camel-case sort values unless the user confirms a backend enum addition: `unsorted`, `updated`, `created`, `name`, `email`, `lastAccessTimestamp`, `organization`, `sipTrunkName`, `phoneNumber`.
- Use only `asc` or `desc` for sort order.
- Use fixed single-direction sorting by default: names and other textual identifiers sort only `asc`; created and updated dates sort only `desc`. Implement the established `switch`-case handler. Do not add bidirectional sorting unless the user explicitly requests it.
- A sortable index page is incomplete unless it declares `sortValue` and `sortOrder`, includes both in its API request and synchronized URL, restores the supported sort from route parameters, uses the switch-case handler to set the fixed direction, resets to page 1 when sorting, and connects every sortable header to that handler.
- On record detail and update pages, prefer a page-local `buildBreadCrumbsAndPageTitle` function for record-dependent metadata. Invoke it after the record loads instead of mixing breadcrumb and page-title construction into `loadData`.

## Concurrency workflow

For update and delete operations that support concurrency:

1. Retain the record's `concurrencyKey` in page state.
2. Include it in the update or delete request body.
3. If the error response has `concurrencyKeyInvalid`, read the serialized current record from `additionalData`.
4. Parse `additionalData`, show the changed values with the existing description-list, badge, alert, and modal components, and replace the stored key with the returned current key.
5. Retry only through the explicit user action implemented by the page.

Do not assume `additionalData` is a DTO alias or a cryptographic validation payload.

## Completion checklist

- [ ] Correct route scope and navigation
- [ ] Matching backend endpoint and request/response shape inspected
- [ ] Backend enum values are represented once in `src/helpers/constants.js` with symbolic keys mapped to numeric values
- [ ] Missing endpoints or fields reported without backend edits
- [ ] Existing components reused
- [ ] No unapproved component changes
- [ ] Every `CustomSelectInput` uses `on:itemselected`, `refreshItems` after programmatic item loads, and `setValue` for programmatic selections
- [ ] Every create/update save action uses `ButtonWithTooltip` and the approved Ctrl+S handler
- [ ] Canonical direct-fetch and validation structure followed
- [ ] Correct concurrency pattern selected
- [ ] Relationship cardinality checked; table used only for a same-page editable one-to-many collection
- [ ] Allowed sort value and `asc`/`desc` order used
- [ ] Copy matches create, update, view, and delete context
- [ ] Diff contains no unrelated cleanup or logic changes
- [ ] Production build passes
