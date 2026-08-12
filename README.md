# EnquirySort

AI-powered inbox triage built on your stack:

- **.NET 10 / C#** API with **FastEndpoints + Dapper + SQL Server** (`scaffold-fastendpoints-crud`)
- **Svelte** admin UI (`ai-agent-platform-frontend` patterns)
- **OpenRouter** classification + reply drafting
- **MailKit** IMAP/SMTP — replies and list forwards are real SMTP sends (unless `Mail:DryRun` is true)

```text
Inbox (IMAP) → OpenRouter classifier → respond | route | ignore
                     │                      │
                     ▼                      ▼
              Knowledge base reply    Mailing list forward (SMTP)
                     │
                     ▼
              SQL audit + Svelte admin
```

## Solution layout

| Path | Purpose |
|------|---------|
| `src/EnquirySort.Api` | FastEndpoints API + inbox worker |
| `src/EnquirySort.Web` | Svelte admin (mailing lists, KB, enquiries) |
| `database/001_InitialSchema.sql` | SQL Server tables, indexes, seed data |
| `scaffold-fastendpoints-crud/` | Backend skill |
| `ai-agent-platform-frontend/` | Frontend skill |

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- SQL Server (LocalDB / full / Docker)

## Database

```bash
# Apply schema (adjust server as needed)
sqlcmd -S localhost -i database/001_InitialSchema.sql
```

Update `src/EnquirySort.Api/appsettings.json` → `ConnectionStrings:EnquirySort`.

## API

```bash
cd src/EnquirySort.Api
dotnet run
# listens on http://localhost:5180
```

### Configuration (`appsettings.json`)

| Section | Keys |
|---------|------|
| `Mail` | `EmailAddress`, `EmailPassword`, IMAP/SMTP hosts, `DryRun` |
| `OpenRouter` | `ApiKey`, `Model` |
| `EnquiryWorker` | `Enabled`, poll interval, confidence thresholds |

**Sending mail:** set `Mail:DryRun` to `false` and provide valid mailbox credentials. The worker/API then sends real SMTP replies and list forwards via MailKit.

Process the inbox once (without waiting for the worker):

```http
POST /enquiries/processInbox
```

Or enable continuous polling with `EnquiryWorker:Enabled=true`.

### RPC-style routes (skill convention)

- `POST /mailingLists/create` · `GET /mailingLists/get/{id}` · `POST /mailingLists/update` · `POST /mailingLists/delete`
- `GET /mailingLists/listForDropdown` · `GET /mailingLists/listForDataTable`
- Same shape for `/knowledgeArticles/*`
- `GET /enquiries/listForDataTable` · `GET /enquiries/get/{id}` · `POST /enquiries/processInbox`

## Web UI

```bash
cd src/EnquirySort.Web
cp .env.example .env
npm install
npm run dev
```

Hash routes for mailing lists, knowledge articles, and processed enquiries (create/update/delete with concurrency keys).

## Skills applied

Greenfield choices documented for the FastEndpoints skill Step 0:

- Root entities (no Organization parent yet)
- Template-style `MyErrorResponse` (`fatalError`, `concurrencyKeyInvalid`, `additionalData`)
- `AllowAnonymous()` until auth is wired
- Soft delete, COMB GUIDs (`RT.Comb.EnsureOrderedProvider.Sql`), `sp_getapplock` uniqueness, computed `ConcurrencyKey`
