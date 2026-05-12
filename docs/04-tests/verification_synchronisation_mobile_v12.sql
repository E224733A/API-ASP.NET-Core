/* ============================================================
   VERIFICATION SYNCHRONISATION MOBILE - FORMAT 1.2

   Objectif :
   - Verifier rapidement les donnees inserees apres un test API.
   - Controle oriente nouveau format quantites[].
   - Un seul filtre obligatoire/recommande : @DateTournee.

   Utilisation :
   - Mettre @DateTournee a la date testee.
   - Laisser NULL pour verifier toute la base de test.
============================================================ */

SET NOCOUNT ON;

DECLARE @DateTournee DATE = NULL;
-- Exemple : DECLARE @DateTournee DATE = '2026-04-28';

/* ============================================================
   1. En-tetes de tournee recus
============================================================ */
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
    t.NombrePointsPrevus,
    t.NombrePointsSaisis,
    t.DateChargementMobile,
    t.DateReceptionApi,
    t.DateEnvoi,
    t.NomAppareil,
    t.VersionApplication,
    t.AdresseIP
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = t.IdLivreur
WHERE (@DateTournee IS NULL OR t.DateTournee = @DateTournee)
ORDER BY t.DateReceptionApi DESC, t.IdTourneeMobile DESC;

/* ============================================================
   2. Lignes de tournee recues
============================================================ */
SELECT
    t.DateTournee,
    t.CodeTournee,
    l.CodeLivreur,
    tl.IdTourneeLigne,
    tl.IdLigneSource,
    tl.OrdreArret,
    tl.NumClient,
    tl.NomClient,
    tl.NomAffiche,
    tl.CodePDL,
    tl.DescriptionPDL,
    tl.ZoneDechargement,
    tl.ZoneDechargementAffichee,
    tl.Zone,
    tl.StatutPassage,
    tl.EstValidee,
    tl.HeureValidation,
    tl.CommentaireLivreur,
    tl.PrecisionLivreur,
    tl.CommentaireExceptionnel,
    tl.QuantiteLivree AS TotalLivreCompatibilite,
    tl.QuantiteReprise AS TotalRepriseCompatibilite
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = t.IdLivreur
INNER JOIN dbo.Mobile_TourneeLigne tl
    ON tl.IdTourneeMobile = t.IdTourneeMobile
WHERE (@DateTournee IS NULL OR t.DateTournee = @DateTournee)
ORDER BY t.DateTournee DESC, t.CodeTournee, tl.OrdreArret, tl.NumClient, tl.CodePDL;

/* ============================================================
   3. Quantites detaillees par article
============================================================ */
SELECT
    v.DateTournee,
    v.CodeTournee,
    v.CodeLivreur,
    v.NomLivreur,
    v.OrdreArret,
    v.NumClient,
    v.NomClient,
    v.CodePDL,
    v.CodeArticle,
    v.LibelleArticle,
    v.QuantiteLivreePrevue,
    v.QuantiteLivree,
    v.QuantiteRecuperee,
    v.EcartLivrePrevuReel
FROM dbo.v_Mobile_TourneeQuantitesDetaillees v
WHERE (@DateTournee IS NULL OR v.DateTournee = @DateTournee)
ORDER BY v.DateTournee DESC, v.CodeTournee, v.OrdreArret, v.NumClient, v.CodePDL, v.CodeArticle;

/* ============================================================
   4. Totaux par tournee et article
============================================================ */
SELECT
    v.DateTournee,
    v.CodeTournee,
    v.LibelleTournee,
    v.CodeLivreur,
    v.NomLivreur,
    v.CodeArticle,
    v.LibelleArticle,
    v.TotalLivrePrevuRenseigne,
    v.TotalLivre,
    v.TotalRecupere,
    v.TotalEcartSurValeursRenseignees,
    v.NombreValeursPrevuesNonRenseignees,
    v.NombreLignesQuantite
FROM dbo.v_Mobile_TotauxTournee v
WHERE (@DateTournee IS NULL OR v.DateTournee = @DateTournee)
ORDER BY v.DateTournee DESC, v.CodeTournee, v.CodeArticle;

/* ============================================================
   5. Anomalies fonctionnelles a surveiller
============================================================ */
SELECT
    'A_FAIRE_DANS_TOURNEE_ENVOYEE' AS TypeAnomalie,
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    tl.IdTourneeLigne,
    tl.IdLigneSource,
    tl.NumClient,
    tl.StatutPassage,
    tl.CommentaireLivreur
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_TourneeLigne tl
    ON tl.IdTourneeMobile = t.IdTourneeMobile
WHERE (@DateTournee IS NULL OR t.DateTournee = @DateTournee)
  AND t.StatutSynchronisation = N'ENVOYEE'
  AND tl.StatutPassage = N'A_FAIRE'

UNION ALL

SELECT
    'NON_FAIT_OU_ANOMALIE_SANS_COMMENTAIRE' AS TypeAnomalie,
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    tl.IdTourneeLigne,
    tl.IdLigneSource,
    tl.NumClient,
    tl.StatutPassage,
    tl.CommentaireLivreur
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_TourneeLigne tl
    ON tl.IdTourneeMobile = t.IdTourneeMobile
WHERE (@DateTournee IS NULL OR t.DateTournee = @DateTournee)
  AND tl.StatutPassage IN (N'NON_FAIT', N'ANOMALIE')
  AND (tl.CommentaireLivreur IS NULL OR LEN(LTRIM(RTRIM(tl.CommentaireLivreur))) = 0)

UNION ALL

SELECT
    'LIGNE_VALIDEE_SANS_HEURE' AS TypeAnomalie,
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    tl.IdTourneeLigne,
    tl.IdLigneSource,
    tl.NumClient,
    tl.StatutPassage,
    tl.CommentaireLivreur
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_TourneeLigne tl
    ON tl.IdTourneeMobile = t.IdTourneeMobile
WHERE (@DateTournee IS NULL OR t.DateTournee = @DateTournee)
  AND tl.EstValidee = 1
  AND tl.HeureValidation IS NULL;

/* ============================================================
   6. Logs de synchronisation
============================================================ */
SELECT
    log.IdLog,
    log.DateEvenement,
    log.TypeEvenement,
    log.Niveau,
    log.Message,
    log.DetailTechnique,
    log.IdTourneeMobile,
    log.IdLivreur,
    log.IdSynchronisation,
    log.AdresseIP,
    log.NomAppareil,
    log.VersionApplication
FROM dbo.Mobile_LogSynchronisation log
LEFT JOIN dbo.Mobile_Tournee t
    ON t.IdTourneeMobile = log.IdTourneeMobile
WHERE (@DateTournee IS NULL OR t.DateTournee = @DateTournee OR t.IdTourneeMobile IS NULL)
ORDER BY log.DateEvenement DESC, log.IdLog DESC;

/* ============================================================
   7. Recherche de doublons techniques et metier
============================================================ */
SELECT
    'DOUBLON_ID_SYNCHRONISATION' AS TypeControle,
    CAST(IdSynchronisation AS NVARCHAR(100)) AS Cle,
    COUNT(*) AS Nombre
FROM dbo.Mobile_Tournee
WHERE (@DateTournee IS NULL OR DateTournee = @DateTournee)
GROUP BY IdSynchronisation
HAVING COUNT(*) > 1

UNION ALL

SELECT
    'DOUBLON_ENVOI_TOURNEE' AS TypeControle,
    CONCAT(CONVERT(NVARCHAR(10), DateTournee, 120), N'|', CodeTournee, N'|', IdLivreur) AS Cle,
    COUNT(*) AS Nombre
FROM dbo.Mobile_Tournee
WHERE (@DateTournee IS NULL OR DateTournee = @DateTournee)
  AND StatutSynchronisation = N'ENVOYEE'
GROUP BY DateTournee, CodeTournee, IdLivreur
HAVING COUNT(*) > 1

UNION ALL

SELECT
    'DOUBLON_ID_LIGNE_SOURCE' AS TypeControle,
    CONCAT(CAST(IdTourneeMobile AS NVARCHAR(30)), N'|', IdLigneSource) AS Cle,
    COUNT(*) AS Nombre
FROM dbo.Mobile_TourneeLigne
GROUP BY IdTourneeMobile, IdLigneSource
HAVING COUNT(*) > 1

UNION ALL

SELECT
    'DOUBLON_QUANTITE_ARTICLE' AS TypeControle,
    CONCAT(CAST(IdTourneeLigne AS NVARCHAR(30)), N'|', CodeArticle) AS Cle,
    COUNT(*) AS Nombre
FROM dbo.Mobile_TourneeLigneQuantite
GROUP BY IdTourneeLigne, CodeArticle
HAVING COUNT(*) > 1;

/* ============================================================
   8. Verification des colonnes importantes du format 1.2
============================================================ */
SELECT
    c.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME IN (
    'Mobile_Tournee',
    'Mobile_TourneeLigne',
    'Mobile_TourneeLigneQuantite',
    'Mobile_PreRemplissageTournee',
    'Mobile_PreRemplissageQuantite',
    'Mobile_PreRemplissageHistorique',
    'Mobile_CommentaireExceptionnel',
    'Mobile_UtilisateurExpedition',
    'Mobile_ArticleSaisissable'
)
AND c.COLUMN_NAME IN (
    'SchemaVersion',
    'QuantiteLivreePrevue',
    'QuantiteLivree',
    'QuantiteRecuperee',
    'CommentaireExceptionnel',
    'ZoneDechargementAffichee',
    'MotDePasseHash',
    'IdLigneSource'
)
ORDER BY c.TABLE_NAME, c.COLUMN_NAME;

/* ============================================================
   9. Verification des index importants
============================================================ */
SELECT
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.is_unique,
    i.has_filter,
    i.filter_definition
FROM sys.indexes i
WHERE i.name IN (
    'UQ_Mobile_Tournee_IdSynchronisation',
    'UX_Mobile_Tournee_EnvoiUnique',
    'UX_Mobile_TourneeLigne_IdLigneSource',
    'UX_Mobile_TourneeLigneQuantite_Article',
    'UX_Mobile_PreRemplissageQuantite_LigneArticle_Actif',
    'UX_Mobile_CommentaireExceptionnel_Actif'
)
ORDER BY TableName, IndexName;
