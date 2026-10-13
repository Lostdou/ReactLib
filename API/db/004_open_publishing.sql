SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('dbo.Video', 'ProposedTags') IS NULL
    ALTER TABLE dbo.Video ADD ProposedTags varchar(160) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Video_ProposedTags' AND object_id = OBJECT_ID('dbo.Video'))
    CREATE NONCLUSTERED INDEX IX_Video_ProposedTags ON dbo.Video (VideoId)
        INCLUDE (ProposedTags) WHERE ProposedTags IS NOT NULL;
GO

-- publica directo, pero solo enlaza tags que YA existen: los demas quedan como propuestos para
-- que un humano decida. Sin esto la taxonomia se llena de sinonimos en una semana.
CREATE OR ALTER PROCEDURE dbo.SubmitVideo
    @Handle      varchar(15),
    @TweetId     bigint,
    @Title       nvarchar(80),
    @TagsCsv     varchar(160) = NULL,
    @MediaIndex  tinyint      = 1,
    @PosterPath  varchar(120) = NULL,
    @Mp4Path     varchar(200) = NULL,
    @DurationMs  int          = NULL,
    @Width       smallint     = NULL,
    @Height      smallint     = NULL,
    @MediaKey    bigint       = NULL,
    @Dhash       bigint       = NULL,
    @IpHash      binary(8)    = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Video WHERE TweetId = @TweetId AND MediaIndex = @MediaIndex)
    BEGIN
        SELECT 0 AS PubSeq;
        RETURN;
    END

    DECLARE @incoming TABLE (Slug varchar(30) PRIMARY KEY);
    INSERT @incoming (Slug)
    SELECT DISTINCT LEFT(LTRIM(RTRIM(value)), 30)
    FROM   STRING_SPLIT(ISNULL(@TagsCsv, ''), ',')
    WHERE  LTRIM(RTRIM(value)) <> '';

    DECLARE @known varchar(160) =
        (SELECT STRING_AGG(i.Slug, ',') WITHIN GROUP (ORDER BY i.Slug)
         FROM @incoming i WHERE EXISTS (SELECT 1 FROM dbo.Tag t WHERE t.NameNorm = i.Slug));

    DECLARE @proposed varchar(160) =
        (SELECT STRING_AGG(i.Slug, ',') WITHIN GROUP (ORDER BY i.Slug)
         FROM @incoming i WHERE NOT EXISTS (SELECT 1 FROM dbo.Tag t WHERE t.NameNorm = i.Slug));

    -- INSERT ... EXEC para que el SELECT de PublishVideo no se filtre como primer result set
    DECLARE @published TABLE (PubSeq int);
    INSERT @published (PubSeq)
    EXEC dbo.PublishVideo
        @Handle = @Handle, @TweetId = @TweetId, @Title = @Title, @Tags = @known,
        @MediaIndex = @MediaIndex, @PosterPath = @PosterPath, @Mp4Path = @Mp4Path,
        @DurationMs = @DurationMs, @Width = @Width, @Height = @Height, @Dhash = @Dhash;

    UPDATE dbo.Video
       SET ProposedTags    = @proposed,
           SubmitterIpHash = @IpHash,
           MediaKey        = ISNULL(MediaKey, @MediaKey)
     WHERE TweetId = @TweetId AND MediaIndex = @MediaIndex;

    SELECT PubSeq, ProposedTags FROM dbo.Video WHERE TweetId = @TweetId AND MediaIndex = @MediaIndex;
END
GO

-- a los N reportes el video vuelve a pendiente: sale solo de todos los indices filtrados
CREATE OR ALTER PROCEDURE dbo.ReportVideo
    @PubSeq    int,
    @Threshold tinyint = 3
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Video
       SET ReportCount = CASE WHEN ReportCount < 255 THEN ReportCount + 1 ELSE ReportCount END,
           Status      = CASE WHEN ReportCount + 1 >= @Threshold THEN 0 ELSE Status END
     WHERE PubSeq = @PubSeq AND Status = 1;

    IF @@ROWCOUNT > 0
        UPDATE dbo.Meta SET V = CAST(CAST(V AS int) + 1 AS varchar(64)) WHERE K = 'CatalogVersion';
END
GO

CREATE OR ALTER PROCEDURE dbo.ApproveProposedTag @Slug varchar(30), @Name nvarchar(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Tag WHERE NameNorm = @Slug)
        INSERT dbo.Tag (Name, NameNorm) VALUES (ISNULL(@Name, @Slug), @Slug);

    DECLARE @TagId smallint = (SELECT TagId FROM dbo.Tag WHERE NameNorm = @Slug);

    INSERT dbo.VideoTag (TagId, PubSeq, VideoId)
    SELECT @TagId, v.PubSeq, v.VideoId
    FROM   dbo.Video v
    WHERE  v.PubSeq IS NOT NULL
      AND  ',' + v.ProposedTags + ',' LIKE '%,' + @Slug + ',%'
      AND  NOT EXISTS (SELECT 1 FROM dbo.VideoTag vt WHERE vt.VideoId = v.VideoId AND vt.TagId = @TagId);

    UPDATE dbo.Video
       SET TagsCsv      = LEFT(ISNULL(TagsCsv + ',', '') + @Slug, 160),
           ProposedTags = NULLIF(REPLACE(REPLACE(',' + ProposedTags + ',', ',' + @Slug + ',', ','), ',,', ','), ',')
     WHERE ProposedTags IS NOT NULL
       AND ',' + ProposedTags + ',' LIKE '%,' + @Slug + ',%';

    UPDATE dbo.Tag SET UseCount = (SELECT COUNT(*) FROM dbo.VideoTag vt WHERE vt.TagId = @TagId)
     WHERE TagId = @TagId;

    UPDATE dbo.Meta SET V = CAST(CAST(V AS int) + 1 AS varchar(64)) WHERE K = 'CatalogVersion';

    COMMIT TRANSACTION;

    SELECT @TagId AS TagId;
END
GO

CREATE OR ALTER PROCEDURE dbo.RejectProposedTag @Slug varchar(30)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Video
       SET ProposedTags = NULLIF(REPLACE(REPLACE(',' + ProposedTags + ',', ',' + @Slug + ',', ','), ',,', ','), ',')
     WHERE ProposedTags IS NOT NULL
       AND ',' + ProposedTags + ',' LIKE '%,' + @Slug + ',%';

    SELECT @@ROWCOUNT AS Affected;
END
GO
