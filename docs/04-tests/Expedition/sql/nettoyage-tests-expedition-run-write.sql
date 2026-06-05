USE [bd_eric];
GO

/* ============================================================
   NETTOYAGE TESTS API EXPEDITION - RunWriteTests

   Objectif :
   Supprimer les donnees de test creees par :
   docs/04-tests/Expedition/tests/Run-ExpeditionTests.ps1 -RunWriteTests

   Cible :
   - lots dont LibelleTournee commence par :
     'Lot global Expédition TEST-EXPEDITION-'
   - logs dont DetailTechnique contient :
     'IdLotVerrouillageMetier=TEST-EXPEDITION-'

   Tables ciblees :
   - dbo.Mobile_ExpeditionPreparationLigne
   - dbo.Mobile_ExpeditionPreparationHistorique
   - dbo.Mobile_ExpeditionPreparation
   - dbo.Mobile_ExpeditionLotVerrouillage
   - dbo.Mobile_LogSynchronisation

   Securite :
   - @ExecuteDelete = 0 : apercu uniquement
   - @ExecuteDelete = 1 : suppression reelle

   Attention :
   A utiliser uniquement sur une base de developpement/test.
   Ne pas executer sur une base de production sans validation explicite.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ExecuteDelete bit = 0;

IF OBJECT_ID('tempdb..#LotsExpeditionTest') IS NOT NULL
    DROP TABLE #LotsExpeditionTest;

IF OBJECT_ID('tempdb..#PreparationsExpeditionTest') IS NOT NULL
    DROP TABLE #PreparationsExpeditionTest;

CREATE TABLE #LotsExpeditionTest
(
    IdLotVerrouillage uniqueidentifier NOT NULL PRIMARY KEY,
    DateTournee date NOT NULL,
    LibelleTournee nvarchar(255) NULL,
    StatutLot nvarchar(50) NULL
);

CREATE TABLE #PreparationsExpeditionTest
(
    IdPreparationExpedition bigint NOT NULL PRIMARY KEY,
    IdLotVerrouillage uniqueidentifier NOT NULL,
    DateTournee date NOT NULL,
    CodeTournee nvarchar(50) NOT NULL
);

INSERT INTO #LotsExpeditionTest
(
    IdLotVerrouillage,
    DateTournee,
    LibelleTournee,
    StatutLot
)
SELECT
    lot.IdLotVerrouillage,
    lot.DateTournee,
    lot.LibelleTournee,
    lot.StatutLot
FROM dbo.Mobile_ExpeditionLotVerrouillage lot
WHERE lot.LibelleTournee LIKE N'Lot global Expédition TEST-EXPEDITION-%';

INSERT INTO #PreparationsExpeditionTest
(
    IdPreparationExpedition,
    IdLotVerrouillage,
    DateTournee,
    CodeTournee
)
SELECT
    prep.IdPreparationExpedition,
    prep.IdLotVerrouillage,
    prep.DateTournee,
    prep.CodeTournee
FROM dbo.Mobile_ExpeditionPreparation prep
INNER JOIN #LotsExpeditionTest lot
    ON lot.IdLotVerrouillage = prep.IdLotVerrouillage;

SELECT
    @ExecuteDelete AS ExecuteDelete,
    COUNT(*) AS LotsExpeditionTestCibles
FROM #LotsExpeditionTest;

SELECT
    COUNT(*) AS PreparationsExpeditionTestCibles
FROM #PreparationsExpeditionTest;

SELECT
    lot.IdLotVerrouillage,
    lot.DateTournee,
    lot.LibelleTournee,
    lot.StatutLot
FROM #LotsExpeditionTest lot
ORDER BY
    lot.DateTournee DESC,
    lot.LibelleTournee;

SELECT
    'dbo.Mobile_ExpeditionPreparationLigne' AS TableCible,
    COUNT(*) AS NombreLignes
FROM dbo.Mobile_ExpeditionPreparationLigne ligne
INNER JOIN #PreparationsExpeditionTest prep
    ON prep.IdPreparationExpedition = ligne.IdPreparationExpedition

UNION ALL

SELECT
    'dbo.Mobile_ExpeditionPreparationHistorique' AS TableCible,
    COUNT(*) AS NombreLignes
FROM dbo.Mobile_ExpeditionPreparationHistorique hist
INNER JOIN #PreparationsExpeditionTest prep
    ON prep.IdPreparationExpedition = hist.IdPreparationExpedition

UNION ALL

SELECT
    'dbo.Mobile_ExpeditionPreparation' AS TableCible,
    COUNT(*) AS NombreLignes
FROM dbo.Mobile_ExpeditionPreparation prep
INNER JOIN #PreparationsExpeditionTest cible
    ON cible.IdPreparationExpedition = prep.IdPreparationExpedition

UNION ALL

SELECT
    'dbo.Mobile_ExpeditionLotVerrouillage' AS TableCible,
    COUNT(*) AS NombreLignes
FROM dbo.Mobile_ExpeditionLotVerrouillage lot
INNER JOIN #LotsExpeditionTest cible
    ON cible.IdLotVerrouillage = lot.IdLotVerrouillage

UNION ALL

SELECT
    'dbo.Mobile_LogSynchronisation' AS TableCible,
    COUNT(*) AS NombreLignes
FROM dbo.Mobile_LogSynchronisation logSync
WHERE logSync.DetailTechnique LIKE N'%IdLotVerrouillageMetier=TEST-EXPEDITION-%';

IF @ExecuteDelete = 0
BEGIN
    SELECT
        'APERCU UNIQUEMENT - aucune suppression effectuee. Passer @ExecuteDelete a 1 pour supprimer.' AS Diagnostic;
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE ligne
    FROM dbo.Mobile_ExpeditionPreparationLigne ligne
    INNER JOIN #PreparationsExpeditionTest prep
        ON prep.IdPreparationExpedition = ligne.IdPreparationExpedition;

    DELETE hist
    FROM dbo.Mobile_ExpeditionPreparationHistorique hist
    INNER JOIN #PreparationsExpeditionTest prep
        ON prep.IdPreparationExpedition = hist.IdPreparationExpedition;

    DELETE prep
    FROM dbo.Mobile_ExpeditionPreparation prep
    INNER JOIN #PreparationsExpeditionTest cible
        ON cible.IdPreparationExpedition = prep.IdPreparationExpedition;

    DELETE lot
    FROM dbo.Mobile_ExpeditionLotVerrouillage lot
    INNER JOIN #LotsExpeditionTest cible
        ON cible.IdLotVerrouillage = lot.IdLotVerrouillage;

    DELETE logSync
    FROM dbo.Mobile_LogSynchronisation logSync
    WHERE logSync.DetailTechnique LIKE N'%IdLotVerrouillageMetier=TEST-EXPEDITION-%';

    COMMIT TRANSACTION;

    SELECT
        'OK - donnees de tests Expedition supprimees.' AS Diagnostic;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
