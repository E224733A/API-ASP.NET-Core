USE [bd_eric];
GO

/* ============================================================
   VERIFICATION GENERALE - EXPEDITION / MOBILE - BASE V13+

   Objectif :
   - Vérifier la structure réellement présente.
   - Vérifier les lots de verrouillage Expédition.
   - Vérifier les préparations verrouillées consommables par le GET mobile.
   - Vérifier les lignes / articles / quantités.
   - Vérifier les commentaires exceptionnels.
   - Vérifier les chargements mobile et les synchronisations terrain.
   - Remonter les anomalies principales.

   Script en lecture seule : aucun INSERT, UPDATE, DELETE, DROP, ALTER.
============================================================ */

SET NOCOUNT ON;

PRINT '============================================================';
PRINT '0. Base et contexte SQL';
PRINT '============================================================';

SELECT
    DB_NAME() AS BaseCourante,
    SYSDATETIMEOFFSET() AS DateExecutionSql,
    SUSER_SNAME() AS UtilisateurSql,
    HOST_NAME() AS PosteClient,
    APP_NAME() AS ApplicationClient;

PRINT '============================================================';
PRINT '1. Objets Mobile_* attendus en base v13+';
PRINT '============================================================';

SELECT
    expected.Objet,
    expected.TypeObjet,
    CASE
        WHEN expected.TypeObjet = N'TABLE' AND t.object_id IS NOT NULL THEN N'OK'
        WHEN expected.TypeObjet = N'VUE'   AND v.object_id IS NOT NULL THEN N'OK'
        ELSE N'ABSENT'
    END AS Etat
FROM (
    VALUES
        (N'Mobile_ArticleSaisissable', N'TABLE'),
        (N'Mobile_UtilisateurExpedition', N'TABLE'),
        (N'Mobile_Livreur', N'TABLE'),
        (N'Mobile_ChargementTournee', N'TABLE'),
        (N'Mobile_Tournee', N'TABLE'),
        (N'Mobile_TourneeLigne', N'TABLE'),
        (N'Mobile_TourneeLigneQuantite', N'TABLE'),
        (N'Mobile_ExpeditionLotVerrouillage', N'TABLE'),
        (N'Mobile_ExpeditionPreparation', N'TABLE'),
        (N'Mobile_ExpeditionPreparationLigne', N'TABLE'),
        (N'Mobile_ExpeditionPreparationHistorique', N'TABLE'),
        (N'Mobile_CommentaireExceptionnel', N'TABLE'),
        (N'Mobile_LogSynchronisation', N'TABLE'),
        (N'Mobile_ExportAdmin', N'TABLE'),
        (N'v_Mobile_TourneeQuantitesDetaillees', N'VUE'),
        (N'v_Mobile_TotauxTournee', N'VUE'),
        (N'v_Mobile_ExpeditionPreparationDetail', N'VUE'),
        (N'v_Mobile_ExpeditionPreparationSynthese', N'VUE')
) expected(Objet, TypeObjet)
LEFT JOIN sys.tables t
    ON t.name = expected.Objet
   AND expected.TypeObjet = N'TABLE'
LEFT JOIN sys.views v
    ON v.name = expected.Objet
   AND expected.TypeObjet = N'VUE'
ORDER BY
    expected.TypeObjet,
    expected.Objet;

PRINT '============================================================';
PRINT '2. Anciens objets PreRemplissage qui ne doivent plus être utilisés';
PRINT '============================================================';

SELECT
    old_objects.ObjetAncien,
    CASE
        WHEN o.object_id IS NULL THEN N'OK - absent'
        ELSE N'A_CONTROLER - ancien objet encore présent'
    END AS Etat
FROM (
    VALUES
        (N'Mobile_PreRemplissageTournee'),
        (N'Mobile_PreRemplissageQuantite'),
        (N'Mobile_PreRemplissageHistorique')
) old_objects(ObjetAncien)
LEFT JOIN sys.objects o
    ON o.name = old_objects.ObjetAncien
ORDER BY old_objects.ObjetAncien;

PRINT '============================================================';
PRINT '3. Colonnes critiques attendues sur les tables Expédition';
PRINT '============================================================';

SELECT
    expected.TableName,
    expected.ColumnName,
    CASE
        WHEN c.column_id IS NULL THEN N'ABSENTE'
        ELSE N'OK'
    END AS Etat
FROM (
    VALUES
        (N'Mobile_ExpeditionLotVerrouillage', N'IdLotVerrouillage'),
        (N'Mobile_ExpeditionLotVerrouillage', N'EmpreintePayload'),
        (N'Mobile_ExpeditionLotVerrouillage', N'DateTournee'),
        (N'Mobile_ExpeditionLotVerrouillage', N'CodeTournee'),
        (N'Mobile_ExpeditionLotVerrouillage', N'StatutLot'),
        (N'Mobile_ExpeditionLotVerrouillage', N'NombrePreparations'),
        (N'Mobile_ExpeditionLotVerrouillage', N'NombreLignes'),
        (N'Mobile_ExpeditionLotVerrouillage', N'NombreQuantites'),
        (N'Mobile_ExpeditionLotVerrouillage', N'DateReceptionApi'),
        (N'Mobile_ExpeditionLotVerrouillage', N'DateSauvegardeSql'),
        (N'Mobile_ExpeditionLotVerrouillage', N'MessageRetour'),

        (N'Mobile_ExpeditionPreparation', N'IdPreparationExpedition'),
        (N'Mobile_ExpeditionPreparation', N'DateTournee'),
        (N'Mobile_ExpeditionPreparation', N'CodeTournee'),
        (N'Mobile_ExpeditionPreparation', N'StatutPreparation'),
        (N'Mobile_ExpeditionPreparation', N'EstVerrouille'),
        (N'Mobile_ExpeditionPreparation', N'DateVerrouillage'),
        (N'Mobile_ExpeditionPreparation', N'IdLotVerrouillage'),
        (N'Mobile_ExpeditionPreparation', N'EmpreintePayload'),

        (N'Mobile_ExpeditionPreparationLigne', N'IdPreparationExpeditionLigne'),
        (N'Mobile_ExpeditionPreparationLigne', N'IdPreparationExpedition'),
        (N'Mobile_ExpeditionPreparationLigne', N'IdLigneSource'),
        (N'Mobile_ExpeditionPreparationLigne', N'OrdreArret'),
        (N'Mobile_ExpeditionPreparationLigne', N'NumClient'),
        (N'Mobile_ExpeditionPreparationLigne', N'NomClient'),
        (N'Mobile_ExpeditionPreparationLigne', N'CodePDL'),
        (N'Mobile_ExpeditionPreparationLigne', N'CodeArticle'),
        (N'Mobile_ExpeditionPreparationLigne', N'LibelleArticle'),
        (N'Mobile_ExpeditionPreparationLigne', N'QuantiteLivreePrevue'),
        (N'Mobile_ExpeditionPreparationLigne', N'Actif')
) expected(TableName, ColumnName)
LEFT JOIN sys.tables t
    ON t.name = expected.TableName
LEFT JOIN sys.columns c
    ON c.object_id = t.object_id
   AND c.name = expected.ColumnName
ORDER BY
    expected.TableName,
    expected.ColumnName;

PRINT '============================================================';
PRINT '4. Référentiel articles saisissables';
PRINT '============================================================';

SELECT
    CodeArticle,
    LibelleArticle,
    OrdreAffichage,
    EstActif,
    EstVisibleMobile,
    EstLivrableMobile,
    EstRecuperableMobile,
    EstUtilisableExpedition,
    DateCreation,
    DateModification
FROM dbo.Mobile_ArticleSaisissable
ORDER BY OrdreAffichage, CodeArticle;

PRINT '============================================================';
PRINT '5. Contrainte StatutLot : vérifier que REMPLACE est autorisé';
PRINT '============================================================';

SELECT
    cc.name AS Contrainte,
    OBJECT_NAME(cc.parent_object_id) AS TableName,
    cc.definition AS DefinitionContrainte,
    CASE
        WHEN cc.definition LIKE N'%REMPLACE%' THEN N'OK - REMPLACE autorisé'
        ELSE N'KO - REMPLACE absent : appliquer la migration expedition_lot_remplacement'
    END AS Etat
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Mobile_ExpeditionLotVerrouillage')
  AND cc.name = N'CK_Mobile_ExpeditionLotVerrouillage_StatutLot';

PRINT '============================================================';
PRINT '6. Index critiques Expédition';
PRINT '============================================================';

SELECT
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.is_unique,
    i.has_filter,
    i.filter_definition
FROM sys.indexes i
WHERE OBJECT_NAME(i.object_id) IN (
    N'Mobile_ExpeditionLotVerrouillage',
    N'Mobile_ExpeditionPreparation',
    N'Mobile_ExpeditionPreparationLigne'
)
  AND i.name IS NOT NULL
ORDER BY
    OBJECT_NAME(i.object_id),
    i.name;

PRINT '============================================================';
PRINT '7. Synthèse globale des lots de verrouillage Expédition';
PRINT '============================================================';

SELECT
    DateTournee,
    CodeTournee,
    StatutLot,
    COUNT(*) AS NombreLots,
    SUM(NombrePreparations) AS TotalPreparationsDeclarees,
    SUM(NombreLignes) AS TotalLignesDeclarees,
    SUM(NombreQuantites) AS TotalQuantitesDeclarees,
    MIN(DateCreation) AS PremiereCreation,
    MAX(DateCreation) AS DerniereCreation
FROM dbo.Mobile_ExpeditionLotVerrouillage
GROUP BY
    DateTournee,
    CodeTournee,
    StatutLot
ORDER BY
    DateTournee DESC,
    CodeTournee,
    StatutLot;

PRINT '============================================================';
PRINT '8. Derniers lots reçus';
PRINT '============================================================';

SELECT TOP (50)
    IdLotVerrouillage,
    EmpreintePayload,
    DateTournee,
    CodeTournee,
    LibelleTournee,
    StatutLot,
    NombrePreparations,
    NombreLignes,
    NombreQuantites,
    AdresseIP,
    NomAppareil,
    VersionApplication,
    MessageRetour,
    DateReceptionApi,
    DateSauvegardeSql,
    DateCreation,
    DateModification
FROM dbo.Mobile_ExpeditionLotVerrouillage
ORDER BY
    DateCreation DESC,
    DateTournee DESC;

PRINT '============================================================';
PRINT '9. Synthèse globale des préparations verrouillées';
PRINT '============================================================';

SELECT
    p.DateTournee,
    p.CodeTournee,
    MAX(p.LibelleTournee) AS LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    COUNT(*) AS NombrePreparations,
    COUNT(DISTINCT p.IdLotVerrouillage) AS NombreLotsAssocies,
    MIN(p.DateVerrouillage) AS PremierVerrouillage,
    MAX(p.DateVerrouillage) AS DernierVerrouillage,
    MAX(p.DateModification) AS DerniereModification
FROM dbo.Mobile_ExpeditionPreparation p
GROUP BY
    p.DateTournee,
    p.CodeTournee,
    p.StatutPreparation,
    p.EstVerrouille
ORDER BY
    p.DateTournee DESC,
    p.CodeTournee;

PRINT '============================================================';
PRINT '10. Dernières préparations avec le lot associé';
PRINT '============================================================';

SELECT TOP (100)
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
    lot.NombrePreparations,
    lot.NombreLignes,
    lot.NombreQuantites,
    lot.DateReceptionApi,
    lot.DateSauvegardeSql,
    p.EmpreintePayload,
    p.DateCreation,
    p.DateModification
FROM dbo.Mobile_ExpeditionPreparation p
LEFT JOIN dbo.Mobile_ExpeditionLotVerrouillage lot
    ON lot.IdLotVerrouillage = p.IdLotVerrouillage
ORDER BY
    p.DateTournee DESC,
    p.DateVerrouillage DESC,
    p.CodeTournee;

PRINT '============================================================';
PRINT '11. Synthèse des lignes actives par date / tournée / article';
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
WHERE q.Actif = 1
GROUP BY
    p.DateTournee,
    p.CodeTournee,
    q.CodeArticle
ORDER BY
    p.DateTournee DESC,
    p.CodeTournee,
    q.CodeArticle;

PRINT '============================================================';
PRINT '12. Données réellement consommables par le GET mobile';
PRINT '============================================================';

SELECT TOP (200)
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
WHERE p.EstVerrouille = 1
  AND p.StatutPreparation = N'VERROUILLEE'
  AND q.Actif = 1
ORDER BY
    p.DateTournee DESC,
    p.CodeTournee,
    q.OrdreArret,
    q.NumClient,
    q.CodePDL,
    q.CodeArticle;

PRINT '============================================================';
PRINT '13. Anomalies globales à contrôler';
PRINT '============================================================';

PRINT '13.1 Préparations non consommables par le mobile';
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
WHERE p.EstVerrouille <> 1
   OR p.StatutPreparation <> N'VERROUILLEE'
   OR p.DateVerrouillage IS NULL;

PRINT '13.2 Préparations verrouillées sans ligne active';
SELECT
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    COUNT(q.IdPreparationExpeditionLigne) AS NombreLignesActives
FROM dbo.Mobile_ExpeditionPreparation p
LEFT JOIN dbo.Mobile_ExpeditionPreparationLigne q
    ON q.IdPreparationExpedition = p.IdPreparationExpedition
   AND q.Actif = 1
WHERE p.EstVerrouille = 1
  AND p.StatutPreparation = N'VERROUILLEE'
GROUP BY
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille
HAVING COUNT(q.IdPreparationExpeditionLigne) = 0
ORDER BY
    p.DateTournee DESC,
    p.CodeTournee;

PRINT '13.3 ROLLS_VIDES présent dans les préparations Expédition actives';
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
WHERE q.Actif = 1
  AND q.CodeArticle = N'ROLLS_VIDES';

PRINT '13.4 Articles actifs Expédition non autorisés ou inconnus';
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
WHERE q.Actif = 1
  AND (
        a.CodeArticle IS NULL
        OR a.EstActif <> 1
        OR a.EstUtilisableExpedition <> 1
      );

PRINT '13.5 Lots VERROUILLE en double pour une même date + code lot';
SELECT
    DateTournee,
    CodeTournee,
    COUNT(*) AS NombreLotsVerrouilles
FROM dbo.Mobile_ExpeditionLotVerrouillage
WHERE StatutLot = N'VERROUILLE'
GROUP BY
    DateTournee,
    CodeTournee
HAVING COUNT(*) > 1;

PRINT '13.6 Lots Expédition dont CodeTournee est différent de GLOBAL';
SELECT
    IdLotVerrouillage,
    DateTournee,
    CodeTournee,
    StatutLot,
    NombrePreparations,
    NombreLignes,
    NombreQuantites,
    DateCreation
FROM dbo.Mobile_ExpeditionLotVerrouillage
WHERE CodeTournee <> N'GLOBAL'
ORDER BY
    DateTournee DESC,
    DateCreation DESC;

PRINT '13.7 Lignes actives avec quantité non renseignée';
SELECT TOP (200)
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
WHERE q.Actif = 1
  AND q.QuantiteLivreePrevue IS NULL
ORDER BY
    p.DateTournee DESC,
    p.CodeTournee,
    q.OrdreArret,
    q.NumClient,
    q.CodePDL,
    q.CodeArticle;

PRINT '============================================================';
PRINT '14. Commentaires exceptionnels actifs récents';
PRINT '============================================================';

SELECT TOP (100)
    IdCommentaireExceptionnel,
    DateTournee,
    CodeTournee,
    IdLigneSource,
    NumClient,
    CodePDL,
    Commentaire,
    Actif,
    CreePar,
    ModifiePar,
    DateCreation,
    DateModification
FROM dbo.Mobile_CommentaireExceptionnel
WHERE Actif = 1
ORDER BY
    DateTournee DESC,
    DateCreation DESC;

PRINT '============================================================';
PRINT '15. Derniers chargements mobile';
PRINT '============================================================';

SELECT TOP (100)
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
ORDER BY
    ch.DateChargement DESC,
    ch.IdChargement DESC;

PRINT '============================================================';
PRINT '16. Synthèse des synchronisations mobile reçues';
PRINT '============================================================';

SELECT
    t.DateTournee,
    t.CodeTournee,
    MAX(t.LibelleTournee) AS LibelleTournee,
    t.StatutSynchronisation,
    t.EstVerrouillee,
    COUNT(*) AS NombreSynchronisations,
    MIN(t.DateReceptionApi) AS PremiereReceptionApi,
    MAX(t.DateReceptionApi) AS DerniereReceptionApi,
    SUM(CASE WHEN t.DateEnvoi IS NULL THEN 0 ELSE 1 END) AS NombreAvecDateEnvoi
FROM dbo.Mobile_Tournee t
GROUP BY
    t.DateTournee,
    t.CodeTournee,
    t.StatutSynchronisation,
    t.EstVerrouillee
ORDER BY
    t.DateTournee DESC,
    t.CodeTournee;

PRINT '============================================================';
PRINT '17. Quantités terrain saisies par le mobile';
PRINT '============================================================';

SELECT TOP (200)
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
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
GROUP BY
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
    t.StatutSynchronisation,
    q.CodeArticle
ORDER BY
    t.DateTournee DESC,
    t.CodeTournee,
    q.CodeArticle;

PRINT '============================================================';
PRINT '18. Derniers logs API liés à Expédition / mobile';
PRINT '============================================================';

SELECT TOP (200)
    IdLog,
    DateEvenement,
    TypeEvenement,
    Niveau,
    Message,
    DetailTechnique,
    AdresseIP,
    NomAppareil,
    VersionApplication,
    IdTourneeMobile,
    IdLivreur,
    IdSynchronisation
FROM dbo.Mobile_LogSynchronisation
WHERE TypeEvenement IN (
    N'CHARGEMENT_TOURNEE',
    N'ENVOI_TOURNEE',
    N'ENVOI_REUSSI',
    N'ERREUR_ENVOI',
    N'DOUBLE_ENVOI',
    N'ERREUR_SQL',
    N'VALIDATION_API',
    N'ERREUR_VALIDATION',
    N'CHARGEMENT_EXPEDITION_PREPARATION',
    N'VERROUILLAGE_EXPEDITION_PREPARATION',
    N'REJEU_EXPEDITION_PREPARATION',
    N'REFUS_EXPEDITION_PREPARATION',
    N'ERREUR_EXPEDITION_PREPARATION',
    N'AUTH_EXPEDITION',
    N'COMMENTAIRE_EXCEPTIONNEL'
)
ORDER BY
    DateEvenement DESC,
    IdLog DESC;

PRINT '============================================================';
PRINT 'FIN VERIFICATION GENERALE';
PRINT '============================================================';
GO
