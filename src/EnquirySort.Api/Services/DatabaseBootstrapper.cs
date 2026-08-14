using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using EnquirySort.Api.Configuration;
using EnquirySort.Api.Enums;
using Microsoft.Data.SqlClient;
using RT.Comb;

namespace EnquirySort.Api.Services;

public sealed class DatabaseBootstrapper
{
    private readonly AppSettings _settings;
    private readonly ICombProvider _combProvider;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseBootstrapper> _logger;

    public DatabaseBootstrapper(
        AppSettings settings,
        ICombProvider combProvider,
        IHostEnvironment environment,
        ILogger<DatabaseBootstrapper> logger)
    {
        _settings = settings;
        _combProvider = combProvider;
        _environment = environment;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string connectionString = _settings.ConnectionStrings.EnquirySort;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:EnquirySort is required for database bootstrap.");
        }

        await EnsureDatabaseExistsAsync(connectionString, cancellationToken);
        await EnsureSchemaAsync(connectionString, cancellationToken);
        await EnsureMigrationsAsync(connectionString, cancellationToken);

        if (!_settings.Seed.Enabled)
        {
            _logger.LogInformation("Database seed disabled (Seed:Enabled=false)");
            return;
        }

        await SeedAsync(connectionString, cancellationToken);
    }

    private async Task EnsureDatabaseExistsAsync(string connectionString, CancellationToken cancellationToken)
    {
        SqlConnectionStringBuilder builder = new(connectionString);
        string databaseName = string.IsNullOrWhiteSpace(builder.InitialCatalog)
            ? "EnquirySort"
            : builder.InitialCatalog;
        builder.InitialCatalog = "master";

        await using SqlConnection connection = new(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            IF DB_ID(@databaseName) IS NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'CREATE DATABASE [' + REPLACE(@databaseName, ']', ']]') + N']';
                EXEC(@sql);
            END
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { databaseName }, cancellationToken: cancellationToken));
        _logger.LogInformation("Ensured database exists: {Database}", databaseName);
    }

    private async Task EnsureSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        bool tablesExist = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                select case when object_id(N'dbo.tblMailingLists', N'U') is null then 0 else 1 end
                """,
                cancellationToken: cancellationToken)) == 1;

        if (tablesExist)
        {
            _logger.LogInformation("Schema already present; skipping initial DDL");
            return;
        }

        string schemaPath = ResolveSqlPath("001_InitialSchema.sql");
        await ExecuteSqlScriptAsync(connection, schemaPath, skipCreateDbAndSeed: true, cancellationToken);
        _logger.LogInformation("Applied EnquirySort schema from {Path}", schemaPath);
    }

    private async Task EnsureMigrationsAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        bool hasReplyStatus = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                select case when col_length(N'dbo.tblEnquiries', N'ReplyStatus') is null then 0 else 1 end
                """,
                cancellationToken: cancellationToken)) == 1;

        if (hasReplyStatus)
        {
            _logger.LogInformation("Migration 002_ReplyStatus already applied");
            return;
        }

        string migrationPath = ResolveSqlPath("002_ReplyStatus.sql");
        await ExecuteSqlScriptAsync(connection, migrationPath, skipCreateDbAndSeed: false, cancellationToken);
        _logger.LogInformation("Applied migration {Path}", migrationPath);
    }

    private async Task ExecuteSqlScriptAsync(
        SqlConnection connection,
        string scriptPath,
        bool skipCreateDbAndSeed,
        CancellationToken cancellationToken)
    {
        string script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        foreach (string batch in SplitSqlBatches(script))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            if (skipCreateDbAndSeed
                && (Regex.IsMatch(batch, @"^\s*(create\s+database|use)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline)
                    || Regex.IsMatch(batch, @"^\s*insert\s+into\s+tbl", RegexOptions.IgnoreCase | RegexOptions.Multiline)))
            {
                continue;
            }

            await connection.ExecuteAsync(new CommandDefinition(batch, cancellationToken: cancellationToken));
        }
    }

    private async Task SeedAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        int mailingListCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "select count(*) from tblMailingLists where Deleted = 0",
                cancellationToken: cancellationToken));

        if (mailingListCount == 0)
        {
            await SeedMailingListsAsync(connection, cancellationToken);
            _logger.LogInformation("Seeded mailing lists");
        }
        else
        {
            _logger.LogInformation("Mailing lists already present ({Count}); skipping list seed", mailingListCount);
        }

        int articleCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "select count(*) from tblKnowledgeArticles where Deleted = 0",
                cancellationToken: cancellationToken));

        if (articleCount == 0)
        {
            await SeedKnowledgeArticlesAsync(connection, cancellationToken);
            _logger.LogInformation("Seeded knowledge articles");
        }
        else
        {
            _logger.LogInformation("Knowledge articles already present ({Count}); skipping article seed", articleCount);
        }

        if (!_settings.Seed.SampleEnquiries)
        {
            return;
        }

        int enquiryCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "select count(*) from tblEnquiries where Deleted = 0",
                cancellationToken: cancellationToken));

        if (enquiryCount == 0)
        {
            await SeedSampleEnquiriesAsync(connection, cancellationToken);
            _logger.LogInformation("Seeded sample enquiries");
        }
        else
        {
            _logger.LogInformation("Enquiries already present ({Count}); skipping enquiry seed", enquiryCount);
        }
    }

    private async Task SeedMailingListsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        (Guid Id, string Name, string Address, string Description)[] rows =
        [
            (Guid.Parse("11111111-1111-1111-1111-111111111111"), "sales", "sales@example.com",
                "New business, demos, pricing negotiations."),
            (Guid.Parse("22222222-2222-2222-2222-222222222222"), "support", "support@example.com",
                "Technical issues needing a human."),
            (Guid.Parse("33333333-3333-3333-3333-333333333333"), "billing", "billing@example.com",
                "Invoices, refunds, payment failures.")
        ];

        const string sql = """
            insert into tblMailingLists (id, Name, Address, Description)
            values (@id, @name, @address, @description);
            """;

        foreach ((Guid id, string name, string address, string description) in rows)
        {
            DynamicParameters parameters = new();
            parameters.Add("@id", id, DbType.Guid);
            parameters.Add("@name", name, DbType.String, size: 100);
            parameters.Add("@address", address, DbType.String, size: 320);
            parameters.Add("@description", description, DbType.String, size: 500);
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
    }

    private async Task SeedKnowledgeArticlesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        (Guid Id, string Title, string Slug, string Content)[] rows =
        [
            (Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Resetting Your Password", "password-reset",
                "Open https://app.oraclecms.com/forgot-password, enter your account email, and follow the reset link (expires in 60 minutes)."),
            (Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Pricing Overview", "pricing",
                "Starter $49/mo, Growth $149/mo, Enterprise custom. Annual billing saves 15%. Contact sales for enterprise quotes."),
            (Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Connecting a Custom Domain", "custom-domains",
                "Growth and Enterprise plans support custom domains. Add the domain in Settings → Domains, create the DNS CNAME to sites.oraclecms.com, then Verify."),
            (Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "Getting Started", "getting-started",
                "Sign in at https://app.oraclecms.com, click New Site, choose a template, and publish when ready.")
        ];

        const string sql = """
            insert into tblKnowledgeArticles (id, Title, Slug, Content)
            values (@id, @title, @slug, @content);
            """;

        foreach ((Guid id, string title, string slug, string content) in rows)
        {
            DynamicParameters parameters = new();
            parameters.Add("@id", id, DbType.Guid);
            parameters.Add("@title", title, DbType.String, size: 200);
            parameters.Add("@slug", slug, DbType.String, size: 200);
            parameters.Add("@content", content, DbType.String);
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
    }

    private async Task SeedSampleEnquiriesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        Guid respondId = _combProvider.Create();
        Guid routeId = _combProvider.Create();
        Guid salesListId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // lang=sql
        const string sql = """
            declare @_now datetime2(3) = sysutcdatetime();

            insert into tblEnquiries
                (id, MessageId, FromAddress, Subject, BodyText, Action, Confidence, Reason, CustomerQuestion,
                 RoutedToMailingListId, ReplyBody, ReplySent, ReplyStatus, ProcessedUtc, InsertDateUtc, UpdatedDateUtc)
            values
                (@respondId, N'<demo-password@example.com>', N'customer@example.org', N'Need password reset help',
                 N'How do I reset my password?', @respondAction, 0.91, N'FAQ password reset',
                 N'How do I reset my password?', null,
                 N'Please visit https://app.oraclecms.com/forgot-password and follow the reset link.',
                 0, @draftStatus, @_now, @_now, @_now),
                (@routeId, N'<demo-sales@example.com>', N'buyer@acmecorp.com', N'Enterprise pricing for 40 sites + SSO',
                 N'We need SSO and a formal quote for procurement.', @routeAction, 0.94, N'Enterprise quote request',
                 null, @salesListId, null, 0, @noneStatus, @_now, @_now, @_now);
            """;

        DynamicParameters parameters = new();
        parameters.Add("@respondId", respondId, DbType.Guid);
        parameters.Add("@routeId", routeId, DbType.Guid);
        parameters.Add("@salesListId", salesListId, DbType.Guid);
        parameters.Add("@respondAction", (byte)EnquiryAction.Respond, DbType.Byte);
        parameters.Add("@routeAction", (byte)EnquiryAction.Route, DbType.Byte);
        parameters.Add("@draftStatus", (byte)ReplyStatus.Draft, DbType.Byte);
        parameters.Add("@noneStatus", (byte)ReplyStatus.None, DbType.Byte);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private string ResolveSqlPath(string fileName)
    {
        string[] candidates =
        [
            Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..", "database", fileName)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "database", fileName)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "database", fileName))
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Could not find database/{fileName}. Run from the repo root or copy the file next to the API.");
    }

    private static IEnumerable<string> SplitSqlBatches(string script)
    {
        return Regex.Split(script, @"^\s*GO\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .Select(batch => batch.Trim())
            .Where(batch => batch.Length > 0);
    }
}
