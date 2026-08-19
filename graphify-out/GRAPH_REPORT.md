# Graph Report - workspace  (2026-08-19)

## Corpus Check
- 137 files · ~40,524 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 757 nodes · 1328 edges · 47 communities (37 shown, 10 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 38 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `0c52d5ba`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- SqlQueryResult
- MailingList
- .ProcessMessageAsync
- constants.ts
- Endpoint
- EnquirySort
- AppSettings
- Enquiry
- ListEnquiriesForDataTableEndpoint
- EnquirySort.Api.Enums
- EnquirySort.Api.Models
- devDependencies
- ListKnowledgeArticlesForDropdownEndpoint
- Frontend source-of-truth patterns
- compilerOptions
- DatabaseBootstrapper
- compilerOptions
- Cross-cutting conventions (apply to every endpoint)
- .BuildAlternative
- EnquirySort.Api.csproj
- http
- Bruno test stubs
- Create{Entity} endpoint
- Database schema conventions
- List{EntityPlural}ForDataTable endpoint
- CustomNoRepeatTimestampProvider
- Delete{Entity} endpoint
- Get{Entity} endpoint
- List{EntityPlural}ForDropdown endpoint
- Update{Entity} endpoint
- Scaffolding a FastEndpoints CRUD entity
- ListEnquiriesForDataTableEndpoint.cs
- ListKnowledgeArticlesForDataTableEndpoint.cs
- SendEnquiryReplyEndpoint.cs
- EnquirySort.Api.Features.MailingLists.DeleteMailingList
- GetEnquiryEndpoint.cs
- UpdateEnquiryDraftEndpoint.cs
- MyErrorResponse
- Why the conventions are this way
- EnquirySort.Web
- UpdateAppSettingsEndpoint.cs
- GetAppSettingsEndpoint.cs
- vite-env.d.ts
- tsconfig.json

## God Nodes (most connected - your core abstractions)
1. `EnquirySort.Api.Models` - 51 edges
2. `EnquirySort.Api.Enums` - 33 edges
3. `EnquirySort.Api.Repositories` - 25 edges
4. `Enquiry` - 20 edges
5. `SqlQueryResult` - 19 edges
6. `KnowledgeArticlesRepository` - 19 edges
7. `KnowledgeArticle` - 18 edges
8. `MailingList` - 18 edges
9. `AppSettings` - 17 edges
10. `OpenRouterClient` - 17 edges

## Surprising Connections (you probably didn't know these)
- `AppSettings` --references--> `SeedSettings`  [EXTRACTED]
  src/EnquirySort.Api/Configuration/AppSettings.cs → src/EnquirySort.Api/Configuration/SeedSettings.cs
- `ImapEmailClient` --references--> `AppSettings`  [EXTRACTED]
  src/EnquirySort.Api/Email/ImapEmailClient.cs → src/EnquirySort.Api/Configuration/AppSettings.cs
- `OpenRouterClient` --references--> `AppSettings`  [EXTRACTED]
  src/EnquirySort.Api/Email/OpenRouterClient.cs → src/EnquirySort.Api/Configuration/AppSettings.cs
- `SendEnquiryReplyEndpoint` --references--> `AppSettings`  [EXTRACTED]
  src/EnquirySort.Api/Features/Enquiries/SendEnquiryReply/SendEnquiryReplyEndpoint.cs → src/EnquirySort.Api/Configuration/AppSettings.cs
- `AppSettingsRepository` --references--> `AppSettings`  [EXTRACTED]
  src/EnquirySort.Api/Repositories/AppSettingsRepository.cs → src/EnquirySort.Api/Configuration/AppSettings.cs

## Import Cycles
- None detected.

## Communities (47 total, 10 thin omitted)

### Community 0 - "SqlQueryResult"
Cohesion: 0.06
Nodes (34): EnquirySort.Api.Features.KnowledgeArticles.GetKnowledgeArticle, SqlQueryResult, CancellationToken, GeneratedRegex, Regex, Task, CreateKnowledgeArticleEndpoint, CreateKnowledgeArticleRequest (+26 more)

### Community 1 - "MailingList"
Cohesion: 0.06
Nodes (32): EnquirySort.Api.Features.MailingLists.GetMailingList, CancellationToken, Task, CreateMailingListEndpoint, CreateMailingListRequest, CancellationToken, Task, DeleteMailingListEndpoint (+24 more)

### Community 2 - ".ProcessMessageAsync"
Cohesion: 0.08
Nodes (27): HttpClient, IMailFolder, ImapClient, IReadOnlyList, JsonSerializerOptions, MimeMessage, ClassificationResult, CancellationToken (+19 more)

### Community 3 - "constants.ts"
Cohesion: 0.07
Nodes (18): ENQUIRY_ACTION, ENQUIRY_ACTION_LABELS, ENQUIRY_FILTER, ENQUIRY_FILTER_OPTIONS, EnquiryFilterValue, REPLY_STATUS, REPLY_STATUS_LABELS, RESPONSE_MODE (+10 more)

### Community 4 - "Endpoint"
Cohesion: 0.07
Nodes (27): Endpoint, EndpointWithoutRequest, int, ResponseMode, CancellationToken, RuntimeAppSettings, Task, GetAppSettingsEndpoint (+19 more)

### Community 5 - "EnquirySort"
Cohesion: 0.15
Nodes (12): 1. Prerequisites, 2. Start SQL Server, 3. Run the API, 4. Run the admin UI, 5. Configure mail + OpenRouter (required for Process inbox), EnquirySort, Manual schema (optional), Project layout (+4 more)

### Community 6 - "AppSettings"
Cohesion: 0.07
Nodes (25): BackgroundService, EnquirySort.Api.Configuration, EnquirySort.Api.Services, EnquirySort.Api, EnquirySort.Api.Features.Enquiries.ProcessInbox, EnquirySort.Api.Email, IApplicationBuilder, IConfiguration (+17 more)

### Community 7 - "Enquiry"
Cohesion: 0.08
Nodes (24): ReplyStatus, CancellationToken, Task, GetEnquiryEndpoint, Guid, GetEnquiryRequest, CancellationToken, Task (+16 more)

### Community 8 - "ListEnquiriesForDataTableEndpoint"
Cohesion: 0.10
Nodes (16): EnquiryListFilter, SortType, CancellationToken, Task, ListEnquiriesForDataTableEndpoint, ListEnquiriesForDataTableRequest, CancellationToken, Task (+8 more)

### Community 9 - "EnquirySort.Api.Enums"
Cohesion: 0.14
Nodes (10): EnquirySort.Api.Features.MailingLists.CreateMailingList, EnquirySort.Api.Features.MailingLists.UpdateMailingList, EnquirySort.Api.Features.KnowledgeArticles.CreateKnowledgeArticle, RT.Comb, EnquirySort.Api.Features.KnowledgeArticles.UpdateKnowledgeArticle, EnquirySort.Api.Repositories, EnquirySort.Api.Utilities, EnquirySort.Api.Enums (+2 more)

### Community 10 - "EnquirySort.Api.Models"
Cohesion: 0.11
Nodes (13): EnquirySort.Api.Models, JsonSerializerContext, ProcessInboxContext, CreateKnowledgeArticleContext, DeleteKnowledgeArticleContext, GetKnowledgeArticleContext, ListKnowledgeArticlesForDropdownContext, UpdateKnowledgeArticleContext (+5 more)

### Community 11 - "devDependencies"
Cohesion: 0.08
Nodes (24): devDependencies, svelte, svelte-check, @sveltejs/vite-plugin-svelte, @tsconfig/svelte, @types/node, typescript, vite (+16 more)

### Community 12 - "ListKnowledgeArticlesForDropdownEndpoint"
Cohesion: 0.10
Nodes (14): EnquirySort.Api.Features.MailingLists.ListMailingListsForDropdown, EnquirySort.Api.Features.KnowledgeArticles.ListKnowledgeArticlesForDropdown, CancellationToken, Task, ListKnowledgeArticlesForDropdownEndpoint, ListKnowledgeArticlesForDropdownRequest, CancellationToken, Task (+6 more)

### Community 13 - "Frontend source-of-truth patterns"
Cohesion: 0.10
Nodes (19): Canonical pages, Concurrency details, Form pattern, Frontend source-of-truth patterns, List pattern, Page construction, Request pattern, Review boundaries (+11 more)

### Community 14 - "compilerOptions"
Cohesion: 0.10
Nodes (19): src/**/*.js, src/**/*.svelte, src/**/*.ts, svelte, @tsconfig/svelte/tsconfig.json, vite/client, app, compilerOptions (+11 more)

### Community 15 - "DatabaseBootstrapper"
Cohesion: 0.28
Nodes (8): IEnumerable, IHostEnvironment, CancellationToken, ICombProvider, ILogger, SqlConnection, Task, DatabaseBootstrapper

### Community 16 - "compilerOptions"
Cohesion: 0.10
Nodes (19): ES2023, node, vite.config.ts, compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module (+11 more)

### Community 17 - "Cross-cutting conventions (apply to every endpoint)"
Cohesion: 0.12
Nodes (15): COMB sequential GUID generation, `ConcurrencyKey`, Cross-cutting conventions (apply to every endpoint), Determining *why* a 0-row Update/Delete failed (fixed priority order, no extra round trip), `IsXExistsAsync`, `JsonSerializerContext` per endpoint, Naming and folder layout, Root entity vs. child-of-Organization entity (+7 more)

### Community 18 - ".BuildAlternative"
Cohesion: 0.24
Nodes (8): Bytes, Cid, ContentType, MimeEntity, GeneratedRegex, List, Regex, EmailBodyComposer

### Community 19 - "EnquirySort.Api.csproj"
Cohesion: 0.20
Nodes (8): net10.0, Dapper (2.1.79), FastEndpoints (8.2.0), MailKit (4.17.0), Microsoft.Data.SqlClient (7.0.2), MimeKit (4.17.0), RT.Comb (4.0.3), Microsoft.NET.Sdk.Web

### Community 20 - "http"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, http (+1 more)

### Community 21 - "Bruno test stubs"
Cohesion: 0.25
Nodes (7): Bruno test stubs, Create, Delete, Get, ListForDataTable, ListForDropdown, Update

### Community 22 - "Create{Entity} endpoint"
Cohesion: 0.29
Nodes (6): Context, Create{Entity} endpoint, DI registration, Endpoint, Repository method, Request

### Community 23 - "Database schema conventions"
Cohesion: 0.29
Nodes (6): Database schema conventions, Indexes, Log table, Migration files, Schema-editing gotchas (SSMS), Table shape

### Community 24 - "List{EntityPlural}ForDataTable endpoint"
Cohesion: 0.29
Nodes (6): Endpoint, List{EntityPlural}ForDataTable endpoint, Repository method, Request, Response type, Sorting rule

### Community 25 - "CustomNoRepeatTimestampProvider"
Cohesion: 0.38
Nodes (5): DateTime, ICombProvider, SemaphoreSlim, CustomNoRepeatTimestampProvider, EnsureOrderedProvider

### Community 26 - "Delete{Entity} endpoint"
Cohesion: 0.33
Nodes (5): Cascading deletes, Delete{Entity} endpoint, Endpoint, Repository method, Request

### Community 27 - "Get{Entity} endpoint"
Cohesion: 0.33
Nodes (5): Context, Endpoint, Get{Entity} endpoint, Repository method, Request

### Community 28 - "List{EntityPlural}ForDropdown endpoint"
Cohesion: 0.33
Nodes (5): Endpoint, List{EntityPlural}ForDropdown endpoint, Repository method, Request, Response type

### Community 29 - "Update{Entity} endpoint"
Cohesion: 0.33
Nodes (5): Context, Endpoint, Repository method, Request, Update{Entity} endpoint

### Community 30 - "Scaffolding a FastEndpoints CRUD entity"
Cohesion: 0.33
Nodes (5): Scaffolding a FastEndpoints CRUD entity, Step 0 — Detect the target project's actual style before writing anything, Step 1 — Gather entity requirements, Step 2 — Generate in this order, Step 3 — Verify against the checklist in `reference/conventions.md`

### Community 37 - "MyErrorResponse"
Cohesion: 0.50
Nodes (4): Dictionary, List, ErrorMessageItem, MyErrorResponse

### Community 38 - "Why the conventions are this way"
Cohesion: 0.40
Nodes (4): Action-based, RPC-style routing (not textbook REST), Folder-per-feature, not layer-per-folder, "The back end is the last line of defense", Why the conventions are this way

### Community 39 - "EnquirySort.Web"
Cohesion: 0.40
Nodes (4): EnquirySort.Web, Routes (hash), Scripts, Setup

## Knowledge Gaps
- **167 isolated node(s):** `ClassificationDto`, `net10.0`, `Dapper (2.1.79)`, `FastEndpoints (8.2.0)`, `MailKit (4.17.0)` (+162 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **10 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EnquirySort.Api.Models` connect `EnquirySort.Api.Models` to `SqlQueryResult`, `SendEnquiryReplyEndpoint.cs`, `ListKnowledgeArticlesForDataTableEndpoint.cs`, `GetEnquiryEndpoint.cs`, `UpdateEnquiryDraftEndpoint.cs`, `EnquirySort.Api.Features.MailingLists.DeleteMailingList`, `AppSettings`, `MailingList`, `UpdateAppSettingsEndpoint.cs`, `GetAppSettingsEndpoint.cs`, `EnquirySort.Api.Enums`, `Endpoint`, `ListKnowledgeArticlesForDropdownEndpoint`, `ListEnquiriesForDataTableEndpoint`, `Enquiry`, `MyErrorResponse`, `ListEnquiriesForDataTableEndpoint.cs`?**
  _High betweenness centrality (0.099) - this node is a cross-community bridge._
- **Why does `AppSettings` connect `AppSettings` to `SqlQueryResult`, `MailingList`, `.ProcessMessageAsync`, `Endpoint`, `Enquiry`, `DatabaseBootstrapper`?**
  _High betweenness centrality (0.057) - this node is a cross-community bridge._
- **Why does `KnowledgeArticlesRepository` connect `SqlQueryResult` to `.ProcessMessageAsync`, `AppSettings`, `ListEnquiriesForDataTableEndpoint`, `EnquirySort.Api.Enums`, `ListKnowledgeArticlesForDropdownEndpoint`?**
  _High betweenness centrality (0.043) - this node is a cross-community bridge._
- **What connects `ClassificationDto`, `net10.0`, `Dapper (2.1.79)` to the rest of the system?**
  _167 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SqlQueryResult` be split into smaller, more focused modules?**
  _Cohesion score 0.05961538461538462 - nodes in this community are weakly interconnected._
- **Should `MailingList` be split into smaller, more focused modules?**
  _Cohesion score 0.05711263881544157 - nodes in this community are weakly interconnected._
- **Should `.ProcessMessageAsync` be split into smaller, more focused modules?**
  _Cohesion score 0.08235294117647059 - nodes in this community are weakly interconnected._