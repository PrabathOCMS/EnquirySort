/*
  Adds ReplyStatus for draft/auto response workflow.
  Safe to re-run: skips if column already exists.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.tblEnquiries', N'ReplyStatus') IS NULL
BEGIN
    ALTER TABLE dbo.tblEnquiries ADD ReplyStatus tinyint NOT NULL
        CONSTRAINT DF_tblEnquiries_ReplyStatus DEFAULT (0);

    -- Sent / draft with body / otherwise none
    EXEC(N'
        UPDATE dbo.tblEnquiries
        SET ReplyStatus = CASE
            WHEN ReplySent = 1 THEN 2
            WHEN ReplyBody IS NOT NULL AND LTRIM(RTRIM(ReplyBody)) <> N'''' THEN 1
            ELSE 0
        END');
END
GO

-- Recreate ConcurrencyKey to include ReplyBody + ReplyStatus (needed for draft edits)
IF EXISTS (
    SELECT 1
    FROM sys.computed_columns
    WHERE object_id = OBJECT_ID(N'dbo.tblEnquiries')
      AND name = N'ConcurrencyKey'
)
BEGIN
    ALTER TABLE dbo.tblEnquiries DROP COLUMN ConcurrencyKey;
END
GO

ALTER TABLE dbo.tblEnquiries ADD ConcurrencyKey AS
    CONVERT(varbinary(4), BINARY_CHECKSUM(FromAddress, Subject, Action, ReplyBody, ReplySent, ReplyStatus))
    PERSISTED NOT NULL;
GO

IF COL_LENGTH(N'dbo.tblEnquiries_Log', N'ReplyStatus') IS NULL
BEGIN
    ALTER TABLE dbo.tblEnquiries_Log ADD ReplyStatus tinyint NULL;
    ALTER TABLE dbo.tblEnquiries_Log ADD OldReplyStatus tinyint NULL;
END
GO
