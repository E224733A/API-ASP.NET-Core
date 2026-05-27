:On Error Exit
:setvar DateTournee "2026-01-01"
:setvar CodeTourneePrefix "K6000000"
:setvar ExecuteDelete "NON"

SET NOCOUNT ON;

DECLARE @DateTournee date = CONVERT(date, '$(DateTournee)', 23);
DECLARE @CodeTourneePrefix nvarchar(50) = N'$(CodeTourneePrefix)';
DECLARE @ExecuteDelete nvarchar(10) = UPPER(N'$(ExecuteDelete)');

PRINT '============================================================';
PRINT 'Nettoyage optionnel des donnees de test de masse';
PRINT '============================================================';
PRINT CONCAT('DateTournee       : ', CONVERT(varchar(10), @DateTournee, 23));
PRINT CONCAT('CodeTourneePrefix : ', @CodeTourneePrefix);
PRINT CONCAT('ExecuteDelete     : ', @ExecuteDelete);
PRINT '============================================================';

;WITH TourneesK6 AS
(
    SELECT t.IdTourneeMobile
    FROM dbo.Mobile_Tournee t
    WHERE t.DateTournee = @DateTournee
      AND t.CodeTournee LIKE @CodeTourneePrefix + N'%'
)
SELECT
    'PREVIEW_TOURNEES_A_SUPPRIMER' AS Controle,
    COUNT(*) AS NombreTournees
FROM TourneesK6;

IF @ExecuteDelete <> N'OUI'
BEGIN
    PRINT 'Mode preview uniquement. Pour supprimer, relancer avec -v ExecuteDelete="OUI".';
    RETURN;
END;

BEGIN TRANSACTION;

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
DELETE q
FROM dbo.Mobile_TourneeLigneQuantite q
INNER JOIN LignesK6 l ON l.IdTourneeLigne = q.IdTourneeLigne;

;WITH TourneesK6 AS
(
    SELECT t.IdTourneeMobile
    FROM dbo.Mobile_Tournee t
    WHERE t.DateTournee = @DateTournee
      AND t.CodeTournee LIKE @CodeTourneePrefix + N'%'
)
DELETE l
FROM dbo.Mobile_TourneeLigne l
INNER JOIN TourneesK6 t ON t.IdTourneeMobile = l.IdTourneeMobile;

DELETE l
FROM dbo.Mobile_LogSynchronisation l
WHERE l.CodeTournee LIKE @CodeTourneePrefix + N'%';

DELETE t
FROM dbo.Mobile_Tournee t
WHERE t.DateTournee = @DateTournee
  AND t.CodeTournee LIKE @CodeTourneePrefix + N'%';

COMMIT TRANSACTION;

PRINT 'Nettoyage termine.';
