/*
    SOLO PARA DESARROLLO LOCAL. Los TweetId son inventados: los links no resuelven a nada.
    Esta fuera de la secuencia numerada a proposito para que ningun deploy lo corra.
    Se borra con: DELETE FROM dbo.VideoTag; DELETE FROM dbo.Video; DELETE FROM dbo.Tag;
*/

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

EXEC dbo.PublishVideo @Handle='mariaa__esc',   @TweetId=1900000000000000001, @Title=N'Gato mirando con total indiferencia',        @Tags=N'gato, indiferencia, me da igual';
EXEC dbo.PublishVideo @Handle='cosasdeperros', @TweetId=1900000000000000002, @Title=N'Perro que se da vuelta y se va',             @Tags=N'perro, me voy, cringe';
EXEC dbo.PublishVideo @Handle='memeteca',      @TweetId=1900000000000000003, @Title=N'Señor riéndose y después serio',             @Tags=N'risa, incomodo, cambio';
EXEC dbo.PublishVideo @Handle='reaccionesarg', @TweetId=1900000000000000004, @Title=N'Aplauso lento sarcástico',                    @Tags=N'aplauso, sarcasmo, ironia';
EXEC dbo.PublishVideo @Handle='clipsvarios',   @TweetId=1900000000000000005, @Title=N'Nena diciendo que no con la cabeza',          @Tags=N'no, negacion, nena';
EXEC dbo.PublishVideo @Handle='memeteca',      @TweetId=1900000000000000006, @Title=N'Explosión de fondo mientras camina',          @Tags=N'explosion, me voy, epico';
EXEC dbo.PublishVideo @Handle='futbolclips',   @TweetId=1900000000000000007, @Title=N'Hincha llorando en la tribuna',               @Tags=N'llanto, tristeza, futbol';
EXEC dbo.PublishVideo @Handle='reaccionesarg', @TweetId=1900000000000000008, @Title=N'Tipo mirando a cámara tipo The Office',       @Tags=N'camara, incomodo, the-office';
EXEC dbo.PublishVideo @Handle='gatitosdiarios',@TweetId=1900000000000000009, @Title=N'Gato asustado saltando',                      @Tags=N'gato, susto, salto';
EXEC dbo.PublishVideo @Handle='clipsvarios',   @TweetId=1900000000000000010, @Title=N'Señora revoleando los ojos',                  @Tags=N'ojos, fastidio, señora';
EXEC dbo.PublishVideo @Handle='memeteca',      @TweetId=1900000000000000011, @Title=N'Chico comiendo pochoclo esperando el quilombo', @Tags=N'pochoclo, expectativa, quilombo';
EXEC dbo.PublishVideo @Handle='cosasdeperros', @TweetId=1900000000000000012, @Title=N'Perro cabizbajo caminando bajo la lluvia',    @Tags=N'perro, tristeza, lluvia';
EXEC dbo.PublishVideo @Handle='futbolclips',   @TweetId=1900000000000000013, @Title=N'Festejo exagerado de gol',                    @Tags=N'festejo, gol, euforia';
EXEC dbo.PublishVideo @Handle='gatitosdiarios',@TweetId=1900000000000000014, @Title=N'Gato tirándose al piso dramáticamente',       @Tags=N'gato, drama, caida';
EXEC dbo.PublishVideo @Handle='reaccionesarg', @TweetId=1900000000000000015, @Title=N'Pibe haciendo que no escucha',                @Tags=N'no escucho, ignorar, pibe';
GO
