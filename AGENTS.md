# AGENTS.md

## Cursor Cloud specific instructions

EnquirySort is a single-product monorepo: a .NET 10 API + a Svelte/Vite admin UI, backed by SQL Server (via Docker). See `README.md` for the canonical setup/run/config docs; the notes below only cover non-obvious cloud gotchas. The update script already installs/refreshes dependencies on VM startup.

### Services

| Service | Path | Dev command | URL |
|---|---|---|---|
| SQL Server 2022 | `docker-compose.yml` | `sudo docker compose up -d` (from repo root) | `localhost:1433` (`sa` / `EnquirySort_Demo1!`) |
| API (.NET 10 / FastEndpoints) | `src/EnquirySort.Api` | `dotnet run` | `http://localhost:5288` |
| Web (Svelte 5 / Vite) | `src/EnquirySort.Web` | `npm run dev` | `http://localhost:5173` |

The API auto-creates the DB, applies `database/001_InitialSchema.sql`, and idempotently seeds demo data (mailing lists, knowledge articles, sample enquiries) on startup — no manual migrations needed.

### Toolchain / environment notes

- `.NET 10 SDK` is installed under `~/.dotnet` (not via apt). It is added to `PATH`/`DOTNET_ROOT` in `~/.bashrc`. New non-login shells may need `export PATH="$HOME/.dotnet:$PATH"` if `dotnet` is not found.
- Docker daemon is not managed by systemd here. Start it once per VM boot with `sudo dockerd &` (or in a tmux session) before `docker compose up`. The daemon is configured for `fuse-overlayfs` with the containerd snapshotter disabled (required for Docker 29 in this nested environment).

### Critical gotcha: API connection string on Linux

`src/EnquirySort.Api/appsettings.Development.json` overrides the connection string to **Windows integrated auth** (`Trusted_Connection=True`), which **fails on Linux**. Since `dotnet run` uses the `Development` environment by default, you MUST override the connection string to point at the Docker `sa` account. Do NOT edit the committed config — set an env var instead:

```bash
export ConnectionStrings__EnquirySort="Server=localhost,1433;Database=EnquirySort;User Id=sa;Password=EnquirySort_Demo1!;TrustServerCertificate=True;Encrypt=False;"
cd src/EnquirySort.Api && dotnet run
```

Note: the `!` in the SA password triggers bash history expansion in interactive shells — run `set +H` first (or wrap the value in single quotes) when exporting it interactively.

### Lint / test / build

- API: `dotnet build` (nullable + analyzers enabled; treat warnings as the lint signal). There is **no test project** in the repo.
- Web: `npm run check` (svelte-check + `tsc`) is the only static check; `npm run build` for a production bundle. There is **no unit test runner** configured.

### AI / mail features (optional)

The "Process inbox" flow (`POST /enquiries/processInbox`) needs `OpenRouter:ApiKey` and Gmail IMAP/SMTP creds (`Mail:EmailAddress`/`Mail:EmailPassword`). Keep `Mail:DryRun=true` to avoid real sends. All CRUD + UI browsing works without these; the inbox worker is disabled by default (`EnquiryWorker:Enabled=false`).
