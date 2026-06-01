/* ============================================================
   VERIFICATION DU VERROUILLAGE EXPEDITION AUTOMATIQUE 22h35
   A lancer demain matin dans SSMS sur la base API centrale.

   Objectif :
   - vérifier que le verrouillage automatique a fonctionné ;
   - vérifier que DateModification correspond au clic humain
     "Marquer prête pour verrouillage" ;
   - vérifier que toutes les heures sont cohérentes en heure France ;
   - vérifier que ROLLS_VIDES est bien sauvegardé ;
   - afficher un diagnostic métier clair.
   ============================================================ */

SET NOCOUNT ON;

DECLARE @DateTournee date = '2026-06-01';

-- Jour ISO indépendant de la langue SQL Server :
-- 1 = lundi, 2 = mardi, ..., 7 = dimanche
DECLARE @JourIso int =
    (DATEDIFF(DAY, CONVERT(date, '19000101'), @DateTournee) % 7) + 1;

-- Règle métier :
-- - si la tournée est un lundi, elle est verrouillée le vendredi soir ;
-- - sinon, elle est verrouillée la veille au soir.
DECLARE @DateExecutionVerrouillageAttendue date =
    CASE
        WHEN @JourIso = 1 THEN DATEADD(DAY, -3, @DateTournee)
        ELSE DATEADD(DAY, -1, @DateTournee)
    END;

DECLARE @HeureExecutionVerrouillageLocale datetime2(0) =
    DATETIMEFROMPARTS(
        YEAR(@DateExecutionVerrouillageAttendue),
        MONTH(@DateExecutionVerrouillageAttendue),
        DAY(@DateExecutionVerrouillageAttendue),
        22, 35, 0, 0
    );

DECLARE @HeureExecutionVerrouillageParis datetimeoffset(0) =
    CONVERT(datetimeoffset(0), @HeureExecutionVerrouillageLocale AT TIME ZONE 'Romance Standard Time');

DECLARE @DebutVerrouillageAttendu datetimeoffset(0) =
    DATEADD(MINUTE, -10, @HeureExecutionVerrouillageParis);

DECLARE @FinVerrouillageAttendu datetimeoffset(0) =
    DATEADD(MINUTE, 30, @HeureExecutionVerrouillageParis);

PRINT '============================================================';
PRINT 'DATE TOURNEE CONTROLEE';
PRINT '============================================================';

SELECT
    @DateTournee AS DateTourneeControlee,
    @JourIso AS JourIso,
    @DateExecutionVerrouillageAttendue AS DateExecutionVerrouillageAttendue,
    @DebutVerrouillageAttendu AS DebutFenetreVerrouillageAttendue,
    @HeureExecutionVerrouillageParis AS HeureVerrouillageAttendue,
    @FinVerrouillageAttendu AS FinFenetreVerrouillageAttendue;


PRINT '============================================================';
PRINT '1. DERNIER LOT GLOBAL POUR LA DATE TOURNEE';
PRINT '============================================================';

;WITH DernierLot AS
(
    SELECT TOP (1)
        *
    FROM dbo.Mobile_ExpeditionLotVerrouillage
    WHERE DateTournee = @DateTournee
      AND CodeTournee = N'GLOBAL'
    ORDER BY DateReceptionApi DESC
)
SELECT
    IdLotVerrouillage,
    DateTournee,
    CodeTournee,
    StatutLot,
    NombrePreparations,
    NombreLignes,
    NombreQuantites,
    DateReceptionApi,
    DateSauvegardeSql,
    DateCreation,
    DateModification AS DateModificationLot,
    AdresseIP,
    NomAppareil,
    VersionApplication,
    MessageRetour
FROM DernierLot;


PRINT '============================================================';
PRINT '2. DIAGNOSTIC GLOBAL DU LOT';
PRINT '============================================================';

;WITH DernierLot AS
(
    SELECT TOP (1)
        *
    FROM dbo.Mobile_ExpeditionLotVerrouillage
    WHERE DateTournee = @DateTournee
      AND CodeTournee = N'GLOBAL'
    ORDER BY DateReceptionApi DESC
)
SELECT
    CASE
        WHEN NOT EXISTS (SELECT 1 FROM DernierLot)
            THEN N'ERREUR - aucun lot global trouvé pour la date tournée'
        WHEN EXISTS (SELECT 1 FROM DernierLot WHERE StatutLot <> N'VERROUILLE')
            THEN N'ERREUR - le dernier lot global n’est pas VERROUILLE'
        WHEN EXISTS (
            SELECT 1
            FROM DernierLot
            WHERE DateReceptionApi < @DebutVerrouillageAttendu
               OR DateReceptionApi > @FinVerrouillageAttendu
        )
            THEN N'ATTENTION - le lot existe mais il n’a pas été reçu dans la fenêtre attendue autour de 22h35'
        ELSE N'OK - verrouillage automatique 22h35 trouvé'
    END AS DiagnosticLot;


PRINT '============================================================';
PRINT '3. DATE MODIFICATION PAR TOURNEE';
PRINT '============================================================';

;WITH DernierLot AS
(
    SELECT TOP (1)
        *
    FROM dbo.Mobile_ExpeditionLotVerrouillage
    WHERE DateTournee = @DateTournee
      AND CodeTournee = N'GLOBAL'
    ORDER BY DateReceptionApi DESC
)
SELECT
    lot.IdLotVerrouillage,
    lot.DateTournee,
    lot.CodeTournee AS CodeTourneeLot,
    lot.StatutLot,

    lot.DateReceptionApi AS HeureReceptionApi,
    lot.DateSauvegardeSql AS HeureSauvegardeApi,
    lot.DateModification AS DateModificationLot,

    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,

    p.DateVerrouillage AS HeureVerrouillageTechniqueApi,
    p.DateModification AS HeureClicPretVerrouillage,

    DATEDIFF(SECOND, p.DateModification, lot.DateReceptionApi) AS SecondesEntreClicEtVerrouillage,
    DATEDIFF(MINUTE, p.DateModification, lot.DateReceptionApi) AS MinutesEntreClicEtVerrouillage,

    CASE
        WHEN p.DateModification IS NULL
            THEN N'ERREUR - aucune heure de clic prête pour verrouillage enregistrée'
        WHEN p.DateModification > lot.DateReceptionApi
            THEN N'ERREUR - l’heure du clic est après la réception API'
        WHEN DATEPART(TZOFFSET, p.DateModification) <> DATEPART(TZOFFSET, lot.DateReceptionApi)
            THEN N'ERREUR - les heures ne sont pas enregistrées avec le même fuseau horaire'
        WHEN DATEDIFF(SECOND, p.DateModification, lot.DateReceptionApi) < 60
            THEN N'OK TECHNIQUE - l’heure de verrouillage sert à identifier la dernière personne qui a verrouillé'
        ELSE N'OK METIER - l’heure de verrouillage sert à identifier la dernière personne qui a verrouillé'
    END AS Diagnostic
FROM DernierLot lot
INNER JOIN dbo.Mobile_ExpeditionPreparation p
    ON p.IdLotVerrouillage = lot.IdLotVerrouillage
ORDER BY
    p.CodeTournee;


PRINT '============================================================';
PRINT '4. CONTROLE FUSEAU HORAIRE';
PRINT '============================================================';

;WITH DernierLot AS
(
    SELECT TOP (1)
        *
    FROM dbo.Mobile_ExpeditionLotVerrouillage
    WHERE DateTournee = @DateTournee
      AND CodeTournee = N'GLOBAL'
    ORDER BY DateReceptionApi DESC
)
SELECT
    p.CodeTournee,
    lot.DateReceptionApi,
    DATEPART(TZOFFSET, lot.DateReceptionApi) AS OffsetReceptionApiMinutes,
    p.DateModification AS HeureClicPretVerrouillage,
    DATEPART(TZOFFSET, p.DateModification) AS OffsetClicPretMinutes,

    CASE
        WHEN p.DateModification IS NULL
            THEN N'ERREUR - DateModification NULL'
        WHEN DATEPART(TZOFFSET, lot.DateReceptionApi) = DATEPART(TZOFFSET, p.DateModification)
            THEN N'OK - même fuseau horaire'
        ELSE N'ERREUR - fuseau horaire différent entre DateReceptionApi et DateModification'
    END AS DiagnosticFuseauHoraire
FROM DernierLot lot
INNER JOIN dbo.Mobile_ExpeditionPreparation p
    ON p.IdLotVerrouillage = lot.IdLotVerrouillage
ORDER BY p.CodeTournee;


PRINT '============================================================';
PRINT '5. CONTROLE ROLLS_VIDES';
PRINT '============================================================';

;WITH DernierLot AS
(
    SELECT TOP (1)
        IdLotVerrouillage
    FROM dbo.Mobile_ExpeditionLotVerrouillage
    WHERE DateTournee = @DateTournee
      AND CodeTournee = N'GLOBAL'
    ORDER BY DateReceptionApi DESC
)
SELECT
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.DateModification AS HeureClicPretVerrouillage,
    pl.CodeArticle,
    pl.LibelleArticle,
    COUNT(*) AS NombreLignesRollsVides,
    SUM(ISNULL(pl.QuantiteLivreePrevue, 0)) AS TotalRollsVidesPrevus
FROM DernierLot lot
INNER JOIN dbo.Mobile_ExpeditionPreparation p
    ON p.IdLotVerrouillage = lot.IdLotVerrouillage
INNER JOIN dbo.Mobile_ExpeditionPreparationLigne pl
    ON pl.IdPreparationExpedition = p.IdPreparationExpedition
WHERE pl.Actif = 1
  AND pl.CodeArticle = N'ROLLS_VIDES'
GROUP BY
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.DateModification,
    pl.CodeArticle,
    pl.LibelleArticle
ORDER BY
    p.CodeTournee;


PRINT '============================================================';
PRINT '6. DETAILS DES QUANTITES VERROUILLEES';
PRINT '============================================================';

;WITH DernierLot AS
(
    SELECT TOP (1)
        IdLotVerrouillage
    FROM dbo.Mobile_ExpeditionLotVerrouillage
    WHERE DateTournee = @DateTournee
      AND CodeTournee = N'GLOBAL'
    ORDER BY DateReceptionApi DESC
)
SELECT
    p.CodeTournee,
    p.LibelleTournee,
    pl.CodeArticle,
    pl.LibelleArticle,
    COUNT(*) AS NombreLignes,
    SUM(CASE WHEN pl.QuantiteLivreePrevue IS NULL THEN 1 ELSE 0 END) AS NombreQuantitesNulles,
    SUM(ISNULL(pl.QuantiteLivreePrevue, 0)) AS TotalQuantiteLivreePrevue
FROM DernierLot lot
INNER JOIN dbo.Mobile_ExpeditionPreparation p
    ON p.IdLotVerrouillage = lot.IdLotVerrouillage
INNER JOIN dbo.Mobile_ExpeditionPreparationLigne pl
    ON pl.IdPreparationExpedition = p.IdPreparationExpedition
WHERE pl.Actif = 1
GROUP BY
    p.CodeTournee,
    p.LibelleTournee,
    pl.CodeArticle,
    pl.LibelleArticle
ORDER BY
    p.CodeTournee,
    pl.CodeArticle;


PRINT '============================================================';
PRINT '7. DIAGNOSTIC FINAL';
PRINT '============================================================';

;WITH DernierLot AS
(
    SELECT TOP (1)
        *
    FROM dbo.Mobile_ExpeditionLotVerrouillage
    WHERE DateTournee = @DateTournee
      AND CodeTournee = N'GLOBAL'
    ORDER BY DateReceptionApi DESC
),
Preparations AS
(
    SELECT
        p.IdPreparationExpedition,
        p.CodeTournee,
        p.StatutPreparation,
        p.EstVerrouille,
        p.DateModification,
        lot.DateReceptionApi
    FROM DernierLot lot
    INNER JOIN dbo.Mobile_ExpeditionPreparation p
        ON p.IdLotVerrouillage = lot.IdLotVerrouillage
),
RollsVides AS
(
    SELECT
        COUNT(*) AS NombreLignesRollsVides
    FROM DernierLot lot
    INNER JOIN dbo.Mobile_ExpeditionPreparation p
        ON p.IdLotVerrouillage = lot.IdLotVerrouillage
    INNER JOIN dbo.Mobile_ExpeditionPreparationLigne pl
        ON pl.IdPreparationExpedition = p.IdPreparationExpedition
    WHERE pl.Actif = 1
      AND pl.CodeArticle = N'ROLLS_VIDES'
)
SELECT
    CASE
        WHEN NOT EXISTS (SELECT 1 FROM DernierLot)
            THEN N'ERREUR - aucun verrouillage Expédition trouvé'
        WHEN EXISTS (SELECT 1 FROM DernierLot WHERE StatutLot <> N'VERROUILLE')
            THEN N'ERREUR - le dernier lot n’est pas VERROUILLE'
        WHEN NOT EXISTS (SELECT 1 FROM Preparations)
            THEN N'ERREUR - aucune préparation rattachée au dernier lot'
        WHEN EXISTS (SELECT 1 FROM Preparations WHERE StatutPreparation <> N'VERROUILLEE' OR EstVerrouille <> 1)
            THEN N'ERREUR - au moins une préparation n’est pas correctement verrouillée'
        WHEN EXISTS (SELECT 1 FROM Preparations WHERE DateModification IS NULL)
            THEN N'ERREUR - au moins une préparation a DateModification NULL'
        WHEN EXISTS (SELECT 1 FROM Preparations WHERE DateModification > DateReceptionApi)
            THEN N'ERREUR - au moins une DateModification est après la réception API'
        WHEN EXISTS (
            SELECT 1
            FROM Preparations
            WHERE DATEPART(TZOFFSET, DateModification) <> DATEPART(TZOFFSET, DateReceptionApi)
        )
            THEN N'ERREUR - au moins une DateModification n’a pas le même fuseau horaire que DateReceptionApi'
        WHEN (SELECT NombreLignesRollsVides FROM RollsVides) = 0
            THEN N'OK PARTIEL - verrouillage correct, mais aucune ligne ROLLS_VIDES trouvée dans ce lot'
        ELSE N'OK COMPLET - verrouillage automatique, DateModification, fuseau horaire et ROLLS_VIDES sont cohérents'
    END AS DiagnosticFinal;