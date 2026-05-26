USE [bd_eric];
GO

/* ============================================================
   VERIFICATION CIBLEE PAR DATE - EXPEDITION / MOBILE - BASE V13+

   Modifier uniquement les paramètres ci-dessous.

   @DateTournee :
   - Date métier à contrôler.

   @CodeTournee :
   - NULL = toutes les tournées de la date.
   - N'4006' = une tournée précise.
   - Pour le journal de lot Expédition, le CodeTournee du lot global est
     normalement GLOBAL. Les tournées réelles sont dans
     Mobile_ExpeditionPreparation.CodeTournee.

   Script en lecture seule : aucun INSERT, UPDATE, DELETE, DROP, ALTER.
============================================================ */

SET NOCOUNT ON;

DECLARE @DateTournee DATE = '2026-05-22';
DECLARE @CodeTournee NVARCHAR(50) = NULL;
-- Exemple pour cibler une tournée précise :
-- DECLARE @CodeTournee NVARCHAR(50) = N'4006';

PRINT '============================================================';
PRINT '0. Paramètres de contrôle';
PRINT '============================================================';

SELECT
    @DateTournee AS DateTourneeControlee,
    @CodeTournee AS CodeTourneeControle,
    CASE
        WHEN @CodeTournee IS NULL THEN N'Toutes les tournées de la date'
        ELSE N'Tournée ciblée'
    END AS ModeControle,
    SYSDATETIMEOFFSET() AS DateExecutionSql,
    DB_NAME() AS BaseCourante;

PRINT '============================================================';
PRINT '1. Lots de verrouillage Expédition pour la date';
PRINT '============================================================';

SELECT
    lot.IdLotVerrouillage,
    lot.EmpreintePayload,
    lot.DateTournee,
    lot.CodeTournee AS CodeTourneeLot,
    lot.LibelleTournee AS LibelleLot,
    lot.StatutLot,
    lot.NombrePreparations,
    lot.NombreLignes,
    lot.NombreQuantites,
    lot.AdresseIP,
    lot.NomAppareil,
    lot.VersionApplication,
    lot.MessageRetour,
    lot.DateReceptionApi,
    lot.DateSauvegardeSql,
    lot.DateCreation,
    lot.DateModification
FROM dbo.Mobile_ExpeditionLotVerrouillage lot
WHERE lot.DateTournee = @DateTournee
ORDER BY
    lot.DateCreation DESC,
    lot.StatutLot,
    lot.IdLotVerrouillage;

PRINT '============================================================';
PRINT '2. Préparations verrouillées pour la date';
PRINT '============================================================';

SELECT
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage,
    p.IdLotVerrouillage,
    lot.CodeTournee AS CodeTourneeLot,
    lot.StatutLot,
    lot.NombrePreparations AS NombrePreparationsLot,
    lot.NombreLignes AS NombreLignesLot,
    lot.NombreQuantites AS NombreQuantitesLot,
    lot.DateReceptionApi,
    lot.DateSauvegardeSql,
    p.EmpreintePayload,
    p.DateCreation,
    p.DateModification
FROM dbo.Mobile_ExpeditionPreparation p
LEFT JOIN dbo.Mobile_ExpeditionLotVerrouillage lot
    ON lot.IdLotVerrouillage = p.IdLotVerrouillage
WHERE p.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR p.CodeTournee = @CodeTournee)
ORDER BY
    p.CodeTournee,
    p.DateVerrouillage DESC;

PRINT '============================================================';
PRINT '3. Etat de disponibilité pour le GET mobile';
PRINT '============================================================';

SELECT
    p.DateTournee,
    p.CodeTournee,
    MAX(p.LibelleTournee) AS LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage,
    COUNT(q.IdPreparationExpeditionLigne) AS NombreLignesActives,
    COUNT(DISTINCT q.IdLigneSource) AS NombrePointsOuLignesSources,
    COUNT(DISTINCT q.NumClient) AS NombreClients,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 1 ELSE 0 END) AS NombreQuantitesNonRenseignees,
    SUM(CASE WHEN q.QuantiteLivreePrevue = 0 THEN 1 ELSE 0 END) AS NombreQuantitesZero,
    SUM(CASE WHEN q.QuantiteLivreePrevue > 0 THEN 1 ELSE 0 END) AS NombreQuantitesPositives,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 0 ELSE q.QuantiteLivreePrevue END) AS TotalQuantiteLivreePrevueRenseignee,
    CASE
        WHEN p.EstVerrouille = 1
         AND p.StatutPreparation = N'VERROUILLEE'
         AND COUNT(q.IdPreparationExpeditionLigne) > 0
            THEN N'OK - préparation consommable par le GET mobile'
        WHEN p.EstVerrouille = 1
         AND p.StatutPreparation = N'VERROUILLEE'
         AND COUNT(q.IdPreparationExpeditionLigne) = 0
            THEN N'KO - préparation verrouillée mais sans ligne active'
        ELSE N'KO - préparation non consommable par le GET mobile'
    END AS EtatGetMobile
FROM dbo.Mobile_ExpeditionPreparation p
LEFT JOIN dbo.Mobile_ExpeditionPreparationLigne q
    ON q.IdPreparationExpedition = p.IdPreparationExpedition
   AND q.Actif = 1
WHERE p.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR p.CodeTournee = @CodeTournee)
GROUP BY
    p.DateTournee,
    p.CodeTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage
ORDER BY
    p.CodeTournee;

PRINT '============================================================';
PRINT '4. Synthèse des quantités prévues par tournée et article';
PRINT '============================================================';

SELECT
    p.DateTournee,
    p.CodeTournee,
    MAX(p.LibelleTournee) AS LibelleTournee,
    q.CodeArticle,
    MAX(COALESCE(q.LibelleArticle, a.LibelleArticle)) AS LibelleArticle,
    COUNT_BIG(*) AS NombreLignesArticleActives,
    COUNT_BIG(DISTINCT q.IdLigneSource) AS NombreLignesSourcesDistinctes,
    COUNT_BIG(DISTINCT q.NumClient) AS NombreClientsDistincts,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 1 ELSE 0 END) AS NombreQuantitesNonRenseignees,
    SUM(CASE WHEN q.QuantiteLivreePrevue = 0 THEN 1 ELSE 0 END) AS NombreQuantitesZero,
    SUM(CASE WHEN q.QuantiteLivreePrevue > 0 THEN 1 ELSE 0 END) AS NombreQuantitesPositives,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 0 ELSE q.QuantiteLivreePrevue END) AS TotalQuantiteLivreePrevueRenseignee
FROM dbo.Mobile_ExpeditionPreparation p
INNER JOIN dbo.Mobile_ExpeditionPreparationLigne q
    ON q.IdPreparationExpedition = p.IdPreparationExpedition
LEFT JOIN dbo.Mobile_ArticleSaisissable a
    ON a.CodeArticle = q.CodeArticle
WHERE p.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR p.CodeTournee = @CodeTournee)
  AND p.EstVerrouille = 1
  AND p.StatutPreparation = N'VERROUILLEE'
  AND q.Actif = 1
GROUP BY
    p.DateTournee,
    p.CodeTournee,
    q.CodeArticle
ORDER BY
    p.CodeTournee,
    q.CodeArticle;

PRINT '============================================================';
PRINT '5. Détail des lignes actives injectables dans le GET mobile';
PRINT '============================================================';

SELECT
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage,
    q.IdPreparationExpeditionLigne,
    q.IdLigneSource,
    q.OrdreArret,
    q.NumClient,
    q.NomClient,
    q.NomAffiche,
    q.CodePDL,
    q.DescriptionPDL,
    q.CodeArticle,
    COALESCE(q.LibelleArticle, a.LibelleArticle) AS LibelleArticle,
    q.QuantiteLivreePrevue,
    q.Actif,
    q.DateCreation,
    q.DateModification
FROM dbo.Mobile_ExpeditionPreparation p
INNER JOIN dbo.Mobile_ExpeditionPreparationLigne q
    ON q.IdPreparationExpedition = p.IdPreparationExpedition
LEFT JOIN dbo.Mobile_ArticleSaisissable a
    ON a.CodeArticle = q.CodeArticle
WHERE p.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR p.CodeTournee = @CodeTournee)
  AND p.EstVerrouille = 1
  AND p.StatutPreparation = N'VERROUILLEE'
  AND q.Actif = 1
ORDER BY
    p.CodeTournee,
    q.OrdreArret,
    q.NumClient,
    q.CodePDL,
    q.CodeArticle;

PRINT '============================================================';
PRINT '6. Commentaires exceptionnels actifs utilisés par le mobile';
PRINT '============================================================';

SELECT
    c.IdCommentaireExceptionnel,
    c.DateTournee,
    c.CodeTournee,
    c.IdLigneSource,
    c.NumClient,
    c.CodePDL,
    c.Commentaire,
    c.Actif,
    c.CreePar,
    c.ModifiePar,
    c.DateCreation,
    c.DateModification
FROM dbo.Mobile_CommentaireExceptionnel c
WHERE c.DateTournee = @DateTournee
  AND c.Actif = 1
  AND (
        @CodeTournee IS NULL
        OR c.CodeTournee = @CodeTournee
        OR c.CodeTournee IS NULL
        OR LTRIM(RTRIM(c.CodeTournee)) = N''
      )
ORDER BY
    c.CodeTournee,
    c.IdLigneSource,
    c.NumClient,
    c.CodePDL,
    c.DateCreation DESC;

PRINT '============================================================';
PRINT '7. Contrôle commentaires actifs rattachables aux lignes Expédition';
PRINT '============================================================';

SELECT
    c.IdCommentaireExceptionnel,
    c.DateTournee,
    c.CodeTournee,
    c.IdLigneSource,
    c.NumClient,
    c.CodePDL,
    c.Commentaire,
    CASE
        WHEN q.IdPreparationExpeditionLigne IS NOT NULL THEN N'OK - commentaire rattachable à une ligne active'
        WHEN c.IdLigneSource IS NULL THEN N'A_CONTROLER - commentaire client/PDL sans IdLigneSource'
        ELSE N'A_CONTROLER - aucune ligne active trouvée pour cet IdLigneSource'
    END AS EtatRattachement
FROM dbo.Mobile_CommentaireExceptionnel c
LEFT JOIN dbo.Mobile_ExpeditionPreparation p
    ON p.DateTournee = c.DateTournee
   AND (
        c.CodeTournee = p.CodeTournee
        OR c.CodeTournee IS NULL
        OR LTRIM(RTRIM(c.CodeTournee)) = N''
   )
LEFT JOIN dbo.Mobile_ExpeditionPreparationLigne q
    ON q.IdPreparationExpedition = p.IdPreparationExpedition
   AND q.Actif = 1
   AND (
        (c.IdLigneSource IS NOT NULL AND q.IdLigneSource = c.IdLigneSource)
        OR
        (c.IdLigneSource IS NULL
            AND q.NumClient = c.NumClient
            AND ISNULL(q.CodePDL, N'') = ISNULL(c.CodePDL, N'')
        )
   )
WHERE c.DateTournee = @DateTournee
  AND c.Actif = 1
  AND (
        @CodeTournee IS NULL
        OR c.CodeTournee = @CodeTournee
        OR c.CodeTournee IS NULL
        OR LTRIM(RTRIM(c.CodeTournee)) = N''
      )
ORDER BY
    EtatRattachement,
    c.CodeTournee,
    c.NumClient,
    c.CodePDL;

PRINT '============================================================';
PRINT '8. Anomalies ciblées sur la date';
PRINT '============================================================';

PRINT '8.1 Aucune préparation pour la date / tournée ciblée';
SELECT
    @DateTournee AS DateTournee,
    @CodeTournee AS CodeTournee,
    N'KO - aucune préparation Expédition trouvée' AS Etat
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.Mobile_ExpeditionPreparation p
    WHERE p.DateTournee = @DateTournee
      AND (@CodeTournee IS NULL OR p.CodeTournee = @CodeTournee)
);

PRINT '8.2 Préparations non consommables par le mobile';
SELECT
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage,
    p.IdLotVerrouillage,
    p.DateCreation,
    p.DateModification
FROM dbo.Mobile_ExpeditionPreparation p
WHERE p.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR p.CodeTournee = @CodeTournee)
  AND (
        p.EstVerrouille <> 1
        OR p.StatutPreparation <> N'VERROUILLEE'
        OR p.DateVerrouillage IS NULL
      );

PRINT '8.3 Préparations verrouillées sans ligne active';
SELECT
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    COUNT(q.IdPreparationExpeditionLigne) AS NombreLignesActives
FROM dbo.Mobile_ExpeditionPreparation p
LEFT JOIN dbo.Mobile_ExpeditionPreparationLigne q
    ON q.IdPreparationExpedition = p.IdPreparationExpedition
   AND q.Actif = 1
WHERE p.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR p.CodeTournee = @CodeTournee)
  AND p.EstVerrouille = 1
  AND p.StatutPreparation = N'VERROUILLEE'
GROUP BY
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee
HAVING COUNT(q.IdPreparationExpeditionLigne) = 0
ORDER BY
    p.CodeTournee;

PRINT '8.4 ROLLS_VIDES présent dans les préparations Expédition actives';
SELECT
    p.DateTournee,
    p.CodeTournee,
    q.IdPreparationExpeditionLigne,
    q.IdLigneSource,
    q.NumClient,
    q.CodePDL,
    q.CodeArticle,
    q.QuantiteLivreePrevue,
    q.Actif
FROM dbo.Mobile_ExpeditionPreparation p
INNER JOIN dbo.Mobile_ExpeditionPreparationLigne q
    ON q.IdPreparationExpedition = p.IdPreparationExpedition
WHERE p.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR p.CodeTournee = @CodeTournee)
  AND q.Actif = 1
  AND q.CodeArticle = N'ROLLS_VIDES';

PRINT '8.5 Articles actifs Expédition non autorisés ou inconnus';
SELECT
    p.DateTournee,
    p.CodeTournee,
    q.IdPreparationExpeditionLigne,
    q.IdLigneSource,
    q.NumClient,
    q.CodePDL,
    q.CodeArticle,
    a.EstActif,
    a.EstUtilisableExpedition,
    q.QuantiteLivreePrevue
FROM dbo.Mobile_ExpeditionPreparation p
INNER JOIN dbo.Mobile_ExpeditionPreparationLigne q
    ON q.IdPreparationExpedition = p.IdPreparationExpedition
LEFT JOIN dbo.Mobile_ArticleSaisissable a
    ON a.CodeArticle = q.CodeArticle
WHERE p.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR p.CodeTournee = @CodeTournee)
  AND q.Actif = 1
  AND (
        a.CodeArticle IS NULL
        OR a.EstActif <> 1
        OR a.EstUtilisableExpedition <> 1
      );

PRINT '8.6 Lignes actives avec quantité non renseignée';
SELECT
    p.DateTournee,
    p.CodeTournee,
    q.IdPreparationExpeditionLigne,
    q.IdLigneSource,
    q.OrdreArret,
    q.NumClient,
    q.NomClient,
    q.CodePDL,
    q.CodeArticle,
    q.QuantiteLivreePrevue,
    q.DateCreation,
    q.DateModification
FROM dbo.Mobile_ExpeditionPreparation p
INNER JOIN dbo.Mobile_ExpeditionPreparationLigne q
    ON q.IdPreparationExpedition = p.IdPreparationExpedition
WHERE p.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR p.CodeTournee = @CodeTournee)
  AND q.Actif = 1
  AND q.QuantiteLivreePrevue IS NULL
ORDER BY
    p.CodeTournee,
    q.OrdreArret,
    q.NumClient,
    q.CodePDL,
    q.CodeArticle;

PRINT '8.7 Lots VERROUILLE en double pour la date';
SELECT
    lot.DateTournee,
    lot.CodeTournee AS CodeTourneeLot,
    COUNT(*) AS NombreLotsVerrouilles
FROM dbo.Mobile_ExpeditionLotVerrouillage lot
WHERE lot.DateTournee = @DateTournee
  AND lot.StatutLot = N'VERROUILLE'
GROUP BY
    lot.DateTournee,
    lot.CodeTournee
HAVING COUNT(*) > 1;

PRINT '============================================================';
PRINT '9. Chargements mobile réalisés pour la date';
PRINT '============================================================';

SELECT
    ch.IdChargement,
    ch.SchemaVersion,
    ch.DateTournee,
    ch.CodeTournee,
    ch.LibelleTournee,
    l.CodeLivreur,
    l.NomLivreur,
    ch.DateChargement,
    ch.NombrePointsEnvoyes,
    ch.NomAppareil,
    ch.VersionApplication,
    ch.AdresseIP,
    ch.DateCreation
FROM dbo.Mobile_ChargementTournee ch
INNER JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = ch.IdLivreur
WHERE ch.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR ch.CodeTournee = @CodeTournee)
ORDER BY
    ch.DateChargement DESC,
    ch.IdChargement DESC;

PRINT '============================================================';
PRINT '10. Synchronisations terrain reçues par le mobile pour la date';
PRINT '============================================================';

SELECT
    t.IdTourneeMobile,
    t.SchemaVersion,
    t.IdSynchronisation,
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
    l.CodeLivreur,
    l.NomLivreur,
    t.StatutSynchronisation,
    t.EstVerrouillee,
    t.DateChargementMobile,
    t.DateReceptionApi,
    t.DateEnvoi,
    t.NombrePointsPrevus,
    t.NombrePointsSaisis,
    t.NomAppareil,
    t.VersionApplication,
    t.AdresseIP,
    t.DateCreation,
    t.DateModification
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = t.IdLivreur
WHERE t.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR t.CodeTournee = @CodeTournee)
ORDER BY
    t.DateReceptionApi DESC,
    t.IdTourneeMobile DESC;

PRINT '============================================================';
PRINT '11. Détail lignes terrain synchronisées';
PRINT '============================================================';

SELECT
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    t.StatutSynchronisation,
    tl.IdTourneeLigne,
    tl.IdLigneSource,
    tl.OrdreArret,
    tl.NumClient,
    tl.NomClient,
    tl.CodePDL,
    tl.DescriptionPDL,
    tl.StatutPassage,
    tl.EstValidee,
    tl.HeureValidation,
    tl.CommentaireLivreur,
    tl.CommentaireExceptionnel,
    tl.QuantiteLivree,
    tl.QuantiteReprise
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_TourneeLigne tl
    ON tl.IdTourneeMobile = t.IdTourneeMobile
WHERE t.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR t.CodeTournee = @CodeTournee)
ORDER BY
    t.CodeTournee,
    tl.OrdreArret,
    tl.NumClient,
    tl.CodePDL;

PRINT '============================================================';
PRINT '12. Quantités terrain synchronisées comparées aux quantités prévues';
PRINT '============================================================';

SELECT
    t.DateTournee,
    t.CodeTournee,
    t.StatutSynchronisation,
    q.CodeArticle,
    MAX(COALESCE(q.LibelleArticle, a.LibelleArticle)) AS LibelleArticle,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 0 ELSE q.QuantiteLivreePrevue END) AS TotalLivrePrevuRenseigne,
    SUM(q.QuantiteLivree) AS TotalLivreTerrain,
    SUM(q.QuantiteRecuperee) AS TotalRecupereTerrain,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 1 ELSE 0 END) AS NombrePrevusNonRenseignes,
    COUNT_BIG(*) AS NombreLignesQuantite
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_TourneeLigne tl
    ON tl.IdTourneeMobile = t.IdTourneeMobile
INNER JOIN dbo.Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = tl.IdTourneeLigne
LEFT JOIN dbo.Mobile_ArticleSaisissable a
    ON a.CodeArticle = q.CodeArticle
WHERE t.DateTournee = @DateTournee
  AND (@CodeTournee IS NULL OR t.CodeTournee = @CodeTournee)
GROUP BY
    t.DateTournee,
    t.CodeTournee,
    t.StatutSynchronisation,
    q.CodeArticle
ORDER BY
    t.CodeTournee,
    q.CodeArticle;

PRINT '============================================================';
PRINT '13. Logs API pour la date';
PRINT '============================================================';

SELECT TOP (200)
    log.IdLog,
    log.DateEvenement,
    log.TypeEvenement,
    log.Niveau,
    log.Message,
    log.DetailTechnique,
    log.AdresseIP,
    log.NomAppareil,
    log.VersionApplication,
    log.IdTourneeMobile,
    log.IdLivreur,
    log.IdSynchronisation
FROM dbo.Mobile_LogSynchronisation log
LEFT JOIN dbo.Mobile_Tournee t
    ON t.IdTourneeMobile = log.IdTourneeMobile
WHERE
    (
        CONVERT(DATE, log.DateEvenement) = @DateTournee
        OR t.DateTournee = @DateTournee
    )
  AND (
        @CodeTournee IS NULL
        OR t.CodeTournee = @CodeTournee
        OR log.TypeEvenement IN (
            N'CHARGEMENT_EXPEDITION_PREPARATION',
            N'VERROUILLAGE_EXPEDITION_PREPARATION',
            N'REJEU_EXPEDITION_PREPARATION',
            N'REFUS_EXPEDITION_PREPARATION',
            N'ERREUR_EXPEDITION_PREPARATION',
            N'AUTH_EXPEDITION',
            N'COMMENTAIRE_EXCEPTIONNEL'
        )
      )
ORDER BY
    log.DateEvenement DESC,
    log.IdLog DESC;

PRINT '============================================================';
PRINT 'FIN VERIFICATION CIBLEE PAR DATE';
PRINT '============================================================';
GO
