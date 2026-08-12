# Why the conventions are this way

Not required reading to generate correct code — the other reference files state the rules directly. Load this
one when you (or a reviewer) need the reasoning behind a rule, e.g. to judge an edge case the other files don't
explicitly cover, or to explain "why isn't this a normal REST API?"

## Action-based, RPC-style routing (not textbook REST)

```
POST /organizations/create
GET  /organizations/get/{id}
POST /organizations/update
GET  /organizations/listForDropdown
GET  /organizations/listForDataTable
POST /organizations/delete
```

Only two HTTP verbs are used at all: **GET = read-only, no side effects; POST = anything that writes**
(create/update/delete are all POST). PUT/PATCH/DELETE verbs are deliberately not used.

Reasoning, as actually argued:
- Writes must never be GET (GET requests can be cached by browsers and carry other GET-specific quirks) —
  beyond that single constraint, there's no need to reason about which of five HTTP verbs "best fits" a given
  action.
- The URI alone tells the full story without inspecting the HTTP verb — it reads like a function call
  (`DeleteOrganizationAsync(id)`), which is exactly what it maps to.
- Non-CRUD "actions" (e.g. `/users/activate/{id}`) fall out naturally, where REST/PATCH semantics get awkward
  trying to express "this isn't really an update, it's a distinct action."
- Endpoints map 1:1 to specific front-end views — `/listForDataTable` returns exactly what one page needs,
  avoiding generic-endpoint query-string gymnastics and the payload/transform code that comes with a
  one-size-fits-all `GET /organizations?fields=...&sort=...` endpoint.
- Smaller blast radius for authorization bugs — each endpoint does one precise thing rather than one endpoint
  branching over many cases behind a single route.
- At scale (the source codebase this tutorial is drawn from has 600+ endpoints), searching for a specific
  route string finds the exact feature instantly, rather than getting a wall of generic `POST /organizations`
  hits across dozens of unrelated actions.

## Folder-per-feature, not layer-per-folder

```
/Features
  /{EntityPlural}
    /Create{Entity}      -- Endpoint, Request, Context classes
    /Get{Entity}
    /Update{Entity}
    /List{EntityPlural}ForDropdown
    /List{EntityPlural}ForDataTable
    /Delete{Entity}

/Models
  {Entity}.cs            -- shared POCO reused across all of that entity's endpoints

/Repositories
  {EntityPlural}Repository.cs   -- one class per entity, all its DB-query functions
```

- Folder grouping under `/Features` is by *type of data*, which "98% of the time" means one database table —
  the exception is cross-table things like a Dashboard, which gets its own folder named for what it is, not
  for a table.
- `/Models` holds one shared response POCO per entity, reused by Create/Get/Update/Delete — don't create a
  separate response class per endpoint unless that endpoint's response genuinely differs from the shared
  shape (a Dashboard again being the typical exception).
- `/Repositories` groups by entity, not by feature folder, because some repository methods are reused across
  features (most commonly an `IsXExistsAsync` helper called from a *different* entity's Create/Update endpoint
  to validate a foreign reference, since this schema uses no FK constraints).

Payoff of this structure: adding a new endpoint to an existing project means adding one new folder under
`/Features` plus one new method appended to the relevant `Repository` class — nothing else needs touching,
which minimizes the risk of an unrelated regression. This is the reasoning behind `scaffold-fastendpoints-crud`
generating exactly this shape for every new entity.

## "The back end is the last line of defense"

Core thesis: the back end must independently enforce both **authorization** (never let a user see or do
something with data they shouldn't have access to) and **validation/sanitization** (never let invalid or
unsanitized data reach the database or disk) — regardless of what the front end does or doesn't check.

What "strict validation" means in practice:
- Reject requests missing required fields, even if the front end would never normally send one blank.
- Enforce every DB/application constraint server-side too — if a column is `nvarchar(100)`, reject longer
  input in C# before it ever reaches SQL; if a value must be 1–10, reject out-of-range values explicitly.
- Sanitize any input that could carry malicious content (e.g. embedded script in HTML/SVG fields).
- For file uploads: verify the actual binary content/type, never trust the client-reported filename or
  extension — assume someone will rename a `.exe` to `.jpg` and try to upload it, because someone will.
- Re-save uploaded images at a sane resolution/filesize server-side (phone cameras produce oversized images;
  left unchecked this becomes a storage/bandwidth cost problem that grows over time).
- Treat PDFs as a risk too (they can carry embedded scripts/content) — re-save/sanitize rather than storing
  the uploaded file verbatim.
- Never persist an uploaded file to disk under the client-supplied filename — always generate a random
  filename for the on-disk path; the original filename can still be stored in the database purely for
  display purposes, but must never touch the actual file path.

**Why front-end validation has zero security value** (the actual argument): a web browser is just one possible
client for the API. Anyone can bypass it entirely — Postman, Bruno, curl, or a hand-written client — and send
arbitrary requests directly to the back end. If validation logic lives mostly or only in the front end, anyone
who skips the front end skips the validation completely and can submit anything at all. Front-end validation's
real value is purely UX: catching mistakes early, before a round trip, so it's faster and more pleasant for a
well-behaved user — but "it holds no value outside of this" as a security control. This is why every request
DTO in this codebase has every property nullable and re-validates everything server-side (see
`reference/conventions.md`) — the server can never assume the client already checked anything.
