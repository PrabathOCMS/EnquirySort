using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using EnquirySort.Api.Configuration;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Features.KnowledgeArticles.CreateKnowledgeArticle;
using EnquirySort.Api.Features.KnowledgeArticles.DeleteKnowledgeArticle;
using EnquirySort.Api.Features.KnowledgeArticles.UpdateKnowledgeArticle;
using EnquirySort.Api.Models;
using EnquirySort.Api.Utilities;
using Microsoft.Data.SqlClient;
using RT.Comb;

namespace EnquirySort.Api.Repositories;

public sealed class KnowledgeArticlesRepository
{
    private readonly AppSettings _appSettings;
    private readonly ICombProvider _combProvider;

    public KnowledgeArticlesRepository(AppSettings appSettings, ICombProvider combProvider)
    {
        _appSettings = appSettings;
        _combProvider = combProvider;
    }

    public async Task<(SqlQueryResult, KnowledgeArticle?)> CreateKnowledgeArticleAsync(
        CreateKnowledgeArticleRequest req,
        Guid? adminUserUid,
        string? adminUserDisplayName,
        string? remoteIpAddress)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);
        Guid id = _combProvider.Create();

        // lang=sql
        string sql = @"
declare @_result int = 0;
declare @_now datetime2(3) = sysutcdatetime();
declare @_lockResult int;
declare @_data table (
    id uniqueidentifier,
    Title nvarchar(200),
    Slug nvarchar(200),
    Content nvarchar(max),
    InsertDateUtc datetime2(3),
    UpdatedDateUtc datetime2(3),
    Deleted bit,
    ConcurrencyKey varbinary(4));

begin transaction;

exec @_lockResult = sp_getapplock
    @Resource = @lockResourceName, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;

if @_lockResult < 0
begin
    set @_result = 2;
    rollback transaction;
end
else
begin
    insert into tblKnowledgeArticles (id, Title, Slug, Content, InsertDateUtc, UpdatedDateUtc)
    output inserted.id, inserted.Title, inserted.Slug, inserted.Content,
           inserted.InsertDateUtc, inserted.UpdatedDateUtc, inserted.Deleted, inserted.ConcurrencyKey
    into @_data
    select @id, @title, @slug, @content, @_now, @_now
    where not exists (
        select * from tblKnowledgeArticles
        where Deleted = 0 and Slug = @slug);

    if @@ROWCOUNT = 1
    begin
        set @_result = 1;
        insert into tblKnowledgeArticles_Log
            (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
             KnowledgeArticleId, Title, Slug, Content, Deleted, LogAction)
        select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, null,
               d.id, d.Title, d.Slug, d.Content, d.Deleted, 'Insert'
        from @_data d;
    end
    else
        set @_result = 2;

    commit transaction;
end

select @_result;
select * from @_data;";

        DynamicParameters parameters = new();
        parameters.Add("@id", id, DbType.Guid);
        parameters.Add("@logId", _combProvider.Create(), DbType.Guid);
        parameters.Add("@title", req.Title, DbType.String, size: 200);
        parameters.Add("@slug", req.Slug, DbType.String, size: 200);
        parameters.Add("@content", req.Content, DbType.String, size: -1);
        parameters.Add("@lockResourceName", $"tblKnowledgeArticles_Slug_{Toolbox.Sha1Upper(req.Slug!)}", DbType.AnsiString, size: 200);
        parameters.Add("@adminUserUid", adminUserUid, DbType.Guid);
        parameters.Add("@adminUserDisplayName", adminUserDisplayName, DbType.String, size: 200);
        parameters.Add("@remoteIpAddress", remoteIpAddress, DbType.AnsiString, size: 45);

        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(sql, parameters);
        int resultCode = await gridReader.ReadFirstAsync<int>();
        KnowledgeArticle? entity = !gridReader.IsConsumed
            ? await gridReader.ReadFirstOrDefaultAsync<KnowledgeArticle>()
            : null;

        SqlQueryResult result = resultCode switch
        {
            1 => SqlQueryResult.Ok,
            2 => SqlQueryResult.RecordAlreadyExists,
            _ => SqlQueryResult.UnknownError
        };

        return (result, entity);
    }

    public async Task<KnowledgeArticle?> GetKnowledgeArticleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);

        // lang=sql
        string sql = @"
select id, Title, Slug, Content, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
from tblKnowledgeArticles
where Deleted = 0 and id = @id";

        DynamicParameters parameters = new();
        parameters.Add("@id", id, DbType.Guid);
        CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
        return await sqlConnection.QueryFirstOrDefaultAsync<KnowledgeArticle>(cmd);
    }

    public async Task<(SqlQueryResult, KnowledgeArticle?)> UpdateKnowledgeArticleAsync(
        UpdateKnowledgeArticleRequest req,
        Guid? adminUserUid,
        string? adminUserDisplayName,
        string? remoteIpAddress)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);

        // lang=sql
        string sql = @"
declare @_result int = 0;
declare @_now datetime2(3) = sysutcdatetime();
declare @_lockResult int;
declare @_data table (
    id uniqueidentifier,
    Title nvarchar(200),
    Slug nvarchar(200),
    Content nvarchar(max),
    InsertDateUtc datetime2(3),
    UpdatedDateUtc datetime2(3),
    Deleted bit,
    ConcurrencyKey varbinary(4),
    OldTitle nvarchar(200),
    OldSlug nvarchar(200),
    OldContent nvarchar(max));

begin transaction;

exec @_lockResult = sp_getapplock
    @Resource = @lockResourceName, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;

if @_lockResult < 0
begin
    set @_result = 3;
    rollback transaction;
end
else
begin
    update tblKnowledgeArticles
    set Title = @title,
        Slug = @slug,
        Content = @content,
        UpdatedDateUtc = @_now
    output inserted.id, inserted.Title, inserted.Slug, inserted.Content,
           inserted.InsertDateUtc, inserted.UpdatedDateUtc, inserted.Deleted, inserted.ConcurrencyKey,
           deleted.Title, deleted.Slug, deleted.Content
    into @_data
    where id = @id
      and Deleted = 0
      and ConcurrencyKey = @concurrencyKey
      and not exists (
          select * from tblKnowledgeArticles x
          where x.Deleted = 0 and x.Slug = @slug and x.id <> @id);

    if @@ROWCOUNT = 1
    begin
        set @_result = 1;
        insert into tblKnowledgeArticles_Log
            (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
             KnowledgeArticleId, Title, Slug, Content, Deleted,
             OldTitle, OldSlug, OldContent, OldDeleted, LogAction)
        select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, null,
               d.id, d.Title, d.Slug, d.Content, d.Deleted,
               d.OldTitle, d.OldSlug, d.OldContent, 0, 'Update'
        from @_data d;
        commit transaction;
    end
    else
    begin
        rollback transaction;

        select top 1
            id, Title, Slug, Content, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
        from tblKnowledgeArticles
        where id = @id and Deleted = 0;

        -- result decided in C#
        set @_result = 0;
    end
end

if @_result = 1
begin
    select @_result;
    select id, Title, Slug, Content, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey from @_data;
end
else if @_result = 3
begin
    select @_result;
    select cast(null as uniqueidentifier) as id where 1 = 0;
end
else
begin
    select @_result;
end";

        DynamicParameters parameters = new();
        parameters.Add("@id", req.id, DbType.Guid);
        parameters.Add("@logId", _combProvider.Create(), DbType.Guid);
        parameters.Add("@title", req.Title, DbType.String, size: 200);
        parameters.Add("@slug", req.Slug, DbType.String, size: 200);
        parameters.Add("@content", req.Content, DbType.String, size: -1);
        parameters.Add("@concurrencyKey", req.ConcurrencyKey, DbType.Binary, size: 4);
        parameters.Add("@lockResourceName", $"tblKnowledgeArticles_Slug_{Toolbox.Sha1Upper(req.Slug!)}", DbType.AnsiString, size: 200);
        parameters.Add("@adminUserUid", adminUserUid, DbType.Guid);
        parameters.Add("@adminUserDisplayName", adminUserDisplayName, DbType.String, size: 200);
        parameters.Add("@remoteIpAddress", remoteIpAddress, DbType.AnsiString, size: 45);

        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(sql, parameters);
        int resultCode = await gridReader.ReadFirstAsync<int>();

        if (resultCode == 1)
        {
            KnowledgeArticle? updated = !gridReader.IsConsumed
                ? await gridReader.ReadFirstOrDefaultAsync<KnowledgeArticle>()
                : null;
            return (SqlQueryResult.Ok, updated);
        }

        if (resultCode == 3)
        {
            return (SqlQueryResult.RecordAlreadyExists, null);
        }

        KnowledgeArticle? current = !gridReader.IsConsumed
            ? await gridReader.ReadFirstOrDefaultAsync<KnowledgeArticle>()
            : null;

        if (current is null)
        {
            return (SqlQueryResult.RecordDidNotExist, null);
        }

        if (!Toolbox.ByteArrayEqual(current.ConcurrencyKey, req.ConcurrencyKey))
        {
            return (SqlQueryResult.ConcurrencyKeyInvalid, current);
        }

        return (SqlQueryResult.RecordAlreadyExists, current);
    }

    public async Task<DropdownResponse> ListKnowledgeArticlesForDropdownAsync(
        string? search,
        long? requestCounter,
        CancellationToken cancellationToken = default)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);

        // lang=sql
        string sql = @"
select id as Value, Title as Text
from tblKnowledgeArticles
where Deleted = 0
  and (@search is null or Title like '%' + @search + '%')
order by Title";

        DynamicParameters parameters = new();
        parameters.Add("@search", string.IsNullOrWhiteSpace(search) ? null : search.Trim(), DbType.String, size: 200);
        CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
        List<SelectListItem> records = (await sqlConnection.QueryAsync<SelectListItem>(cmd)).AsList();
        return new DropdownResponse { RequestCounter = requestCounter, Records = records };
    }

    public async Task<DataTableResponse<KnowledgeArticle>> ListKnowledgeArticlesForDataTableAsync(
        int pageNumber,
        int pageSize,
        SortType sort,
        long? requestCounter,
        string? search,
        CancellationToken cancellationToken = default)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);
        string orderBy = sort switch
        {
            SortType.Created => "id desc",
            SortType.Updated => "UpdatedDateUtc desc",
            _ => "Title asc"
        };

        // lang=sql
        string sql = $@"
declare @_search nvarchar(200) = @search;

select count(*)
from tblKnowledgeArticles
where Deleted = 0
  and (@_search is null or Title like '%' + @_search + '%');

select id, Title, Slug, Content, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
from tblKnowledgeArticles
where Deleted = 0
  and (@_search is null or Title like '%' + @_search + '%')
order by {orderBy}
offset @offset rows fetch next @pageSize rows only;";

        DynamicParameters parameters = new();
        parameters.Add("@search", string.IsNullOrWhiteSpace(search) ? null : search.Trim(), DbType.String, size: 200);
        parameters.Add("@offset", (pageNumber - 1) * pageSize, DbType.Int32);
        parameters.Add("@pageSize", pageSize, DbType.Int32);

        CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(cmd);
        int totalCount = await gridReader.ReadFirstAsync<int>();
        List<KnowledgeArticle> records = !gridReader.IsConsumed
            ? (await gridReader.ReadAsync<KnowledgeArticle>()).AsList()
            : [];

        return new DataTableResponse<KnowledgeArticle>
        {
            RequestCounter = requestCounter,
            Records = records,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<SqlQueryResult> DeleteKnowledgeArticleAsync(
        DeleteKnowledgeArticleRequest req,
        Guid? adminUserUid,
        string? adminUserDisplayName,
        string? remoteIpAddress)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);

        // lang=sql
        string sql = @"
declare @_result int = 0;
declare @_now datetime2(3) = sysutcdatetime();
declare @_data table (
    id uniqueidentifier,
    Title nvarchar(200),
    Slug nvarchar(200),
    Content nvarchar(max),
    Deleted bit,
    OldDeleted bit,
    ConcurrencyKey varbinary(4));

update tblKnowledgeArticles
set Deleted = 1,
    UpdatedDateUtc = @_now
output inserted.id, inserted.Title, inserted.Slug, inserted.Content, inserted.Deleted,
       deleted.Deleted, inserted.ConcurrencyKey
into @_data
where id = @id
  and Deleted = 0
  and ConcurrencyKey = @concurrencyKey;

if @@ROWCOUNT = 1
begin
    set @_result = 1;
    -- TODO: cascade-delete child records here once they exist
    insert into tblKnowledgeArticles_Log
        (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
         KnowledgeArticleId, Title, Slug, Content, Deleted,
         OldTitle, OldSlug, OldContent, OldDeleted, LogAction)
    select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, null,
           d.id, d.Title, d.Slug, d.Content, d.Deleted,
           d.Title, d.Slug, d.Content, d.OldDeleted, 'Delete'
    from @_data d;
end

select @_result;
select id, Title, Slug, Content, InsertDateUtc = sysutcdatetime(), UpdatedDateUtc = sysutcdatetime(),
       Deleted = 0, ConcurrencyKey
from tblKnowledgeArticles
where id = @id;";

        DynamicParameters parameters = new();
        parameters.Add("@id", req.id, DbType.Guid);
        parameters.Add("@logId", _combProvider.Create(), DbType.Guid);
        parameters.Add("@concurrencyKey", req.ConcurrencyKey, DbType.Binary, size: 4);
        parameters.Add("@adminUserUid", adminUserUid, DbType.Guid);
        parameters.Add("@adminUserDisplayName", adminUserDisplayName, DbType.String, size: 200);
        parameters.Add("@remoteIpAddress", remoteIpAddress, DbType.AnsiString, size: 45);

        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(sql, parameters);
        int resultCode = await gridReader.ReadFirstAsync<int>();
        KnowledgeArticle? current = !gridReader.IsConsumed
            ? await gridReader.ReadFirstOrDefaultAsync<KnowledgeArticle>()
            : null;

        if (resultCode == 1)
        {
            return SqlQueryResult.Ok;
        }

        if (current is null || current.Deleted)
        {
            return SqlQueryResult.RecordDidNotExist;
        }

        if (!Toolbox.ByteArrayEqual(current.ConcurrencyKey, req.ConcurrencyKey))
        {
            return SqlQueryResult.ConcurrencyKeyInvalid;
        }

        return SqlQueryResult.UnknownError;
    }

    public async Task<List<KnowledgeArticle>> SearchAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);
        List<string> keywords = ExtractSearchKeywords(query);

        if (keywords.Count == 0)
        {
            return [];
        }

        // Score articles by how many query keywords hit title/slug/content.
        // Full-phrase LIKE fails for questions like "How do I reset my password?"
        // against an article titled "Resetting Your Password".
        StringBuilder scoreBuilder = new();
        StringBuilder whereBuilder = new();
        DynamicParameters parameters = new();
        parameters.Add("@topK", topK, DbType.Int32);

        for (int i = 0; i < keywords.Count; i++)
        {
            string paramName = $"@kw{i}";
            parameters.Add(paramName, keywords[i], DbType.String, size: 100);

            if (i > 0)
            {
                scoreBuilder.Append(" + ");
                whereBuilder.Append(" or ");
            }

            scoreBuilder.Append($@"
                (case
                    when Title like '%' + {paramName} + '%' then 3
                    when Slug like '%' + {paramName} + '%' then 2
                    when Content like '%' + {paramName} + '%' then 1
                    else 0
                 end)");

            whereBuilder.Append($@"
                Title like '%' + {paramName} + '%'
                or Slug like '%' + {paramName} + '%'
                or Content like '%' + {paramName} + '%'");
        }

        // lang=sql
        string sql = $@"
select top (@topK)
    id, Title, Slug, Content, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey,
    ({scoreBuilder}) as SearchScore
from tblKnowledgeArticles
where Deleted = 0
  and ({whereBuilder})
order by SearchScore desc, UpdatedDateUtc desc";

        CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
        return (await sqlConnection.QueryAsync<KnowledgeArticle>(cmd)).AsList();
    }

    internal static List<string> ExtractSearchKeywords(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        HashSet<string> stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "but", "if", "then", "so", "to", "of", "in", "on", "at",
            "for", "from", "with", "about", "into", "over", "after", "is", "are", "was", "were",
            "be", "been", "being", "am", "do", "does", "did", "can", "could", "should", "would",
            "will", "just", "please", "help", "need", "needed", "want", "how", "what", "when",
            "where", "why", "who", "whom", "which", "my", "our", "your", "their", "his", "her",
            "its", "me", "we", "you", "they", "i", "it", "this", "that", "these", "those",
            "hi", "hello", "hey", "thanks", "thank", "regards"
        };

        return Regex.Matches(query, @"[A-Za-z0-9][A-Za-z0-9\-]{1,}")
            .Select(m => m.Value.ToLowerInvariant())
            .Where(token => token.Length >= 3 && !stopWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }
}
