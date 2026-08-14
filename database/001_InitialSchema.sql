SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
GO

/*
  EnquirySort initial schema — FastEndpoints + Dapper conventions.
  Run against SQL Server before starting the API.
*/

if db_id(N'EnquirySort') is null
begin
    create database EnquirySort;
end
go

use EnquirySort;
go

create table tblMailingLists
(
    id              uniqueidentifier not null
                        constraint DF_tblMailingLists_id default (newid())
                        constraint PK_tblMailingLists primary key clustered,
    Name            nvarchar(100) not null,
    Address         nvarchar(320) not null,
    Description     nvarchar(500) null,
    InsertDateUtc   datetime2(3) not null
                        constraint DF_tblMailingLists_InsertDateUtc default (sysutcdatetime()),
    UpdatedDateUtc  datetime2(3) not null
                        constraint DF_tblMailingLists_UpdatedDateUtc default (sysutcdatetime()),
    Deleted         bit not null
                        constraint DF_tblMailingLists_Deleted default (0),
    ConcurrencyKey  as convert(varbinary(4), binary_checksum(Name, Address, Description))
                        persisted not null
);
go

create unique nonclustered index UX_tblMailingLists_Name
    on tblMailingLists (Name)
    where Deleted = 0;
go

create nonclustered index IX_tblMailingLists_Name
    on tblMailingLists (Name)
    where Deleted = 0;
go

create table tblMailingLists_Log
(
    id                      uniqueidentifier not null
                                constraint PK_tblMailingLists_Log primary key clustered,
    InsertDateUtc           datetime2(3) not null
                                constraint DF_tblMailingLists_Log_InsertDateUtc default (sysutcdatetime()),
    UpdatedByUid            uniqueidentifier null,
    UpdatedByDisplayName    nvarchar(200) null,
    UpdatedByIpAddress      varchar(45) null,
    LogDescription          nvarchar(max) null,
    MailingListId           uniqueidentifier not null,
    Name                    nvarchar(100) null,
    Address                 nvarchar(320) null,
    Description             nvarchar(500) null,
    Deleted                 bit null,
    OldName                 nvarchar(100) null,
    OldAddress              nvarchar(320) null,
    OldDescription          nvarchar(500) null,
    OldDeleted              bit null,
    LogAction               varchar(6) not null,
    CascadeFrom             varchar(128) null,
    CascadeLogId            uniqueidentifier null
);
go

create table tblKnowledgeArticles
(
    id              uniqueidentifier not null
                        constraint DF_tblKnowledgeArticles_id default (newid())
                        constraint PK_tblKnowledgeArticles primary key clustered,
    Title           nvarchar(200) not null,
    Slug            nvarchar(200) not null,
    Content         nvarchar(max) not null,
    InsertDateUtc   datetime2(3) not null
                        constraint DF_tblKnowledgeArticles_InsertDateUtc default (sysutcdatetime()),
    UpdatedDateUtc  datetime2(3) not null
                        constraint DF_tblKnowledgeArticles_UpdatedDateUtc default (sysutcdatetime()),
    Deleted         bit not null
                        constraint DF_tblKnowledgeArticles_Deleted default (0),
    ConcurrencyKey  as convert(varbinary(4), binary_checksum(Title, Slug, Content))
                        persisted not null
);
go

create unique nonclustered index UX_tblKnowledgeArticles_Slug
    on tblKnowledgeArticles (Slug)
    where Deleted = 0;
go

create nonclustered index IX_tblKnowledgeArticles_Title
    on tblKnowledgeArticles (Title)
    where Deleted = 0;
go

create table tblKnowledgeArticles_Log
(
    id                      uniqueidentifier not null
                                constraint PK_tblKnowledgeArticles_Log primary key clustered,
    InsertDateUtc           datetime2(3) not null
                                constraint DF_tblKnowledgeArticles_Log_InsertDateUtc default (sysutcdatetime()),
    UpdatedByUid            uniqueidentifier null,
    UpdatedByDisplayName    nvarchar(200) null,
    UpdatedByIpAddress      varchar(45) null,
    LogDescription          nvarchar(max) null,
    KnowledgeArticleId      uniqueidentifier not null,
    Title                   nvarchar(200) null,
    Slug                    nvarchar(200) null,
    Content                 nvarchar(max) null,
    Deleted                 bit null,
    OldTitle                nvarchar(200) null,
    OldSlug                 nvarchar(200) null,
    OldContent              nvarchar(max) null,
    OldDeleted              bit null,
    LogAction               varchar(6) not null,
    CascadeFrom             varchar(128) null,
    CascadeLogId            uniqueidentifier null
);
go

create table tblEnquiries
(
    id                          uniqueidentifier not null
                                    constraint DF_tblEnquiries_id default (newid())
                                    constraint PK_tblEnquiries primary key clustered,
    MessageId                   nvarchar(500) null,
    FromAddress                 nvarchar(320) not null,
    Subject                     nvarchar(500) not null,
    BodyText                    nvarchar(max) not null,
    Action                      tinyint not null,
    Confidence                  float not null,
    Reason                      nvarchar(1000) null,
    CustomerQuestion            nvarchar(1000) null,
    RoutedToMailingListId       uniqueidentifier null,
    ReplyBody                   nvarchar(max) null,
    ReplySent                   bit not null
                                    constraint DF_tblEnquiries_ReplySent default (0),
    ReplyStatus                 tinyint not null
                                    constraint DF_tblEnquiries_ReplyStatus default (0),
    ProcessedUtc                datetime2(3) not null,
    InsertDateUtc               datetime2(3) not null
                                    constraint DF_tblEnquiries_InsertDateUtc default (sysutcdatetime()),
    UpdatedDateUtc              datetime2(3) not null
                                    constraint DF_tblEnquiries_UpdatedDateUtc default (sysutcdatetime()),
    Deleted                     bit not null
                                    constraint DF_tblEnquiries_Deleted default (0),
    ConcurrencyKey              as convert(varbinary(4), binary_checksum(FromAddress, Subject, Action, ReplyBody, ReplySent, ReplyStatus))
                                    persisted not null
);
go

create nonclustered index IX_tblEnquiries_ProcessedUtc
    on tblEnquiries (ProcessedUtc desc)
    where Deleted = 0;
go

create nonclustered index IX_tblEnquiries_FromAddress
    on tblEnquiries (FromAddress)
    where Deleted = 0;
go

create table tblEnquiries_Log
(
    id                      uniqueidentifier not null
                                constraint PK_tblEnquiries_Log primary key clustered,
    InsertDateUtc           datetime2(3) not null
                                constraint DF_tblEnquiries_Log_InsertDateUtc default (sysutcdatetime()),
    UpdatedByUid            uniqueidentifier null,
    UpdatedByDisplayName    nvarchar(200) null,
    UpdatedByIpAddress      varchar(45) null,
    LogDescription          nvarchar(max) null,
    EnquiryId               uniqueidentifier not null,
    FromAddress             nvarchar(320) null,
    Subject                 nvarchar(500) null,
    Action                  tinyint null,
    ReplySent               bit null,
    ReplyStatus             tinyint null,
    Deleted                 bit null,
    OldFromAddress          nvarchar(320) null,
    OldSubject              nvarchar(500) null,
    OldAction               tinyint null,
    OldReplySent            bit null,
    OldReplyStatus          tinyint null,
    OldDeleted              bit null,
    LogAction               varchar(6) not null,
    CascadeFrom             varchar(128) null,
    CascadeLogId            uniqueidentifier null
);
go

-- Seed sample mailing lists
insert into tblMailingLists (id, Name, Address, Description)
values
    ('11111111-1111-1111-1111-111111111111', N'sales', N'sales@example.com', N'New business, demos, pricing negotiations.'),
    ('22222222-2222-2222-2222-222222222222', N'support', N'support@example.com', N'Technical issues needing a human.'),
    ('33333333-3333-3333-3333-333333333333', N'billing', N'billing@example.com', N'Invoices, refunds, payment failures.');
go

insert into tblKnowledgeArticles (id, Title, Slug, Content)
values
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', N'Resetting Your Password', N'password-reset',
     N'Open https://app.oraclecms.com/forgot-password, enter your account email, and follow the reset link (expires in 60 minutes).'),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', N'Pricing Overview', N'pricing',
     N'Starter $49/mo, Growth $149/mo, Enterprise custom. Annual billing saves 15%. Contact sales for enterprise quotes.'),
    ('cccccccc-cccc-cccc-cccc-cccccccccccc', N'Connecting a Custom Domain', N'custom-domains',
     N'Growth and Enterprise plans support custom domains. Add the domain in Settings → Domains, create the DNS CNAME to sites.oraclecms.com, then Verify.');
go
