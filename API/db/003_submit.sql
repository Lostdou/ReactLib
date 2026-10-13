SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- las propuestas guardan los tags en TagsCsv pero NO crean filas en dbo.Tag: si cualquiera pudiera
-- crear tags, la taxonomia se llena de sinonimos antes de que un humano mire nada
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
    @DupOf       int          = NULL,
    @DupDist     tinyint      = NULL,
    @IpHash      binary(8)    = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Video WHERE TweetId = @TweetId AND MediaIndex = @MediaIndex)
    BEGIN
        SELECT 0 AS VideoId;
        RETURN;
    END

    INSERT dbo.Video (TweetId, MediaIndex, MediaKey, AuthorHandle, Title, SearchBlob, TagsCsv,
                      PosterPath, Mp4Path, DurationMs, Width, Height, Dhash, DupOf, DupDist,
                      Status, SubmitterIpHash, MetaFetchedUtc)
    VALUES (@TweetId, @MediaIndex, @MediaKey, @Handle, @Title,
            LEFT(dbo.FoldText(@Title) + ' ' + ISNULL(@TagsCsv, ''), 200), @TagsCsv,
            @PosterPath, @Mp4Path, @DurationMs, @Width, @Height, @Dhash, @DupOf, @DupDist,
            0, @IpHash, CASE WHEN @Mp4Path IS NULL THEN NULL ELSE SYSUTCDATETIME() END);

    SELECT CAST(SCOPE_IDENTITY() AS int) AS VideoId;
END
GO

CREATE OR ALTER PROCEDURE dbo.RejectVideo @VideoId int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Video SET Status = 2 WHERE VideoId = @VideoId AND Status = 0;
    SELECT @@ROWCOUNT AS Affected;
END
GO

-- aprobar reusa PublishVideo, que es el unico lugar donde nacen los tags
CREATE OR ALTER PROCEDURE dbo.ApproveSubmission
    @VideoId int,
    @Title   nvarchar(80) = NULL,
    @Tags    nvarchar(160) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Handle varchar(15), @TweetId bigint, @MediaIndex tinyint,
            @PosterPath varchar(120), @Mp4Path varchar(200),
            @DurationMs int, @Width smallint, @Height smallint, @Dhash bigint,
            @CurrentTitle nvarchar(80), @CurrentTags varchar(160);

    SELECT @Handle = AuthorHandle, @TweetId = TweetId, @MediaIndex = MediaIndex,
           @PosterPath = PosterPath, @Mp4Path = Mp4Path, @DurationMs = DurationMs,
           @Width = Width, @Height = Height, @Dhash = Dhash,
           @CurrentTitle = Title, @CurrentTags = TagsCsv
    FROM dbo.Video WHERE VideoId = @VideoId;

    IF @TweetId IS NULL
    BEGIN
        SELECT 0 AS PubSeq;
        RETURN;
    END

    -- el moderador puede corregir titulo y tags al aprobar; si no manda nada, valen los propuestos
    SET @Title = ISNULL(NULLIF(LTRIM(RTRIM(@Title)), N''), @CurrentTitle);
    SET @Tags  = ISNULL(NULLIF(LTRIM(RTRIM(@Tags)), N''), @CurrentTags);

    EXEC dbo.PublishVideo
        @Handle = @Handle, @TweetId = @TweetId,
        @Title = @Title, @Tags = @Tags,
        @MediaIndex = @MediaIndex, @PosterPath = @PosterPath, @Mp4Path = @Mp4Path,
        @DurationMs = @DurationMs, @Width = @Width, @Height = @Height, @Dhash = @Dhash;
END
GO
