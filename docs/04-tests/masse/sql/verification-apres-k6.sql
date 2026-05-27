:On Error Exit
:setvar DateTournee "2026-01-01"
:setvar CodeTourneePrefix "K6000000"
:setvar ExpectedTournees "20"
:setvar ExpectedLignes "100"
:setvar ExpectedQuantites "400"

SET NOCOUNT ON;

DECLARE @DateTournee date = CONVERT(date, '$(DateTournee)', 23);
DECLARE @CodeTourneePrefix nvarchar(50) = N'$(CodeTourneePrefix)';
DECLARE @ExpectedTournees int = TRY_CONVERT(int, '$(ExpectedTournees)');
DECLARE @ExpectedLignes int = TRY_CONVERT(int, '$(ExpectedLignes)');
DECLARE @ExpectedQuantites int = TRY_CONVERT(int, '$(ExpectedQuantites)');

PRINT '============================================================';
PRINT 'Verification SQL apres test k6 MobileSLI';
PRINT '============================================================';
PRINT CONCAT('DateTournee       : ', CONVERT(varchar(10), @DateTournee, 23));
PRINT CONCAT('CodeTourneePrefix : ', @CodeTourneePrefix);
PRINT CONCAT('ExpectedTournees  : ', @ExpectedTournees);
PRINT CONCAT('ExpectedLignes    : ', @ExpectedLignes);
PRINT CONCAT('ExpectedQuantites : ', @ExpectedQuantites);
PRINT '============================================================';

;WITH TourneesK6 AS
(
    SELECT
        t.IdTourneeMobile,
        t.IdSynchronisation,
        t.DateTournee,
        t.CodeTournee,
        t.LibelleTournee,
        t.StatutSynchronisation,
        t.DateEnvoi,
        t.DateReceptionApi,
        t.NombrePointsPrevus,
        t.NombrePointsSaisis,
        t.NomAppareil,
        t.VersionApplication
    FROM dbo.Mobile_Tournee t
    WHERE t.DateTournee = @DateTournee
      AND t.CodeTournee LIKE @CodeTourneePrefix + N'%'
)
SELECT
    '01_TOURNEES_ENREGISTREES' AS Controle,
    @ExpectedTournees AS Attendu,
    COUNT(*) AS Obtenu,
    CASE WHEN COUNT(*) = @ExpectedTournees THEN 'OK' ELSE 'KO' END AS Resultat
FROM TourneesK6;

;WITH TourneesK6 AS
(
    SELECT t.IdTourneeMobile
    FROM dbo.Mobile_Tournee t
    WHERE t.DateTournee = @DateTournee
      AND t.CodeTournee LIKE @CodeTourneePrefix + N'%'
)
SELECT
    '02_LIGNES_ENREGISTREES' AS Controle,
    @ExpectedLignes AS Attendu,
    COUNT(*) AS Obtenu,
    CASE WHEN COUNT(*) = @ExpectedLignes THEN 'OK' ELSE 'KO' END AS Resultat
FROM dbo.Mobile_TourneeLigne l
INNER JOIN TourneesK6 t ON t.IdTourneeMobile = l.IdTourneeMobile;

;WITH TourneesK6 AS
(
    SELECT t.IdTourneeMobile
    FROM dbo.Mobile_Tournee t
    WHERE t.DateTournee = @DateTournee
      AND t.CodeTournee LIKE @CodeTourneePrefix + N'%'
),
LignesK6 AS
(
    SELECT l.IdTourneeLigne
    FROM dbo.Mobile_TourneeLigne l
    INNER JOIN TourneesK6 t ON t.IdTourneeMobile = l.IdTourneeMobile
)
SELECT
    '03_QUANTITES_ENREGISTREES' AS Controle,
    @ExpectedQuantites AS Attendu,
    COUNT(*) AS Obtenu,
    CASE WHEN COUNT(*) = @ExpectedQuantites THEN 'OK' ELSE 'KO' END AS Resultat
FROM dbo.Mobile_TourneeLigneQuantite q
INNER JOIN LignesK6 l ON l.IdTourneeLigne = q.IdTourneeLigne;

;WITH TourneesK6 AS
(
    SELECT t.IdTourneeMobile, t.StatutSynchronisation
    FROM dbo.Mobile_Tournee t
    WHERE t.DateTournee = @DateTournee
      AND t.CodeTournee LIKE @CodeTourneePrefix + N'%'
)
SELECT
    '04_STATUTS_SYNCHRONISATION' AS Controle,
    StatutSynchronisation,
    COUNT(*) AS Nombre
FROM TourneesK6
GROUP BY StatutSynchronisation
ORDER BY StatutSynchronisation;

;WITH TourneesK6 AS
(
    SELECT t.IdTourneeMobile, t.CodeTournee
    FROM dbo.Mobile_Tournee t
    WHERE t.DateTournee = @DateTournee
      AND t.CodeTournee LIKE @CodeTourneePrefix + N'%'
)
SELECT
    '05_DOUBLONS_CODE_TOURNEE' AS Controle,
    CodeTournee,
    COUNT(*) AS Nombre
FROM TourneesK6
GROUP BY CodeTournee
HAVING COUNT(*) > 1
ORDER BY CodeTournee;

;WITH TourneesK6 AS
(
    SELECT t.IdTourneeMobile
    FROM dbo.Mobile_Tournee t
    WHERE t.DateTournee = @DateTournee
      AND t.CodeTournee LIKE @CodeTourneePrefix + N'%'
),
LignesK6 AS
(
    SELECT l.IdTourneeLigne
    FROM dbo.Mobile_TourneeLigne l
    INNER JOIN TourneesK6 t ON t.IdTourneeMobile = l.IdTourneeMobile
)
SELECT
    '06_TOTAL_PAR_ARTICLE' AS Controle,
    q.CodeArticle,
    SUM(q.QuantiteLivree) AS TotalLivre,
    SUM(q.QuantiteRecuperee) AS TotalRecupere,
    COUNT(*) AS NombreLignesArticle
FROM dbo.Mobile_TourneeLigneQuantite q
INNER JOIN LignesK6 l ON l.IdTourneeLigne = q.IdTourneeLigne
GROUP BY q.CodeArticle
ORDER BY q.CodeArticle;

;WITH TourneesK6 AS
(
    SELECT TOP (20)
        t.IdTourneeMobile,
        t.IdSynchronisation,
        t.DateTournee,
        t.CodeTournee,
        t.StatutSynchronisation,
        t.DateEnvoi,
        t.DateReceptionApi,
        t.NomAppareil,
        t.VersionApplication
    FROM dbo.Mobile_Tournee t
    WHERE t.DateTournee = @DateTournee
      AND t.CodeTournee LIKE @CodeTourneePrefix + N'%'
    ORDER BY t.CodeTournee
)
SELECT
    '07_ECHANTILLON_TOURNEES' AS Controle,
    *
FROM TourneesK6;

SELECT
    '08_LOGS_RECENTS' AS Controle,
    TOP_LOGS.*
FROM
(
    SELECT TOP (30)
        l.DateCreation,
        l.Niveau,
        l.TypeEvenement,
        l.CodeTournee,
        l.IdSynchronisation,
        l.Message
    FROM dbo.Mobile_LogSynchronisation l
    WHERE l.CodeTournee LIKE @CodeTourneePrefix + N'%'
       OR CONVERT(nvarchar(50), l.IdSynchronisation) IN
          (
              SELECT CONVERT(nvarchar(50), t.IdSynchronisation)
              FROM dbo.Mobile_Tournee t
              WHERE t.DateTournee = @DateTournee
                AND t.CodeTournee LIKE @CodeTourneePrefix + N'%'
          )
    ORDER BY l.DateCreation DESC
) AS TOP_LOGS;

PRINT '============================================================';
PRINT 'Fin verification SQL';
PRINT 'Si les controles 01, 02 et 03 sont OK, la sauvegarde de masse est coherente.';
PRINT '============================================================';
