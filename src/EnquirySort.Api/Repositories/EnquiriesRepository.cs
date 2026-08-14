using System.Data;
using Dapper;
using EnquirySort.Api.Configuration;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Features.Enquiries.SendEnquiryReply;
using EnquirySort.Api.Features.Enquiries.UpdateEnquiryDraft;
using EnquirySort.Api.Models;
using EnquirySort.Api.Utilities;
using Microsoft.Data.SqlClient;
using RT.Comb;

namespace EnquirySort.Api.Repositories;

public sealed class EnquiriesRepository
{
    private readonly AppSettings _appSettings;
    private readonly ICombProvider _combProvider;

    public EnquiriesRepository(AppSettings appSettings, ICombProvider combProvider)
    {
        _appSettings = appSettings;
        _combProvider = combProvider;
    }

    public async Task<Enquiry> CreateEnquiryAsync(Enquiry enquiry)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);
        Guid id = _combProvider.Create();

        // lang=sql
        string sql = @"
declare @_now datetime2(3) = sysutcdatetime();
declare @_data table (
    id uniqueidentifier,
    MessageId nvarchar(500),
    FromAddress nvarchar(320),
    Subject nvarchar(500),
    BodyText nvarchar(max),
    Action tinyint,
    Confidence float,
    Reason nvarchar(1000),
    CustomerQuestion nvarchar(1000),
    RoutedToMailingListId uniqueidentifier,
    ReplyBody nvarchar(max),
    ReplySent bit,
    ReplyStatus tinyint,
    ProcessedUtc datetime2(3),
    InsertDateUtc datetime2(3),
    UpdatedDateUtc datetime2(3),
    Deleted bit,
    ConcurrencyKey varbinary(4));

insert into tblEnquiries
    (id, MessageId, FromAddress, Subject, BodyText, Action, Confidence, Reason, CustomerQuestion,
     RoutedToMailingListId, ReplyBody, ReplySent, ReplyStatus, ProcessedUtc, InsertDateUtc, UpdatedDateUtc)
output inserted.id, inserted.MessageId, inserted.FromAddress, inserted.Subject, inserted.BodyText,
       inserted.Action, inserted.Confidence, inserted.Reason, inserted.CustomerQuestion,
       inserted.RoutedToMailingListId, inserted.ReplyBody, inserted.ReplySent, inserted.ReplyStatus,
       inserted.ProcessedUtc, inserted.InsertDateUtc, inserted.UpdatedDateUtc, inserted.Deleted, inserted.ConcurrencyKey
into @_data
values
    (@id, @messageId, @fromAddress, @subject, @bodyText, @action, @confidence, @reason, @customerQuestion,
     @routedToMailingListId, @replyBody, @replySent, @replyStatus, @processedUtc, @_now, @_now);

insert into tblEnquiries_Log
    (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
     EnquiryId, FromAddress, Subject, Action, ReplySent, ReplyStatus, Deleted, LogAction)
select @logId, null, N'EnquirySort Worker', null, @reason,
       d.id, d.FromAddress, d.Subject, d.Action, d.ReplySent, d.ReplyStatus, 0, 'Insert'
from @_data d;

select * from @_data;";

        DynamicParameters parameters = new();
        parameters.Add("@id", id, DbType.Guid);
        parameters.Add("@logId", _combProvider.Create(), DbType.Guid);
        parameters.Add("@messageId", enquiry.MessageId, DbType.String, size: 500);
        parameters.Add("@fromAddress", enquiry.FromAddress, DbType.String, size: 320);
        parameters.Add("@subject", enquiry.Subject, DbType.String, size: 500);
        parameters.Add("@bodyText", enquiry.BodyText, DbType.String);
        parameters.Add("@action", (byte)enquiry.Action, DbType.Byte);
        parameters.Add("@confidence", enquiry.Confidence, DbType.Double);
        parameters.Add("@reason", enquiry.Reason, DbType.String, size: 1000);
        parameters.Add("@customerQuestion", enquiry.CustomerQuestion, DbType.String, size: 1000);
        parameters.Add("@routedToMailingListId", enquiry.RoutedToMailingListId, DbType.Guid);
        parameters.Add("@replyBody", enquiry.ReplyBody, DbType.String);
        parameters.Add("@replySent", enquiry.ReplySent, DbType.Boolean);
        parameters.Add("@replyStatus", (byte)enquiry.ReplyStatus, DbType.Byte);
        parameters.Add("@processedUtc", enquiry.ProcessedUtc, DbType.DateTime2);

        Enquiry created = await sqlConnection.QuerySingleAsync<Enquiry>(sql, parameters);
        created.RoutedToMailingListName = enquiry.RoutedToMailingListName;
        return created;
    }

    public async Task<Enquiry?> GetEnquiryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);

        // lang=sql
        string sql = @"
select e.id, e.MessageId, e.FromAddress, e.Subject, e.BodyText, e.Action, e.Confidence, e.Reason,
       e.CustomerQuestion, e.RoutedToMailingListId, ml.Name as RoutedToMailingListName,
       e.ReplyBody, e.ReplySent, e.ReplyStatus, e.ProcessedUtc, e.InsertDateUtc, e.UpdatedDateUtc,
       e.Deleted, e.ConcurrencyKey
from tblEnquiries e
left join tblMailingLists ml on ml.id = e.RoutedToMailingListId and ml.Deleted = 0
where e.Deleted = 0 and e.id = @id";

        DynamicParameters parameters = new();
        parameters.Add("@id", id, DbType.Guid);
        CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
        return await sqlConnection.QueryFirstOrDefaultAsync<Enquiry>(cmd);
    }

    public async Task<(SqlQueryResult, Enquiry?)> UpdateEnquiryDraftAsync(
        UpdateEnquiryDraftRequest req,
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
    MessageId nvarchar(500),
    FromAddress nvarchar(320),
    Subject nvarchar(500),
    BodyText nvarchar(max),
    Action tinyint,
    Confidence float,
    Reason nvarchar(1000),
    CustomerQuestion nvarchar(1000),
    RoutedToMailingListId uniqueidentifier,
    ReplyBody nvarchar(max),
    ReplySent bit,
    ReplyStatus tinyint,
    ProcessedUtc datetime2(3),
    InsertDateUtc datetime2(3),
    UpdatedDateUtc datetime2(3),
    Deleted bit,
    ConcurrencyKey varbinary(4),
    OldReplyBody nvarchar(max),
    OldReplyStatus tinyint);

update tblEnquiries
set ReplyBody = @replyBody,
    ReplyStatus = @draftStatus,
    ReplySent = 0,
    UpdatedDateUtc = @_now
output inserted.id, inserted.MessageId, inserted.FromAddress, inserted.Subject, inserted.BodyText,
       inserted.Action, inserted.Confidence, inserted.Reason, inserted.CustomerQuestion,
       inserted.RoutedToMailingListId, inserted.ReplyBody, inserted.ReplySent, inserted.ReplyStatus,
       inserted.ProcessedUtc, inserted.InsertDateUtc, inserted.UpdatedDateUtc, inserted.Deleted,
       inserted.ConcurrencyKey, deleted.ReplyBody, deleted.ReplyStatus
into @_data
where id = @id
  and Deleted = 0
  and ReplySent = 0
  and ReplyStatus = @draftStatus
  and ConcurrencyKey = @concurrencyKey;

if @@ROWCOUNT = 1
begin
    set @_result = 1;
    insert into tblEnquiries_Log
        (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
         EnquiryId, FromAddress, Subject, Action, ReplySent, ReplyStatus, Deleted,
         OldReplySent, OldReplyStatus, LogAction)
    select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, N'Draft reply updated',
           d.id, d.FromAddress, d.Subject, d.Action, d.ReplySent, d.ReplyStatus, 0,
           0, d.OldReplyStatus, 'Update'
    from @_data d;
end

select @_result;
select id, MessageId, FromAddress, Subject, BodyText, Action, Confidence, Reason, CustomerQuestion,
       RoutedToMailingListId, ReplyBody, ReplySent, ReplyStatus, ProcessedUtc, InsertDateUtc,
       UpdatedDateUtc, Deleted, ConcurrencyKey
from @_data
union all
select id, MessageId, FromAddress, Subject, BodyText, Action, Confidence, Reason, CustomerQuestion,
       RoutedToMailingListId, ReplyBody, ReplySent, ReplyStatus, ProcessedUtc, InsertDateUtc,
       UpdatedDateUtc, Deleted, ConcurrencyKey
from tblEnquiries
where @_result = 0 and id = @id;";

        DynamicParameters parameters = new();
        parameters.Add("@id", req.id, DbType.Guid);
        parameters.Add("@logId", _combProvider.Create(), DbType.Guid);
        parameters.Add("@replyBody", req.ReplyBody, DbType.String);
        parameters.Add("@draftStatus", (byte)ReplyStatus.Draft, DbType.Byte);
        parameters.Add("@concurrencyKey", req.ConcurrencyKey, DbType.Binary, size: 4);
        parameters.Add("@adminUserUid", adminUserUid, DbType.Guid);
        parameters.Add("@adminUserDisplayName", adminUserDisplayName, DbType.String, size: 200);
        parameters.Add("@remoteIpAddress", remoteIpAddress, DbType.AnsiString, size: 45);

        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(sql, parameters);
        int resultCode = await gridReader.ReadFirstAsync<int>();
        Enquiry? entity = !gridReader.IsConsumed
            ? await gridReader.ReadFirstOrDefaultAsync<Enquiry>()
            : null;

        if (resultCode == 1)
        {
            return (SqlQueryResult.Ok, entity);
        }

        if (entity is null || entity.Deleted)
        {
            return (SqlQueryResult.RecordDidNotExist, null);
        }

        if (entity.ReplySent || entity.ReplyStatus == ReplyStatus.Sent || entity.ReplyStatus != ReplyStatus.Draft)
        {
            return (SqlQueryResult.RecordAlreadyExists, entity);
        }

        if (!Toolbox.ByteArrayEqual(entity.ConcurrencyKey, req.ConcurrencyKey))
        {
            return (SqlQueryResult.ConcurrencyKeyInvalid, entity);
        }

        return (SqlQueryResult.UnknownError, entity);
    }

    public async Task<(SqlQueryResult, Enquiry?)> MarkEnquiryReplySentAsync(
        SendEnquiryReplyRequest req,
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
    MessageId nvarchar(500),
    FromAddress nvarchar(320),
    Subject nvarchar(500),
    BodyText nvarchar(max),
    Action tinyint,
    Confidence float,
    Reason nvarchar(1000),
    CustomerQuestion nvarchar(1000),
    RoutedToMailingListId uniqueidentifier,
    ReplyBody nvarchar(max),
    ReplySent bit,
    ReplyStatus tinyint,
    ProcessedUtc datetime2(3),
    InsertDateUtc datetime2(3),
    UpdatedDateUtc datetime2(3),
    Deleted bit,
    ConcurrencyKey varbinary(4));

update tblEnquiries
set ReplyBody = coalesce(@replyBody, ReplyBody),
    ReplySent = 1,
    ReplyStatus = @sentStatus,
    UpdatedDateUtc = @_now
output inserted.id, inserted.MessageId, inserted.FromAddress, inserted.Subject, inserted.BodyText,
       inserted.Action, inserted.Confidence, inserted.Reason, inserted.CustomerQuestion,
       inserted.RoutedToMailingListId, inserted.ReplyBody, inserted.ReplySent, inserted.ReplyStatus,
       inserted.ProcessedUtc, inserted.InsertDateUtc, inserted.UpdatedDateUtc, inserted.Deleted,
       inserted.ConcurrencyKey
into @_data
where id = @id
  and Deleted = 0
  and ReplySent = 0
  and ConcurrencyKey = @concurrencyKey
  and nullif(ltrim(rtrim(coalesce(@replyBody, ReplyBody))), '') is not null;

if @@ROWCOUNT = 1
begin
    set @_result = 1;
    insert into tblEnquiries_Log
        (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
         EnquiryId, FromAddress, Subject, Action, ReplySent, ReplyStatus, Deleted,
         OldReplySent, OldReplyStatus, LogAction)
    select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, N'Reply approved and sent',
           d.id, d.FromAddress, d.Subject, d.Action, d.ReplySent, d.ReplyStatus, 0,
           0, 1, 'Update'
    from @_data d;
end

select @_result;
select id, MessageId, FromAddress, Subject, BodyText, Action, Confidence, Reason, CustomerQuestion,
       RoutedToMailingListId, ReplyBody, ReplySent, ReplyStatus, ProcessedUtc, InsertDateUtc,
       UpdatedDateUtc, Deleted, ConcurrencyKey
from @_data
union all
select id, MessageId, FromAddress, Subject, BodyText, Action, Confidence, Reason, CustomerQuestion,
       RoutedToMailingListId, ReplyBody, ReplySent, ReplyStatus, ProcessedUtc, InsertDateUtc,
       UpdatedDateUtc, Deleted, ConcurrencyKey
from tblEnquiries
where @_result = 0 and id = @id;";

        DynamicParameters parameters = new();
        parameters.Add("@id", req.id, DbType.Guid);
        parameters.Add("@logId", _combProvider.Create(), DbType.Guid);
        parameters.Add("@replyBody", string.IsNullOrWhiteSpace(req.ReplyBody) ? null : req.ReplyBody, DbType.String);
        parameters.Add("@sentStatus", (byte)ReplyStatus.Sent, DbType.Byte);
        parameters.Add("@concurrencyKey", req.ConcurrencyKey, DbType.Binary, size: 4);
        parameters.Add("@adminUserUid", adminUserUid, DbType.Guid);
        parameters.Add("@adminUserDisplayName", adminUserDisplayName, DbType.String, size: 200);
        parameters.Add("@remoteIpAddress", remoteIpAddress, DbType.AnsiString, size: 45);

        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(sql, parameters);
        int resultCode = await gridReader.ReadFirstAsync<int>();
        Enquiry? entity = !gridReader.IsConsumed
            ? await gridReader.ReadFirstOrDefaultAsync<Enquiry>()
            : null;

        if (resultCode == 1)
        {
            return (SqlQueryResult.Ok, entity);
        }

        if (entity is null || entity.Deleted)
        {
            return (SqlQueryResult.RecordDidNotExist, null);
        }

        if (entity.ReplySent || entity.ReplyStatus == ReplyStatus.Sent)
        {
            return (SqlQueryResult.RecordAlreadyExists, entity);
        }

        if (!Toolbox.ByteArrayEqual(entity.ConcurrencyKey, req.ConcurrencyKey))
        {
            return (SqlQueryResult.ConcurrencyKeyInvalid, entity);
        }

        return (SqlQueryResult.UnknownError, entity);
    }

    public async Task<DataTableResponse<Enquiry>> ListEnquiriesForDataTableAsync(
        int pageNumber,
        int pageSize,
        SortType sort,
        long? requestCounter,
        string? search,
        EnquiryListFilter filter,
        CancellationToken cancellationToken = default)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);
        string orderBy = sort switch
        {
            SortType.Created => "e.InsertDateUtc desc",
            SortType.Name => "e.Subject asc",
            SortType.Email => "e.FromAddress asc",
            _ => "e.ProcessedUtc desc"
        };

        string filterSql = filter switch
        {
            EnquiryListFilter.Open => "and e.ReplyStatus = @draftStatus",
            EnquiryListFilter.Responded => "and e.ReplyStatus = @sentStatus",
            EnquiryListFilter.Ignored => "and e.Action = @ignoreAction",
            EnquiryListFilter.Routed => "and e.Action = @routeAction",
            _ => string.Empty
        };

        // lang=sql
        string sql = $@"
declare @_search nvarchar(200) = @search;

select count(*)
from tblEnquiries e
where e.Deleted = 0
  {filterSql}
  and (@_search is null
       or e.Subject like '%' + @_search + '%'
       or e.FromAddress like '%' + @_search + '%'
       or e.Reason like '%' + @_search + '%');

select e.id, e.MessageId, e.FromAddress, e.Subject, e.BodyText, e.Action, e.Confidence, e.Reason,
       e.CustomerQuestion, e.RoutedToMailingListId, ml.Name as RoutedToMailingListName,
       e.ReplyBody, e.ReplySent, e.ReplyStatus, e.ProcessedUtc, e.InsertDateUtc, e.UpdatedDateUtc,
       e.Deleted, e.ConcurrencyKey
from tblEnquiries e
left join tblMailingLists ml on ml.id = e.RoutedToMailingListId and ml.Deleted = 0
where e.Deleted = 0
  {filterSql}
  and (@_search is null
       or e.Subject like '%' + @_search + '%'
       or e.FromAddress like '%' + @_search + '%'
       or e.Reason like '%' + @_search + '%')
order by {orderBy}
offset @offset rows fetch next @pageSize rows only;";

        DynamicParameters parameters = new();
        parameters.Add("@search", string.IsNullOrWhiteSpace(search) ? null : search.Trim(), DbType.String, size: 200);
        parameters.Add("@offset", (pageNumber - 1) * pageSize, DbType.Int32);
        parameters.Add("@pageSize", pageSize, DbType.Int32);
        parameters.Add("@draftStatus", (byte)ReplyStatus.Draft, DbType.Byte);
        parameters.Add("@sentStatus", (byte)ReplyStatus.Sent, DbType.Byte);
        parameters.Add("@ignoreAction", (byte)EnquiryAction.Ignore, DbType.Byte);
        parameters.Add("@routeAction", (byte)EnquiryAction.Route, DbType.Byte);
        CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(cmd);
        int totalCount = await gridReader.ReadFirstAsync<int>();
        List<Enquiry> records = !gridReader.IsConsumed
            ? (await gridReader.ReadAsync<Enquiry>()).AsList()
            : [];

        return new DataTableResponse<Enquiry>
        {
            RequestCounter = requestCounter,
            Records = records,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
