SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- convertir a una codepage sin acentos latinos es lo que los saca; tiene que dar igual que TextNorm.Fold en C#
CREATE OR ALTER FUNCTION dbo.FoldText (@text nvarchar(300))
RETURNS varchar(300)
AS
BEGIN
    RETURN LOWER(CONVERT(varchar(300), @text COLLATE SQL_Latin1_General_CP1253_CI_AI));
END
GO

CREATE OR ALTER PROCEDURE dbo.PublishVideo
    @Handle      varchar(15),
    @TweetId     bigint,
    @Title       nvarchar(80),
    @Tags        nvarchar(160) = NULL,
    @MediaIndex  tinyint       = 1,
    @PosterPath  varchar(120)  = NULL,
    @Mp4Path     varchar(200)  = NULL,
    @DurationMs  int           = NULL,
    @Width       smallint      = NULL,
    @Height      smallint      = NULL,
    @Dhash       bigint        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @MaxTags int = 5;
    DECLARE @parsed TABLE (NameNorm varchar(30) PRIMARY KEY, Name nvarchar(30));

    INSERT @parsed (NameNorm, Name)
    SELECT TOP (@MaxTags)
           LEFT(REPLACE(dbo.FoldText(LTRIM(RTRIM(s.value))), ' ', '-'), 30),
           LEFT(MIN(LTRIM(RTRIM(s.value))), 30)
    FROM   STRING_SPLIT(ISNULL(@Tags, N''), ',') s
    WHERE  LTRIM(RTRIM(s.value)) <> N''
    GROUP BY LEFT(REPLACE(dbo.FoldText(LTRIM(RTRIM(s.value))), ' ', '-'), 30)
    ORDER BY 1;

    DECLARE @TagsCsv varchar(160) =
        (SELECT STRING_AGG(NameNorm, ',') WITHIN GROUP (ORDER BY NameNorm) FROM @parsed);

    DECLARE @SearchBlob varchar(200) =
        LEFT(dbo.FoldText(@Title) + ' ' + ISNULL(@TagsCsv, ''), 200);

    DECLARE @MediaKey bigint = TRY_CAST(
        SUBSTRING(@Mp4Path,
                  NULLIF(CHARINDEX('_video/', @Mp4Path), 0) + 7,
                  CHARINDEX('/', @Mp4Path + '/', NULLIF(CHARINDEX('_video/', @Mp4Path), 0) + 7)
                      - NULLIF(CHARINDEX('_video/', @Mp4Path), 0) - 7)
        AS bigint);

    BEGIN TRANSACTION;

    DECLARE @VideoId int =
        (SELECT VideoId FROM dbo.Video WHERE TweetId = @TweetId AND MediaIndex = @MediaIndex);

    IF @VideoId IS NULL
    BEGIN
        INSERT dbo.Video (PubSeq, TweetId, MediaIndex, MediaKey, AuthorHandle, Title, SearchBlob,
                          TagsCsv, PosterPath, Mp4Path, DurationMs, Width, Height, Dhash,
                          Status, ApprovedUtc, MetaFetchedUtc)
        VALUES (NEXT VALUE FOR dbo.PubSeqNum, @TweetId, @MediaIndex, @MediaKey, @Handle, @Title,
                @SearchBlob, @TagsCsv, @PosterPath, @Mp4Path, @DurationMs, @Width, @Height, @Dhash,
                1, SYSUTCDATETIME(), CASE WHEN @Mp4Path IS NULL THEN NULL ELSE SYSUTCDATETIME() END);

        SET @VideoId = CAST(SCOPE_IDENTITY() AS int);
    END
    ELSE
    BEGIN
        -- NEXT VALUE FOR no se puede usar dentro de ISNULL, asi que se resuelve antes del UPDATE
        DECLARE @ExistingSeq int = (SELECT PubSeq FROM dbo.Video WHERE VideoId = @VideoId);
        IF @ExistingSeq IS NULL SET @ExistingSeq = NEXT VALUE FOR dbo.PubSeqNum;

        UPDATE dbo.Video
           SET AuthorHandle = @Handle,
               Title        = @Title,
               SearchBlob   = @SearchBlob,
               TagsCsv      = @TagsCsv,
               MediaKey     = ISNULL(@MediaKey, MediaKey),
               PosterPath   = ISNULL(@PosterPath, PosterPath),
               Mp4Path      = ISNULL(@Mp4Path, Mp4Path),
               DurationMs   = ISNULL(@DurationMs, DurationMs),
               Width        = ISNULL(@Width, Width),
               Height       = ISNULL(@Height, Height),
               Dhash        = ISNULL(@Dhash, Dhash),
               MetaFetchedUtc = CASE WHEN @Mp4Path IS NULL THEN MetaFetchedUtc ELSE SYSUTCDATETIME() END,
               Status       = 1,
               PubSeq       = @ExistingSeq,
               ApprovedUtc  = ISNULL(ApprovedUtc, SYSUTCDATETIME())
         WHERE VideoId = @VideoId;
    END

    DECLARE @PubSeq int = (SELECT PubSeq FROM dbo.Video WHERE VideoId = @VideoId);

    INSERT dbo.Tag (Name, NameNorm)
    SELECT p.Name, p.NameNorm
    FROM   @parsed p
    WHERE  NOT EXISTS (SELECT 1 FROM dbo.Tag t WHERE t.NameNorm = p.NameNorm);

    DELETE vt
      FROM dbo.VideoTag vt
     WHERE vt.VideoId = @VideoId;

    INSERT dbo.VideoTag (TagId, PubSeq, VideoId)
    SELECT t.TagId, @PubSeq, @VideoId
    FROM   dbo.Tag t
    INNER JOIN @parsed p ON p.NameNorm = t.NameNorm;

    UPDATE t
       SET UseCount = (SELECT COUNT(*) FROM dbo.VideoTag vt WHERE vt.TagId = t.TagId)
      FROM dbo.Tag t
     WHERE EXISTS (SELECT 1 FROM @parsed p WHERE p.NameNorm = t.NameNorm);

    UPDATE dbo.Meta SET V = CAST(CAST(V AS int) + 1 AS varchar(64)) WHERE K = 'CatalogVersion';

    COMMIT TRANSACTION;

    SELECT @PubSeq AS PubSeq;
END
GO
