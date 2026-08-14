using System.Data;
using Dapper;
using EnquirySort.Api.Configuration;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Features.AdminSettings.UpdateAppSettings;
using EnquirySort.Api.Models;
using EnquirySort.Api.Utilities;
using Microsoft.Data.SqlClient;
using RT.Comb;

namespace EnquirySort.Api.Repositories;

public sealed class AppSettingsRepository
{
    public static readonly Guid SingletonId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly AppSettings _appSettings;
    private readonly ICombProvider _combProvider;

    public AppSettingsRepository(AppSettings appSettings, ICombProvider combProvider)
    {
        _appSettings = appSettings;
        _combProvider = combProvider;
    }

    public async Task<AppSetting> GetAppSettingsAsync(CancellationToken cancellationToken = default)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);

        // lang=sql
        const string sql = @"
select id, ResponseMode, EmailSignatureHtml, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
from tblAppSettings
where Deleted = 0
order by InsertDateUtc
offset 0 rows fetch next 1 rows only;";

        CommandDefinition cmd = new(sql, cancellationToken: cancellationToken);
        AppSetting? existing = await sqlConnection.QueryFirstOrDefaultAsync<AppSetting>(cmd);
        if (existing is not null)
        {
            return existing;
        }

        return await EnsureSingletonAsync(sqlConnection, cancellationToken);
    }

    public async Task<(SqlQueryResult, AppSetting?)> UpdateAppSettingsAsync(
        UpdateAppSettingsRequest req,
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
    ResponseMode tinyint,
    EmailSignatureHtml nvarchar(max),
    InsertDateUtc datetime2(3),
    UpdatedDateUtc datetime2(3),
    Deleted bit,
    ConcurrencyKey varbinary(4),
    OldResponseMode tinyint,
    OldEmailSignatureHtml nvarchar(max));

update tblAppSettings
set ResponseMode = @responseMode,
    EmailSignatureHtml = @emailSignatureHtml,
    UpdatedDateUtc = @_now
output inserted.id, inserted.ResponseMode, inserted.EmailSignatureHtml,
       inserted.InsertDateUtc, inserted.UpdatedDateUtc, inserted.Deleted, inserted.ConcurrencyKey,
       deleted.ResponseMode, deleted.EmailSignatureHtml
into @_data
where id = @id
  and Deleted = 0
  and ConcurrencyKey = @concurrencyKey;

if @@ROWCOUNT = 1
begin
    set @_result = 1;
    insert into tblAppSettings_Log
        (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
         AppSettingsId, ResponseMode, EmailSignatureHtml, Deleted,
         OldResponseMode, OldEmailSignatureHtml, LogAction)
    select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, N'Updated app settings',
           d.id, d.ResponseMode, d.EmailSignatureHtml, 0,
           d.OldResponseMode, d.OldEmailSignatureHtml, 'Update'
    from @_data d;
end

select @_result;
select id, ResponseMode, EmailSignatureHtml, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
from @_data
union all
select id, ResponseMode, EmailSignatureHtml, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
from tblAppSettings
where @_result = 0 and id = @id;";

        DynamicParameters parameters = new();
        parameters.Add("@id", req.id ?? SingletonId, DbType.Guid);
        parameters.Add("@logId", _combProvider.Create(), DbType.Guid);
        parameters.Add("@responseMode", (byte)(req.ResponseMode ?? ResponseMode.Draft), DbType.Byte);
        parameters.Add("@emailSignatureHtml", req.EmailSignatureHtml, DbType.String);
        parameters.Add("@concurrencyKey", req.ConcurrencyKey, DbType.Binary, size: 4);
        parameters.Add("@adminUserUid", adminUserUid, DbType.Guid);
        parameters.Add("@adminUserDisplayName", adminUserDisplayName, DbType.String, size: 200);
        parameters.Add("@remoteIpAddress", remoteIpAddress, DbType.AnsiString, size: 45);

        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(sql, parameters);
        int resultCode = await gridReader.ReadFirstAsync<int>();
        AppSetting? entity = !gridReader.IsConsumed
            ? await gridReader.ReadFirstOrDefaultAsync<AppSetting>()
            : null;

        if (resultCode == 1)
        {
            return (SqlQueryResult.Ok, entity);
        }

        if (entity is null || entity.Deleted)
        {
            return (SqlQueryResult.RecordDidNotExist, null);
        }

        if (!Toolbox.ByteArrayEqual(entity.ConcurrencyKey, req.ConcurrencyKey))
        {
            return (SqlQueryResult.ConcurrencyKeyInvalid, entity);
        }

        return (SqlQueryResult.UnknownError, entity);
    }

    private async Task<AppSetting> EnsureSingletonAsync(SqlConnection sqlConnection, CancellationToken cancellationToken)
    {
        // lang=sql
        const string sql = @"
declare @_now datetime2(3) = sysutcdatetime();
declare @_data table (
    id uniqueidentifier,
    ResponseMode tinyint,
    EmailSignatureHtml nvarchar(max),
    InsertDateUtc datetime2(3),
    UpdatedDateUtc datetime2(3),
    Deleted bit,
    ConcurrencyKey varbinary(4));

if not exists (select 1 from tblAppSettings where Deleted = 0)
begin
    insert into tblAppSettings (id, ResponseMode, EmailSignatureHtml, InsertDateUtc, UpdatedDateUtc)
    output inserted.id, inserted.ResponseMode, inserted.EmailSignatureHtml,
           inserted.InsertDateUtc, inserted.UpdatedDateUtc, inserted.Deleted, inserted.ConcurrencyKey
    into @_data
    values (@id, @responseMode, @emailSignatureHtml, @_now, @_now);

    insert into tblAppSettings_Log
        (id, UpdatedByDisplayName, LogDescription, AppSettingsId, ResponseMode, EmailSignatureHtml, Deleted, LogAction)
    select @logId, N'EnquirySort Worker', N'Created default app settings',
           d.id, d.ResponseMode, d.EmailSignatureHtml, 0, 'Insert'
    from @_data d;
end
else
begin
    insert into @_data
    select top (1) id, ResponseMode, EmailSignatureHtml, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
    from tblAppSettings
    where Deleted = 0
    order by InsertDateUtc;
end

select * from @_data;";

        DynamicParameters parameters = new();
        parameters.Add("@id", SingletonId, DbType.Guid);
        parameters.Add("@logId", _combProvider.Create(), DbType.Guid);
        parameters.Add("@responseMode", (byte)_appSettings.EnquiryWorker.ResponseMode, DbType.Byte);
        parameters.Add("@emailSignatureHtml", "<p>Kind regards,<br/>Support Team</p>", DbType.String);

        CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
        return await sqlConnection.QuerySingleAsync<AppSetting>(cmd);
    }
}
