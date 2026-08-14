/*
  Runtime admin settings: response mode + HTML email signature.
  Safe to re-run: creates table only when missing, seeds singleton row.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.tblAppSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblAppSettings
    (
        id                      uniqueidentifier NOT NULL
                                    CONSTRAINT PK_tblAppSettings PRIMARY KEY CLUSTERED,
        ResponseMode            tinyint NOT NULL
                                    CONSTRAINT DF_tblAppSettings_ResponseMode DEFAULT (1),
        EmailSignatureHtml      nvarchar(max) NULL,
        InsertDateUtc           datetime2(3) NOT NULL
                                    CONSTRAINT DF_tblAppSettings_InsertDateUtc DEFAULT (sysutcdatetime()),
        UpdatedDateUtc          datetime2(3) NOT NULL
                                    CONSTRAINT DF_tblAppSettings_UpdatedDateUtc DEFAULT (sysutcdatetime()),
        Deleted                 bit NOT NULL
                                    CONSTRAINT DF_tblAppSettings_Deleted DEFAULT (0),
        ConcurrencyKey          AS CONVERT(varbinary(4), BINARY_CHECKSUM(ResponseMode, EmailSignatureHtml))
                                    PERSISTED NOT NULL
    );

    CREATE TABLE dbo.tblAppSettings_Log
    (
        id                      uniqueidentifier NOT NULL
                                    CONSTRAINT PK_tblAppSettings_Log PRIMARY KEY CLUSTERED,
        InsertDateUtc           datetime2(3) NOT NULL
                                    CONSTRAINT DF_tblAppSettings_Log_InsertDateUtc DEFAULT (sysutcdatetime()),
        UpdatedByUid            uniqueidentifier NULL,
        UpdatedByDisplayName    nvarchar(200) NULL,
        UpdatedByIpAddress      varchar(45) NULL,
        LogDescription          nvarchar(500) NULL,
        AppSettingsId           uniqueidentifier NOT NULL,
        ResponseMode            tinyint NULL,
        EmailSignatureHtml      nvarchar(max) NULL,
        Deleted                 bit NULL,
        OldResponseMode         tinyint NULL,
        OldEmailSignatureHtml   nvarchar(max) NULL,
        OldDeleted              bit NULL,
        LogAction               varchar(20) NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.tblAppSettings WHERE Deleted = 0)
BEGIN
    INSERT INTO dbo.tblAppSettings (id, ResponseMode, EmailSignatureHtml)
    VALUES (
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        1, -- Draft
        N'<p>Kind regards,<br/>Support Team</p>'
    );

    INSERT INTO dbo.tblAppSettings_Log
        (id, UpdatedByDisplayName, LogDescription, AppSettingsId, ResponseMode, EmailSignatureHtml, Deleted, LogAction)
    VALUES
        ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', N'EnquirySort Bootstrap', N'Seeded default app settings',
         'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 1, N'<p>Kind regards,<br/>Support Team</p>', 0, 'Insert');
END
GO
