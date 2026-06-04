USE [bd_eric];
GO

/* ============================================================
   NETTOYAGE RUN K6 MOBILE 1.3 - TRAJET CAMION

   Objectif :
   Supprimer les donnees de test k6 d'un run de synchronisation mobile 1.3
   incluant la section trajet/camion.

   Exemple de run :
   - CodeTournee LIKE 'K6141951%'
   - DateTournee = 2026-06-04

   Tables ciblees :
   - dbo.Mobile_TourneeCamion
   - dbo.Mobile_TourneeLigneQuantite
   - dbo.Mobile_TourneeLigne
   - dbo.Mobile_LogSynchronisation
   - dbo.Mobile_Tournee

   Securite :
   - @ExecuteDelete = 0 : apercu uniquement
   - @ExecuteDelete = 1 : suppression reelle

   Utilisation :
   1. Renseigner @DateTournee et @CodeTourneePrefix.
   2. Executer une premiere fois avec @ExecuteDelete = 0.
   3. Verifier les volumes affiches.
   4. Passer @ExecuteDelete = 1 uniquement si le filtre est correct.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DateTournee date = '2026-06-04';
DECLARE @CodeTourneePrefix nvarchar(50) = N'K6141951';
DECLARE @ExecuteDelete bit = 0; -- mettre 1 pour supprimer reellement

IF OBJECT_ID('tempdb..#TourneesK6') IS NOT NULL
    DROP TABLE #TourneesK6;

CREATE TABLE #TourneesK6
(
    IdTourneeMobile bigint NOT NULL PRIMARY KEY,
    CodeTournee nvarchar(50) NOT NULL
);

INSERT INTO #TourneesK6
(
    IdTourneeMobile,
    CodeTournee
)
SELECT
    t.IdTourneeMobile,
    t.CodeTournee
FROM dbo.Mobile_Tournee t
WHERE t.DateTournee = @DateTournee
  AND t.CodeTournee LIKE @CodeTourneePrefix + N'%';

SELECT
    @DateTournee AS DateTourneeCible,
    @CodeTourneePrefix AS PrefixeCodeTournee,
    @ExecuteDelete AS ExecuteDelete;

SELECT
    COUNT(*) AS NombreTourneesCiblees
FROM #TourneesK6;

SELECT
    t.IdTourneeMobile,
    t.CodeTournee,
    mt.SchemaVersion
FROM #TourneesK6 t
INNER JOIN dbo.Mobile_Tournee mt
    ON mt.IdTourneeMobile = t.IdTourneeMobile
ORDER BY
    t.IdTourneeMobile;

SELECT
    'dbo.Mobile_TourneeCamion' AS TableCible,
    COUNT(*) AS NombreLignes
FROM dbo.Mobile_TourneeCamion tc
INNER JOIN #TourneesK6 t
    ON t.IdTourneeMobile = tc.IdTourneeMobile

UNION ALL

SELECT
    'dbo.Mobile_TourneeLigneQuantite' AS TableCible,
    COUNT(*) AS NombreLignes
FROM dbo.Mobile_TourneeLigneQuantite q
INNER JOIN dbo.Mobile_TourneeLigne l
    ON l.IdTourneeLigne = q.IdTourneeLigne
INNER JOIN #TourneesK6 t
    ON t.IdTourneeMobile = l.IdTourneeMobile

UNION ALL

SELECT
    'dbo.Mobile_TourneeLigne' AS TableCible,
    COUNT(*) AS NombreLignes
FROM dbo.Mobile_TourneeLigne l
INNER JOIN #TourneesK6 t
    ON t.IdTourneeMobile = l.IdTourneeMobile

UNION ALL

SELECT
    'dbo.Mobile_LogSynchronisation' AS TableCible,
    COUNT(*) AS NombreLignes
FROM dbo.Mobile_LogSynchronisation logSync
INNER JOIN #TourneesK6 t
    ON t.IdTourneeMobile = logSync.IdTourneeMobile

UNION ALL

SELECT
    'dbo.Mobile_Tournee' AS TableCible,
    COUNT(*) AS NombreLignes
FROM dbo.Mobile_Tournee mt
INNER JOIN #TourneesK6 t
    ON t.IdTourneeMobile = mt.IdTourneeMobile;

IF @ExecuteDelete = 0
BEGIN
    SELECT
        'APERCU UNIQUEMENT - aucune suppression effectuee. Passer @ExecuteDelete a 1 pour supprimer.' AS Diagnostic;
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE q
    FROM dbo.Mobile_TourneeLigneQuantite q
    INNER JOIN dbo.Mobile_TourneeLigne l
        ON l.IdTourneeLigne = q.IdTourneeLigne
    INNER JOIN #TourneesK6 t
        ON t.IdTourneeMobile = l.IdTourneeMobile;

    DELETE l
    FROM dbo.Mobile_TourneeLigne l
    INNER JOIN #TourneesK6 t
        ON t.IdTourneeMobile = l.IdTourneeMobile;

    DELETE tc
    FROM dbo.Mobile_TourneeCamion tc
    INNER JOIN #TourneesK6 t
        ON t.IdTourneeMobile = tc.IdTourneeMobile;

    DELETE logSync
    FROM dbo.Mobile_LogSynchronisation logSync
    INNER JOIN #TourneesK6 t
        ON t.IdTourneeMobile = logSync.IdTourneeMobile;

    DELETE mt
    FROM dbo.Mobile_Tournee mt
    INNER JOIN #TourneesK6 t
        ON t.IdTourneeMobile = mt.IdTourneeMobile;

    COMMIT TRANSACTION;

    SELECT
        'OK - donnees k6 supprimees.' AS Diagnostic,
        @DateTournee AS DateTourneeSupprimee,
        @CodeTourneePrefix AS PrefixeSupprime;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
