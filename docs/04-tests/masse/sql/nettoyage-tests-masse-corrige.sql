SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
    Nettoyage optionnel des donnees de test de masse k6 MobileSLI

    Utilisation :
    1. Renseigner les trois variables ci-dessous.
    2. Executer une premiere fois avec @ExecuteDelete = N'NON' pour verifier le volume.
    3. Si le resultat est correct, passer @ExecuteDelete = N'OUI'.

    Exemple pour ton test :
    @DateTournee       = '2026-05-27'
    @CodeTourneePrefix = 'K6115341'
*/

DECLARE @DateTournee date = CONVERT(date, '2026-05-27', 23);
DECLARE @CodeTourneePrefix nvarchar(50) = N'K6115341';
DECLARE @ExecuteDelete nvarchar(10) = N'NON'; -- NON = preview / OUI = suppression reelle

SET @ExecuteDelete = UPPER(LTRIM(RTRIM(@ExecuteDelete)));

PRINT '============================================================';
PRINT 'Nettoyage optionnel des donnees de test de masse';
PRINT '============================================================';
PRINT CONCAT('DateTournee       : ', CONVERT(varchar(10), @DateTournee, 23));
PRINT CONCAT('CodeTourneePrefix : ', @CodeTourneePrefix);
PRINT CONCAT('ExecuteDelete     : ', @ExecuteDelete);
PRINT '============================================================';

IF OBJECT_ID('tempdb..#TourneesK6') IS NOT NULL DROP TABLE #TourneesK6;
IF OBJECT_ID('tempdb..#LignesK6') IS NOT NULL DROP TABLE #LignesK6;

CREATE TABLE #TourneesK6
(
    IdTourneeMobile int NOT NULL PRIMARY KEY,
    IdSynchronisation nvarchar(100) NULL,
    DateTournee date NOT NULL,
    CodeTournee nvarchar(100) NOT NULL
);

CREATE TABLE #LignesK6
(
    IdTourneeLigne int NOT NULL PRIMARY KEY,
    IdTourneeMobile int NOT NULL
);

INSERT INTO #TourneesK6
(
    IdTourneeMobile,
    IdSynchronisation,
    DateTournee,
    CodeTournee
)
SELECT
    t.IdTourneeMobile,
    t.IdSynchronisation,
    t.DateTournee,
    t.CodeTournee
FROM dbo.Mobile_Tournee t
WHERE t.DateTournee = @DateTournee
  AND t.CodeTournee LIKE @CodeTourneePrefix + N'%';

INSERT INTO #LignesK6
(
    IdTourneeLigne,
    IdTourneeMobile
)
SELECT
    l.IdTourneeLigne,
    l.IdTourneeMobile
FROM dbo.Mobile_TourneeLigne l
INNER JOIN #TourneesK6 t
    ON t.IdTourneeMobile = l.IdTourneeMobile;

PRINT 'Apercu des donnees qui correspondent au filtre :';

SELECT
    'TOURNEES_A_SUPPRIMER' AS Controle,
    COUNT(*) AS Nombre
FROM #TourneesK6
UNION ALL
SELECT
    'LIGNES_A_SUPPRIMER' AS Controle,
    COUNT(*) AS Nombre
FROM #LignesK6
UNION ALL
SELECT
    'QUANTITES_A_SUPPRIMER' AS Controle,
    COUNT(*) AS Nombre
FROM dbo.Mobile_TourneeLigneQuantite q
INNER JOIN #LignesK6 l
    ON l.IdTourneeLigne = q.IdTourneeLigne
UNION ALL
SELECT
    'LOGS_A_SUPPRIMER' AS Controle,
    COUNT(*) AS Nombre
FROM dbo.Mobile_LogSynchronisation logSync
WHERE logSync.IdTourneeMobile IN
(
    SELECT t.IdTourneeMobile
    FROM #TourneesK6 t
)
OR logSync.IdSynchronisation IN
(
    SELECT t.IdSynchronisation
    FROM #TourneesK6 t
    WHERE t.IdSynchronisation IS NOT NULL
);

SELECT
    DateTournee,
    CodeTournee,
    IdSynchronisation
FROM #TourneesK6
ORDER BY CodeTournee;

IF @ExecuteDelete <> N'OUI'
BEGIN
    PRINT 'Mode preview uniquement. Aucune donnee supprimee.';
    PRINT 'Pour supprimer reellement, passer @ExecuteDelete = N''OUI'' en haut du script.';
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DELETE q
    FROM dbo.Mobile_TourneeLigneQuantite q
    INNER JOIN #LignesK6 l
        ON l.IdTourneeLigne = q.IdTourneeLigne;

    PRINT CONCAT('Quantites supprimees : ', @@ROWCOUNT);

    DELETE l
    FROM dbo.Mobile_TourneeLigne l
    INNER JOIN #LignesK6 lk6
        ON lk6.IdTourneeLigne = l.IdTourneeLigne;

    PRINT CONCAT('Lignes supprimees : ', @@ROWCOUNT);

    DELETE logSync
    FROM dbo.Mobile_LogSynchronisation logSync
    WHERE logSync.IdTourneeMobile IN
    (
        SELECT t.IdTourneeMobile
        FROM #TourneesK6 t
    )
    OR logSync.IdSynchronisation IN
    (
        SELECT t.IdSynchronisation
        FROM #TourneesK6 t
        WHERE t.IdSynchronisation IS NOT NULL
    );

    PRINT CONCAT('Logs supprimes : ', @@ROWCOUNT);

    DELETE t
    FROM dbo.Mobile_Tournee t
    INNER JOIN #TourneesK6 k6
        ON k6.IdTourneeMobile = t.IdTourneeMobile;

    PRINT CONCAT('Tournees supprimees : ', @@ROWCOUNT);

    COMMIT TRANSACTION;

    PRINT 'Nettoyage termine.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    PRINT 'Erreur pendant le nettoyage. Transaction annulee.';
    PRINT CONCAT('Numero erreur : ', ERROR_NUMBER());
    PRINT CONCAT('Ligne erreur  : ', ERROR_LINE());
    PRINT CONCAT('Message       : ', ERROR_MESSAGE());

    THROW;
END CATCH;
