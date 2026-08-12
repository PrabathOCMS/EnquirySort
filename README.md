# EnquirySort

AI-powered inbox triage:

- **.NET 10 / C#** API — FastEndpoints + Dapper + SQL Server
- **Svelte** admin UI
- **OpenRouter** classification + reply drafting
- **MailKit** IMAP/SMTP (real sends when `Mail:DryRun` is `false`)

On startup the API **creates the database/schema if needed** and **seeds demo data** when tables are empty (`Seed:Enabled=true`).

```text
Inbox (IMAP) → OpenRouter → respond | route | ignore
                  │              │
                  ▼              ▼
           Knowledge reply   Mailing list forward
```

## Quick setup

### 1. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 20+
- Docker (recommended for SQL Server) **or** any SQL Server instance

### 2. Start SQL Server

```bash
docker compose up -d
# waits until port 1433 is ready
```

**Docker SQL** (matches `docker-compose.yml`) — default in `appsettings.json`:

```
Server=localhost,1433;Database=EnquirySort;User Id=sa;Password=EnquirySort_Demo1!;TrustServerCertificate=True;Encrypt=False;
```

**SQL Server already installed on Windows** — use Windows auth in `appsettings.Development.json` (this overrides the Docker `sa` string):

```json
"ConnectionStrings": {
  "EnquirySort": "Server=localhost;Database=EnquirySort;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Other local variants:

```text
Server=.\\SQLEXPRESS;Database=EnquirySort;Trusted_Connection=True;TrustServerCertificate=True;
Server=(localdb)\\MSSQLLocalDB;Database=EnquirySort;Trusted_Connection=True;TrustServerCertificate=True;
```

`Login failed for user 'sa'` almost always means the Docker `sa` password is still configured — switch to `Trusted_Connection=True` (or put your real SA password in the string).

### 3. Run the API

```bash
cd src/EnquirySort.Api
dotnet run
# http://localhost:5288
```

On first boot you’ll see logs like:

- `Ensured database exists: EnquirySort`
- `Applied EnquirySort schema…` (or `Schema already present`)
- `Seeded mailing lists` / `Seeded knowledge articles` / `Seeded sample enquiries`

Seed is **idempotent**: restarting won’t duplicate rows.

### 4. Run the admin UI

```bash
cd src/EnquirySort.Web
cp .env.example .env   # VITE_API_URL=http://localhost:5288
npm install
npm run dev
# http://localhost:5173
```

### 5. Configure mail + OpenRouter (optional for live inbox)

Edit `src/EnquirySort.Api/appsettings.json` (or use user-secrets / env vars):

| Setting | Purpose |
|---------|---------|
| `Mail:EmailAddress` / `Mail:EmailPassword` | IMAP+SMTP mailbox (Gmail: [App Password](https://myaccount.google.com/apppasswords)) |
| `Mail:DryRun` | `true` = log only; `false` = **actually send** replies/forwards |
| `OpenRouter:ApiKey` | From [openrouter.ai/keys](https://openrouter.ai/keys) |
| `EnquiryWorker:Enabled` | `true` = poll inbox on a timer |
| `Seed:Enabled` | `true` = auto schema + seed on startup |
| `Seed:SampleEnquiries` | `true` = insert demo processed enquiries when empty |

Environment variable examples:

```bash
export ConnectionStrings__EnquirySort='Server=localhost,1433;Database=EnquirySort;User Id=sa;Password=EnquirySort_Demo1!;TrustServerCertificate=True;Encrypt=False;'
export OpenRouter__ApiKey='sk-or-v1-...'
export Mail__EmailAddress='you@gmail.com'
export Mail__EmailPassword='your-app-password'
export Mail__DryRun='false'
```

Process the inbox once:

```bash
curl -X POST http://localhost:5288/enquiries/processInbox
```

Or click **Process inbox** in the Enquiries page.

## What gets seeded

When tables are empty and `Seed:Enabled=true`:

| Table | Demo rows |
|-------|-----------|
| Mailing lists | `sales`, `support`, `billing` |
| Knowledge articles | password reset, pricing, custom domains, getting started |
| Enquiries | sample **Respond** (password FAQ) + **Route** (enterprise quote → sales) |

Turn seeding off in production:

```json
"Seed": { "Enabled": false }
```

## Manual schema (optional)

If you prefer applying DDL yourself:

```bash
sqlcmd -S localhost,1433 -U sa -P 'EnquirySort_Demo1!' -C -i database/001_InitialSchema.sql
```

Runtime seed still fills empty tables when the API starts.

## Project layout

| Path | Purpose |
|------|---------|
| `src/EnquirySort.Api` | API + inbox worker + runtime bootstrap/seed |
| `src/EnquirySort.Web` | Svelte admin |
| `database/001_InitialSchema.sql` | SQL Server DDL |
| `docker-compose.yml` | Local SQL Server |

## Skills

Built to match:

- `scaffold-fastendpoints-crud` (vertical-slice FastEndpoints / Dapper / soft delete / concurrency keys)
- `ai-agent-platform-frontend` (Svelte admin patterns)
