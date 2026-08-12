using System.Data;
using Dapper;
using EnquirySort.Api.Configuration;
using EnquirySort.Api.Enums;
using EnquirySort.Api.Features.MailingLists.CreateMailingList;
using EnquirySort.Api.Features.MailingLists.DeleteMailingList;
using EnquirySort.Api.Features.MailingLists.UpdateMailingList;
using EnquirySort.Api.Models;
using EnquirySort.Api.Utilities;
using Microsoft.Data.SqlClient;
using RT.Comb;

namespace EnquirySort.Api.Repositories;

public sealed class MailingListsRepository
{
    private readonly AppSettings _appSettings;
    private readonly ICombProvider _combProvider;

    public MailingListsRepository(AppSettings appSettings, ICombProvider combProvider)
    {
        _appSettings = appSettings;
        _combProvider = combProvider;
    }

    public async Task<(SqlQueryResult, MailingList?)> CreateMailingListAsync(
        CreateMailingListRequest req,
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
    Name nvarchar(100),
    Address nvarchar(320),
    Description nvarchar(500),
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
    insert into tblMailingLists (id, Name, Address, Description, InsertDateUtc, UpdatedDateUtc)
    output inserted.id, inserted.Name, inserted.Address, inserted.Description,
           inserted.InsertDateUtc, inserted.UpdatedDateUtc, inserted.Deleted, inserted.ConcurrencyKey
    into @_data
    select @id, @name, @address, @description, @_now, @_now
    where not exists (
        select * from tblMailingLists
        where Deleted = 0 and Name = @name);

    if @@ROWCOUNT = 1
    begin
        set @_result = 1;
        insert into tblMailingLists_Log
            (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
             MailingListId, Name, Address, Description, Deleted, LogAction)
        select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, null,
               d.id, d.Name, d.Address, d.Description, d.Deleted, 'Insert'
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
        parameters.Add("@name", req.Name, DbType.String, size: 100);
        parameters.Add("@address", req.Address, DbType.String, size: 320);
        parameters.Add("@description", req.Description, DbType.String, size: 500);
        parameters.Add("@lockResourceName", $"tblMailingLists_Name_{Toolbox.Sha1Upper(req.Name!)}", DbType.AnsiString, size: 200);
        parameters.Add("@adminUserUid", adminUserUid, DbType.Guid);
        parameters.Add("@adminUserDisplayName", adminUserDisplayName, DbType.String, size: 200);
        parameters.Add("@remoteIpAddress", remoteIpAddress, DbType.AnsiString, size: 45);

        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(sql, parameters);
        int resultCode = await gridReader.ReadFirstAsync<int>();
        MailingList? entity = !gridReader.IsConsumed
            ? await gridReader.ReadFirstOrDefaultAsync<MailingList>()
            : null;

        SqlQueryResult result = resultCode switch
        {
            1 => SqlQueryResult.Ok,
            2 => SqlQueryResult.RecordAlreadyExists,
            _ => SqlQueryResult.UnknownError
        };

        return (result, entity);
    }

    public async Task<MailingList?> GetMailingListAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);

        // lang=sql
        string sql = @"
select id, Name, Address, Description, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
from tblMailingLists
where Deleted = 0 and id = @id";

        DynamicParameters parameters = new();
        parameters.Add("@id", id, DbType.Guid);
        CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
        return await sqlConnection.QueryFirstOrDefaultAsync<MailingList>(cmd);
    }

    public async Task<(SqlQueryResult, MailingList?)> UpdateMailingListAsync(
        UpdateMailingListRequest req,
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
    Name nvarchar(100),
    Address nvarchar(320),
    Description nvarchar(500),
    InsertDateUtc datetime2(3),
    UpdatedDateUtc datetime2(3),
    Deleted bit,
    ConcurrencyKey varbinary(4),
    OldName nvarchar(100),
    OldAddress nvarchar(320),
    OldDescription nvarchar(500));

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
    update tblMailingLists
    set Name = @name,
        Address = @address,
        Description = @description,
        UpdatedDateUtc = @_now
    output inserted.id, inserted.Name, inserted.Address, inserted.Description,
           inserted.InsertDateUtc, inserted.UpdatedDateUtc, inserted.Deleted, inserted.ConcurrencyKey,
           deleted.Name, deleted.Address, deleted.Description
    into @_data
    where id = @id
      and Deleted = 0
      and ConcurrencyKey = @concurrencyKey
      and not exists (
          select * from tblMailingLists x
          where x.Deleted = 0 and x.Name = @name and x.id <> @id);

    if @@ROWCOUNT = 1
    begin
        set @_result = 1;
        insert into tblMailingLists_Log
            (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
             MailingListId, Name, Address, Description, Deleted,
             OldName, OldAddress, OldDescription, OldDeleted, LogAction)
        select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, null,
               d.id, d.Name, d.Address, d.Description, d.Deleted,
               d.OldName, d.OldAddress, d.OldDescription, 0, 'Update'
        from @_data d;
        commit transaction;
    end
    else
    begin
        rollback transaction;

        select top 1
            id, Name, Address, Description, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
        from tblMailingLists
        where id = @id and Deleted = 0;

        -- result decided in C#
        set @_result = 0;
    end
end

if @_result = 1
begin
    select @_result;
    select id, Name, Address, Description, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey from @_data;
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
        parameters.Add("@name", req.Name, DbType.String, size: 100);
        parameters.Add("@address", req.Address, DbType.String, size: 320);
        parameters.Add("@description", req.Description, DbType.String, size: 500);
        parameters.Add("@concurrencyKey", req.ConcurrencyKey, DbType.Binary, size: 4);
        parameters.Add("@lockResourceName", $"tblMailingLists_Name_{Toolbox.Sha1Upper(req.Name!)}", DbType.AnsiString, size: 200);
        parameters.Add("@adminUserUid", adminUserUid, DbType.Guid);
        parameters.Add("@adminUserDisplayName", adminUserDisplayName, DbType.String, size: 200);
        parameters.Add("@remoteIpAddress", remoteIpAddress, DbType.AnsiString, size: 45);

        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(sql, parameters);
        int resultCode = await gridReader.ReadFirstAsync<int>();

        if (resultCode == 1)
        {
            MailingList? updated = !gridReader.IsConsumed
                ? await gridReader.ReadFirstOrDefaultAsync<MailingList>()
                : null;
            return (SqlQueryResult.Ok, updated);
        }

        if (resultCode == 3)
        {
            return (SqlQueryResult.RecordAlreadyExists, null);
        }

        MailingList? current = !gridReader.IsConsumed
            ? await gridReader.ReadFirstOrDefaultAsync<MailingList>()
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

    public async Task<SqlQueryResult> DeleteMailingListAsync(
        DeleteMailingListRequest req,
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
    Name nvarchar(100),
    Address nvarchar(320),
    Description nvarchar(500),
    Deleted bit,
    OldDeleted bit,
    ConcurrencyKey varbinary(4));

update tblMailingLists
set Deleted = 1,
    UpdatedDateUtc = @_now
output inserted.id, inserted.Name, inserted.Address, inserted.Description, inserted.Deleted,
       deleted.Deleted, inserted.ConcurrencyKey
into @_data
where id = @id
  and Deleted = 0
  and ConcurrencyKey = @concurrencyKey;

if @@ROWCOUNT = 1
begin
    set @_result = 1;
    insert into tblMailingLists_Log
        (id, UpdatedByUid, UpdatedByDisplayName, UpdatedByIpAddress, LogDescription,
         MailingListId, Name, Address, Description, Deleted,
         OldName, OldAddress, OldDescription, OldDeleted, LogAction)
    select @logId, @adminUserUid, @adminUserDisplayName, @remoteIpAddress, null,
           d.id, d.Name, d.Address, d.Description, d.Deleted,
           d.Name, d.Address, d.Description, d.OldDeleted, 'Delete'
    from @_data d;
end

select @_result;
select id, Name, Address, Description, InsertDateUtc = sysutcdatetime(), UpdatedDateUtc = sysutcdatetime(),
       Deleted = 0, ConcurrencyKey
from tblMailingLists
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
        MailingList? current = !gridReader.IsConsumed
            ? await gridReader.ReadFirstOrDefaultAsync<MailingList>()
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

    public async Task<DropdownResponse> ListMailingListsForDropdownAsync(
        string? search,
        long? requestCounter,
        CancellationToken cancellationToken = default)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);

        // lang=sql
        string sql = @"
select id as Value, Name as Text
from tblMailingLists
where Deleted = 0
  and (@search is null or Name like '%' + @search + '%' or Address like '%' + @search + '%')
order by Name";

        DynamicParameters parameters = new();
        parameters.Add("@search", string.IsNullOrWhiteSpace(search) ? null : search.Trim(), DbType.String, size: 100);
        CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
        List<SelectListItem> records = (await sqlConnection.QueryAsync<SelectListItem>(cmd)).AsList();
        return new DropdownResponse { RequestCounter = requestCounter, Records = records };
    }

    public async Task<DataTableResponse<MailingList>> ListMailingListsForDataTableAsync(
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
            SortType.Created => "InsertDateUtc desc",
            SortType.Updated => "UpdatedDateUtc desc",
            SortType.Email => "Address asc",
            _ => "Name asc"
        };

        // lang=sql
        string sql = $@"
declare @_search nvarchar(100) = @search;

select count(*)
from tblMailingLists
where Deleted = 0
  and (@_search is null or Name like '%' + @_search + '%' or Address like '%' + @_search + '%' or Description like '%' + @_search + '%');

select id, Name, Address, Description, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
from tblMailingLists
where Deleted = 0
  and (@_search is null or Name like '%' + @_search + '%' or Address like '%' + @_search + '%' or Description like '%' + @_search + '%')
order by {orderBy}
offset @offset rows fetch next @pageSize rows only;";

        DynamicParameters parameters = new();
        parameters.Add("@search", string.IsNullOrWhiteSpace(search) ? null : search.Trim(), DbType.String, size: 100);
        parameters.Add("@offset", (pageNumber - 1) * pageSize, DbType.Int32);
        parameters.Add("@pageSize", pageSize, DbType.Int32);

        CommandDefinition cmd = new(sql, parameters, cancellationToken: cancellationToken);
        using SqlMapper.GridReader gridReader = await sqlConnection.QueryMultipleAsync(cmd);
        int totalCount = await gridReader.ReadFirstAsync<int>();
        List<MailingList> records = !gridReader.IsConsumed
            ? (await gridReader.ReadAsync<MailingList>()).AsList()
            : [];

        return new DataTableResponse<MailingList>
        {
            RequestCounter = requestCounter,
            Records = records,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<List<MailingList>> ListAllActiveAsync(CancellationToken cancellationToken = default)
    {
        using SqlConnection sqlConnection = new(_appSettings.ConnectionStrings.EnquirySort);

        // lang=sql
        string sql = @"
select id, Name, Address, Description, InsertDateUtc, UpdatedDateUtc, Deleted, ConcurrencyKey
from tblMailingLists
where Deleted = 0
order by Name";

        CommandDefinition cmd = new(sql, cancellationToken: cancellationToken);
        return (await sqlConnection.QueryAsync<MailingList>(cmd)).AsList();
    }
}
