/*
    Idempotente: se puede correr varias veces.

    PROHIBIDO en esta base: nvarchar(max), guardar el JSON crudo de fxtwitter, y cualquier tabla
    con una fila por accion de usuario (clicks, vistas, visitas). El plan gratuito da 1 GB
    contando MDF + LDF, y esas tres cosas son las unicas capaces de agotarlo: 20k videos son 40 MB.
*/

-- sqlcmd arranca con QUOTED_IDENTIFIER OFF y asi no se puede crear ningun indice filtrado
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @db sysname = DB_NAME();
DECLARE @sql nvarchar(200);
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = @db AND recovery_model <> 3)
BEGIN
    SET @sql = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET RECOVERY SIMPLE';
    EXEC sys.sp_executesql @sql;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'PubSeqNum' AND schema_id = SCHEMA_ID('dbo'))
    CREATE SEQUENCE dbo.PubSeqNum AS int START WITH 1 INCREMENT BY 1 CACHE 20;
GO

IF OBJECT_ID('dbo.Video', 'U') IS NULL
CREATE TABLE dbo.Video (
    VideoId         int           IDENTITY(1,1) NOT NULL,
    PubSeq          int           NULL,            -- se asigna al aprobar: ordena por visibilidad, no por envio
    TweetId         bigint        NOT NULL,
    MediaIndex      tinyint       NOT NULL,
    MediaKey        bigint        NULL,            -- media id de X: detecta el mismo video republicado por otra cuenta
    AuthorHandle    varchar(15)   NOT NULL,
    Title           nvarchar(80)  NOT NULL,
    SearchBlob      varchar(200)  NOT NULL,        -- evita full-text, que no esta instalado en el plan gratuito
    TagsCsv         varchar(160)  NULL,            -- denormalizado para que el indice covering resuelva la grilla sin join
    PosterPath      varchar(120)  NULL,            -- sin host: lo reconstruye la API y ahorra ~40 B por fila indexada
    Mp4Path         varchar(200)  NULL,
    DurationMs      int           NULL,
    Width           smallint      NULL,
    Height          smallint      NULL,
    Dhash           bigint        NULL,            -- bigint y no binary(8) para que BIT_COUNT(Dhash ^ @h) funcione directo
    DupOf           int           NULL,
    DupDist         tinyint       NULL,
    Status          tinyint       NOT NULL CONSTRAINT DF_Video_Status DEFAULT 0,  -- 0 pendiente, 1 aprobado, 2 rechazado, 3 muerto
    CopyCount       int           NOT NULL CONSTRAINT DF_Video_CC  DEFAULT 0,
    CopyCount7d     int           NOT NULL CONSTRAINT DF_Video_CC7 DEFAULT 0,     -- sin el decay diario los mismos 20 videos quedan arriba para siempre
    ReportCount     tinyint       NOT NULL CONSTRAINT DF_Video_RC  DEFAULT 0,
    DeadStrikes     tinyint       NOT NULL CONSTRAINT DF_Video_DS  DEFAULT 0,
    CreatedUtc      datetime2(0)  NOT NULL CONSTRAINT DF_Video_Cr  DEFAULT SYSUTCDATETIME(),
    ApprovedUtc     datetime2(0)  NULL,
    MetaFetchedUtc  smalldatetime NULL,            -- NULL = fxtwitter no respondio todavia y un job la completa despues
    LastCheckedUtc  smalldatetime NULL,
    SubmitterIpHash binary(8)     NULL,            -- HMAC truncado: alcanza para banear reincidentes sin guardar PII
    CONSTRAINT PK_Video PRIMARY KEY CLUSTERED (VideoId),
    CONSTRAINT UQ_Video_TweetMedia UNIQUE NONCLUSTERED (TweetId, MediaIndex),
    CONSTRAINT CK_Video_MediaIndex CHECK (MediaIndex BETWEEN 1 AND 4),
    CONSTRAINT CK_Video_Status CHECK (Status BETWEEN 0 AND 3)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Video_MediaKey' AND object_id = OBJECT_ID('dbo.Video'))
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Video_MediaKey ON dbo.Video (MediaKey) WHERE MediaKey IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Video_PubSeq' AND object_id = OBJECT_ID('dbo.Video'))
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Video_PubSeq ON dbo.Video (PubSeq) WHERE PubSeq IS NOT NULL;
GO

-- fillfactor alto: los inserts caen siempre al final del rango, no hay page splits que amortiguar
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Video_New' AND object_id = OBJECT_ID('dbo.Video'))
    CREATE NONCLUSTERED INDEX IX_Video_New ON dbo.Video (PubSeq DESC)
        INCLUDE (TweetId, MediaIndex, AuthorHandle, Title, TagsCsv,
                 PosterPath, Mp4Path, DurationMs, Width, Height, CopyCount)
        WHERE Status = 1 WITH (FILLFACTOR = 95);
GO
-- fillfactor bajo: la clave cambia en cada flush de contadores y en el decay diario
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Video_Top' AND object_id = OBJECT_ID('dbo.Video'))
    CREATE NONCLUSTERED INDEX IX_Video_Top ON dbo.Video (CopyCount7d DESC, PubSeq DESC)
        INCLUDE (TweetId, MediaIndex, AuthorHandle, Title, TagsCsv,
                 PosterPath, Mp4Path, DurationMs, Width, Height, CopyCount)
        WHERE Status = 1 WITH (FILLFACTOR = 80);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Video_Search' AND object_id = OBJECT_ID('dbo.Video'))
    CREATE NONCLUSTERED INDEX IX_Video_Search ON dbo.Video (SearchBlob) INCLUDE (PubSeq) WHERE Status = 1;
GO
-- 31 B por fila: a 50k videos son 1,5 MB, asi que el scan con BIT_COUNT es sub-milisegundo
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Video_Dhash' AND object_id = OBJECT_ID('dbo.Video'))
    CREATE NONCLUSTERED INDEX IX_Video_Dhash ON dbo.Video (Dhash)
        INCLUDE (TweetId, MediaIndex, Title) WHERE Dhash IS NOT NULL AND Status IN (0, 1);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Video_Pending' AND object_id = OBJECT_ID('dbo.Video'))
    CREATE NONCLUSTERED INDEX IX_Video_Pending ON dbo.Video (CreatedUtc)
        INCLUDE (TweetId, MediaIndex, AuthorHandle, DupOf, DupDist) WHERE Status = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Video_Recheck' AND object_id = OBJECT_ID('dbo.Video'))
    CREATE NONCLUSTERED INDEX IX_Video_Recheck ON dbo.Video (LastCheckedUtc) WHERE Status = 1;
GO

IF OBJECT_ID('dbo.Tag', 'U') IS NULL
CREATE TABLE dbo.Tag (
    TagId    smallint     IDENTITY(1,1) NOT NULL,
    Name     nvarchar(30) NOT NULL,
    NameNorm varchar(30)  NOT NULL,
    UseCount int          NOT NULL CONSTRAINT DF_Tag_UC DEFAULT 0,
    CONSTRAINT PK_Tag PRIMARY KEY CLUSTERED (TagId),
    CONSTRAINT UQ_Tag_NameNorm UNIQUE NONCLUSTERED (NameNorm)
);
GO

IF OBJECT_ID('dbo.VideoTag', 'U') IS NULL
CREATE TABLE dbo.VideoTag (
    TagId   smallint NOT NULL,
    PubSeq  int      NOT NULL,   -- copiado del video: convierte "tag X, recientes primero" en un range scan sin sort
    VideoId int      NOT NULL,
    CONSTRAINT PK_VideoTag PRIMARY KEY CLUSTERED (TagId, PubSeq DESC),
    CONSTRAINT FK_VideoTag_Tag FOREIGN KEY (TagId) REFERENCES dbo.Tag (TagId)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VideoTag_Video' AND object_id = OBJECT_ID('dbo.VideoTag'))
    CREATE UNIQUE NONCLUSTERED INDEX IX_VideoTag_Video ON dbo.VideoTag (VideoId, TagId);
GO

IF OBJECT_ID('dbo.Meta', 'U') IS NULL
CREATE TABLE dbo.Meta (
    K varchar(32) NOT NULL CONSTRAINT PK_Meta PRIMARY KEY CLUSTERED,
    V varchar(64) NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.Meta WHERE K = 'CatalogVersion')
    INSERT dbo.Meta (K, V) VALUES ('CatalogVersion', '1');
GO

IF TYPE_ID('dbo.CountDelta') IS NULL
    CREATE TYPE dbo.CountDelta AS TABLE (VideoId int PRIMARY KEY, Delta int NOT NULL);
GO

-- un round trip por minuto en vez de uno por click: las 8 conexiones del plan no toleran lo segundo
CREATE OR ALTER PROCEDURE dbo.ApplyCopyDeltas @deltas dbo.CountDelta READONLY
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE v
       SET CopyCount   = v.CopyCount   + d.Delta,
           CopyCount7d = v.CopyCount7d + d.Delta
      FROM dbo.Video v
      INNER JOIN @deltas d ON d.VideoId = v.VideoId
     WHERE v.Status = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.BumpCatalogVersion
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Meta SET V = CAST(CAST(V AS int) + 1 AS varchar(64)) WHERE K = 'CatalogVersion';
    SELECT CAST(V AS int) AS CatalogVersion FROM dbo.Meta WHERE K = 'CatalogVersion';
END
GO
