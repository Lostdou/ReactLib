/*
    Los hashes viejos se generaron con SHA256('reactlib:' + ip), con el salt escrito en el codigo.
    Al abrir el repo ese salt queda publico, y recorrer las 4.3e9 IPv4 contra 8 bytes es cuestion
    de minutos: el hash deja de proteger nada. Se descartan y los nuevos salen de HMAC con el
    salt que vive en los secrets. La guarda en dbo.Meta es para no borrar esos nuevos si alguien
    vuelve a correr el script.
*/

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Meta WHERE K = 'IpHashSaltRotated')
BEGIN
    UPDATE dbo.Video SET SubmitterIpHash = NULL WHERE SubmitterIpHash IS NOT NULL;

    INSERT dbo.Meta (K, V) VALUES ('IpHashSaltRotated', CONVERT(varchar(64), SYSUTCDATETIME(), 126));
END
GO
