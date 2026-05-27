SET NOCOUNT ON;

DECLARE @RunId nvarchar(80) = N'20260527115341';
DECLARE @DateTournee date = CONVERT(date, N'2026-05-27', 23);
DECLARE @CodeTourneePrefix nvarchar(80) = N'K6115341';
DECLARE @SynchronisationsAttendues int = 20;
DECLARE @LignesAttendues int = 100;
DECLARE @QuantitesAttendues int = 400;

IF OBJECT_ID(N'dbo.Mobile_Tournee', N'U') IS NULL
    THROW 51000, 'Table dbo.Mobile_Tournee introuvable.', 1;

IF OBJECT_ID(N'dbo.Mobile_TourneeLigne', N'U') IS NULL
    THROW 51001, 'Table dbo.Mobile_TourneeLigne introuvable.', 1;

IF OBJECT_ID(N'dbo.Mobile_TourneeLigneQuantite', N'U') IS NULL
    THROW 51002, 'Table dbo.Mobile_TourneeLigneQuantite introuvable.', 1;

IF COL_LENGTH('dbo.Mobile_Tournee', 'IdTourneeMobile') IS NULL
    THROW 51003, 'Colonne dbo.Mobile_Tournee.IdTourneeMobile introuvable.', 1;

IF COL_LENGTH('dbo.Mobile_Tournee', 'DateTournee') IS NULL
    THROW 51004, 'Colonne dbo.Mobile_Tournee.DateTournee introuvable.', 1;

IF COL_LENGTH('dbo.Mobile_Tournee', 'CodeTournee') IS NULL
    THROW 51005, 'Colonne dbo.Mobile_Tournee.CodeTournee introuvable.', 1;

IF COL_LENGTH('dbo.Mobile_TourneeLigne', 'IdTourneeMobile') IS NULL
    THROW 51006, 'Colonne dbo.Mobile_TourneeLigne.IdTourneeMobile introuvable.', 1;

IF COL_LENGTH('dbo.Mobile_TourneeLigne', 'IdTourneeLigne') IS NULL
    THROW 51007, 'Colonne dbo.Mobile_TourneeLigne.IdTourneeLigne introuvable.', 1;

IF COL_LENGTH('dbo.Mobile_TourneeLigneQuantite', 'IdTourneeLigne') IS NULL
    THROW 51008, 'Colonne dbo.Mobile_TourneeLigneQuantite.IdTourneeLigne introuvable.', 1;

SELECT *
INTO #ToursMasse
FROM dbo.Mobile_Tournee
WHERE DateTournee = @DateTournee
  AND CodeTournee LIKE @CodeTourneePrefix + N'%';

DECLARE @Tours int = 0;
DECLARE @Lignes int = 0;
DECLARE @Quantites int = 0;
DECLARE @DoublonsIdSynchronisation int = 0;
DECLARE @DoublonsTournee int = 0;
DECLARE @Logs int = NULL;

SELECT @Tours = COUNT(*)
FROM #ToursMasse;

SELECT @Lignes = COUNT(*)
FROM dbo.Mobile_TourneeLigne l
INNER JOIN #ToursMasse t ON t.IdTourneeMobile = l.IdTourneeMobile;

SELECT @Quantites = COUNT(*)
FROM dbo.Mobile_TourneeLigneQuantite q
INNER JOIN dbo.Mobile_TourneeLigne l ON l.IdTourneeLigne = q.IdTourneeLigne
INNER JOIN #ToursMasse t ON t.IdTourneeMobile = l.IdTourneeMobile;

IF COL_LENGTH('dbo.Mobile_Tournee', 'IdSynchronisation') IS NOT NULL
BEGIN
    EXEC sp_executesql
        N'SELECT @OutValue = COUNT(*)
          FROM (
              SELECT IdSynchronisation
              FROM #ToursMasse
              WHERE IdSynchronisation IS NOT NULL
              GROUP BY IdSynchronisation
              HAVING COUNT(*) > 1
          ) d;',
        N'@OutValue int OUTPUT',
        @OutValue = @DoublonsIdSynchronisation OUTPUT;
END;

SELECT @DoublonsTournee = COUNT(*)
FROM (
    SELECT DateTournee, CodeTournee
    FROM #ToursMasse
    GROUP BY DateTournee, CodeTournee
    HAVING COUNT(*) > 1
) d;

IF OBJECT_ID(N'dbo.Mobile_LogSynchronisation', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Mobile_LogSynchronisation', 'IdTourneeMobile') IS NOT NULL
    BEGIN
        EXEC sp_executesql
            N'SELECT @OutLogs = COUNT(*)
              FROM dbo.Mobile_LogSynchronisation logSync
              WHERE EXISTS (
                  SELECT 1
                  FROM #ToursMasse t
                  WHERE t.IdTourneeMobile = logSync.IdTourneeMobile
              );',
            N'@OutLogs int OUTPUT',
            @OutLogs = @Logs OUTPUT;
    END
    ELSE IF COL_LENGTH('dbo.Mobile_LogSynchronisation', 'IdSynchronisation') IS NOT NULL
         AND COL_LENGTH('dbo.Mobile_Tournee', 'IdSynchronisation') IS NOT NULL
    BEGIN
        EXEC sp_executesql
            N'SELECT @OutLogs = COUNT(*)
              FROM dbo.Mobile_LogSynchronisation logSync
              WHERE EXISTS (
                  SELECT 1
                  FROM #ToursMasse t
                  WHERE t.IdSynchronisation = logSync.IdSynchronisation
              );',
            N'@OutLogs int OUTPUT',
            @OutLogs = @Logs OUTPUT;
    END
END;

SELECT
    @RunId AS RunId,
    @DateTournee AS DateTournee,
    @CodeTourneePrefix AS CodeTourneePrefix,
    @SynchronisationsAttendues AS SynchronisationsAttendues,
    @LignesAttendues AS LignesAttendues,
    @QuantitesAttendues AS QuantitesAttendues;

SELECT
    Controle,
    Attendu,
    Obtenu,
    CASE WHEN Conforme = 1 THEN 'OK' ELSE 'ECART' END AS Statut
FROM (
    SELECT 'Nombre de tournees' AS Controle, @SynchronisationsAttendues AS Attendu, @Tours AS Obtenu, IIF(@Tours = @SynchronisationsAttendues, 1, 0) AS Conforme
    UNION ALL
    SELECT 'Nombre de lignes', @LignesAttendues, @Lignes, IIF(@Lignes = @LignesAttendues, 1, 0)
    UNION ALL
    SELECT 'Nombre de quantites', @QuantitesAttendues, @Quantites, IIF(@Quantites = @QuantitesAttendues, 1, 0)
    UNION ALL
    SELECT 'Doublons IdSynchronisation', 0, @DoublonsIdSynchronisation, IIF(@DoublonsIdSynchronisation = 0, 1, 0)
    UNION ALL
    SELECT 'Doublons DateTournee + CodeTournee', 0, @DoublonsTournee, IIF(@DoublonsTournee = 0, 1, 0)
) r;

SELECT
    'Logs lies au test' AS Controle,
    @Logs AS NombreLogs;

SELECT TOP (50)
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    CASE WHEN COL_LENGTH('dbo.Mobile_Tournee', 'StatutSynchronisation') IS NOT NULL THEN CONVERT(nvarchar(80), t.StatutSynchronisation) ELSE NULL END AS StatutSynchronisation,
    COUNT(DISTINCT l.IdTourneeLigne) AS NombreLignes,
    COUNT(q.IdTourneeLigne) AS NombreQuantites
FROM #ToursMasse t
LEFT JOIN dbo.Mobile_TourneeLigne l ON l.IdTourneeMobile = t.IdTourneeMobile
LEFT JOIN dbo.Mobile_TourneeLigneQuantite q ON q.IdTourneeLigne = l.IdTourneeLigne
GROUP BY
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    CASE WHEN COL_LENGTH('dbo.Mobile_Tournee', 'StatutSynchronisation') IS NOT NULL THEN CONVERT(nvarchar(80), t.StatutSynchronisation) ELSE NULL END
ORDER BY t.CodeTournee;
