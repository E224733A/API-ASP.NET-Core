SET NOCOUNT ON;

DECLARE @DateTournee date = '2026-05-27';
DECLARE @CodeTourneePrefix nvarchar(50) = N'M';
DECLARE @ExecuteDelete nvarchar(10) = N'NON';

SET @ExecuteDelete = UPPER(LTRIM(RTRIM(@ExecuteDelete)));

IF @DateTournee IS NULL
BEGIN
    THROW 50001, 'DateTournee invalide.', 1;
END;

IF @CodeTourneePrefix IS NULL OR LTRIM(RTRIM(@CodeTourneePrefix)) = N''
BEGIN
    THROW 50002, 'CodeTourneePrefix obligatoire.', 1;
END;

PRINT '============================================================';
PRINT 'Nettoyage des donnees de test API Mobile v1.2';
PRINT '============================================================';
PRINT 'DateTournee       : ' + CONVERT(varchar(10), @DateTournee, 23);
PRINT 'CodeTourneePrefix : ' + @CodeTourneePrefix;
PRINT 'ExecuteDelete     : ' + @ExecuteDelete;
PRINT '============================================================';
PRINT '';

IF OBJECT_ID('tempdb..#TourneesTests') IS NOT NULL
    DROP TABLE #TourneesTests;

CREATE TABLE #TourneesTests
(
    IdTourneeMobile int NOT NULL PRIMARY KEY,
    IdSynchronisation nvarchar(100) NULL,
    DateTournee date NOT NULL,
    CodeTournee nvarchar(100) NOT NULL
);

INSERT INTO #TourneesTests
(
    IdTourneeMobile,
    IdSynchronisation,
    DateTournee,
    CodeTournee
)
SELECT
    t.IdTourneeMobile,
    CONVERT(nvarchar(100), t.IdSynchronisation),
    t.DateTournee,
    t.CodeTournee
FROM Mobile_Tournee t
WHERE t.DateTournee = @DateTournee
  AND (
        t.CodeTournee LIKE @CodeTourneePrefix + N'%'
        OR t.CommentaireGlobal LIKE N'%TEST API MOBILE%'
        OR t.NomAppareil LIKE N'%Test PowerShell%'
      );

PRINT '--- PREVIEW : tournees concernees ---';

SELECT
    t.IdTourneeMobile,
    t.IdSynchronisation,
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
    t.StatutSynchronisation,
    t.DateReceptionApi,
    t.NomAppareil,
    t.VersionApplication,
    t.CommentaireGlobal
FROM Mobile_Tournee t
INNER JOIN #TourneesTests tt
    ON tt.IdTourneeMobile = t.IdTourneeMobile
ORDER BY
    t.DateReceptionApi DESC;

PRINT '';
PRINT '--- PREVIEW : volumes qui seront supprimes ---';

SELECT
    'Mobile_TourneeLigneQuantite' AS TableName,
    COUNT(*) AS NombreLignes
FROM Mobile_TourneeLigneQuantite q
INNER JOIN Mobile_TourneeLigne l
    ON l.IdTourneeLigne = q.IdTourneeLigne
INNER JOIN #TourneesTests tt
    ON tt.IdTourneeMobile = l.IdTourneeMobile

UNION ALL

SELECT
    'Mobile_TourneeLigne' AS TableName,
    COUNT(*) AS NombreLignes
FROM Mobile_TourneeLigne l
INNER JOIN #TourneesTests tt
    ON tt.IdTourneeMobile = l.IdTourneeMobile

UNION ALL

SELECT
    'Mobile_Tournee' AS TableName,
    COUNT(*) AS NombreLignes
FROM Mobile_Tournee t
INNER JOIN #TourneesTests tt
    ON tt.IdTourneeMobile = t.IdTourneeMobile;

PRINT '';
PRINT '--- PREVIEW : controle ROLLS_VIDES concerne ---';

SELECT
    t.CodeTournee,
    l.OrdreArret,
    l.IdLigneSource,
    q.CodeArticle,
    q.QuantiteLivreePrevue,
    q.QuantiteLivree,
    q.QuantiteRecuperee
FROM Mobile_Tournee t
INNER JOIN #TourneesTests tt
    ON tt.IdTourneeMobile = t.IdTourneeMobile
INNER JOIN Mobile_TourneeLigne l
    ON l.IdTourneeMobile = t.IdTourneeMobile
INNER JOIN Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = l.IdTourneeLigne
WHERE q.CodeArticle = N'ROLLS_VIDES'
ORDER BY
    t.CodeTournee,
    l.OrdreArret;

PRINT '';

IF @ExecuteDelete <> N'OUI'
BEGIN
    PRINT 'Mode PREVIEW uniquement : aucune suppression effectuee.';
    PRINT 'Pour supprimer reellement, modifier :';
    PRINT 'DECLARE @ExecuteDelete nvarchar(10) = N''OUI'';';
    RETURN;
END;

PRINT 'Suppression reelle demandee.';
PRINT '';

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '--- Suppression Mobile_TourneeLigneQuantite ---';

    DELETE q
    FROM Mobile_TourneeLigneQuantite q
    INNER JOIN Mobile_TourneeLigne l
        ON l.IdTourneeLigne = q.IdTourneeLigne
    INNER JOIN #TourneesTests tt
        ON tt.IdTourneeMobile = l.IdTourneeMobile;

    PRINT 'Lignes supprimees Mobile_TourneeLigneQuantite : ' + CONVERT(varchar(20), @@ROWCOUNT);

    PRINT '--- Suppression Mobile_TourneeLigne ---';

    DELETE l
    FROM Mobile_TourneeLigne l
    INNER JOIN #TourneesTests tt
        ON tt.IdTourneeMobile = l.IdTourneeMobile;

    PRINT 'Lignes supprimees Mobile_TourneeLigne : ' + CONVERT(varchar(20), @@ROWCOUNT);

    PRINT '--- Suppression Mobile_Tournee ---';

    DELETE t
    FROM Mobile_Tournee t
    INNER JOIN #TourneesTests tt
        ON tt.IdTourneeMobile = t.IdTourneeMobile;

    PRINT 'Lignes supprimees Mobile_Tournee : ' + CONVERT(varchar(20), @@ROWCOUNT);

    COMMIT TRANSACTION;

    PRINT '';
    PRINT 'Nettoyage termine avec succes.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '';
    PRINT 'ERREUR pendant le nettoyage. Transaction annulee.';
    PRINT ERROR_MESSAGE();

    THROW;
END CATCH;

PRINT '';
PRINT '--- Verification apres nettoyage ---';

SELECT
    COUNT(*) AS NombreTourneesRestantes
FROM Mobile_Tournee t
WHERE t.DateTournee = @DateTournee
  AND (
        t.CodeTournee LIKE @CodeTourneePrefix + N'%'
        OR t.CommentaireGlobal LIKE N'%TEST API MOBILE%'
        OR t.NomAppareil LIKE N'%Test PowerShell%'
      );

PRINT '';
PRINT '=== Fin nettoyage tests API Mobile v1.2 ===';