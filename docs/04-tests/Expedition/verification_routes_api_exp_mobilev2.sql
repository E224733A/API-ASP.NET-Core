/* ============================================================
   Vérification SQL - Tests routes Expédition + mobile
   À adapter avec la date et la tournée testées.
============================================================ */

DECLARE @DateTournee date = '2026-05-07';
DECLARE @CodeTournee nvarchar(50) = N'4006';

PRINT '1. En-tête de préparation verrouillée';
SELECT
    p.IdPreRemplissageTournee,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.EstVerrouille,
    p.DateVerrouillage,
    p.IdLotVerrouillage,
    p.EmpreintePayload,
    p.DateCreation,
    p.DateModification
FROM dbo.Mobile_PreRemplissageTournee p
WHERE p.DateTournee = @DateTournee
  AND p.CodeTournee = @CodeTournee
ORDER BY p.IdPreRemplissageTournee DESC;

PRINT '2. Lots de verrouillage Expédition';
SELECT
    l.IdLotVerrouillage,
    l.EmpreintePayload,
    l.DateTournee,
    l.CodeTournee,
    l.LibelleTournee,
    l.StatutLot,
    l.IdPreRemplissageTournee,
    l.NombreLignes,
    l.NombreQuantites,
    l.AdresseIP,
    l.DateCreation,
    l.DateModification
FROM dbo.Mobile_ExpeditionLotVerrouillage l
WHERE l.DateTournee = @DateTournee
  AND l.CodeTournee = @CodeTournee
ORDER BY l.DateCreation DESC;

PRINT '3. Quantités préparées actives, utilisées par le GET mobile';
SELECT
    p.EstVerrouille,
    q.IdLigneSource,
    q.OrdreArret,
    q.NumClient,
    q.NomClient,
    q.CodePDL,
    q.DescriptionPDL,
    q.CodeArticle,
    q.LibelleArticle,
    q.QuantiteLivreePrevue,
    q.Actif,
    q.DateCreation,
    q.DateModification
FROM dbo.Mobile_PreRemplissageTournee p
INNER JOIN dbo.Mobile_PreRemplissageQuantite q
    ON q.IdPreRemplissageTournee = p.IdPreRemplissageTournee
WHERE p.DateTournee = @DateTournee
  AND p.CodeTournee = @CodeTournee
  AND p.EstVerrouille = 1
  AND q.Actif = 1
ORDER BY q.OrdreArret, q.NumClient, q.CodePDL, q.CodeArticle;

PRINT '4. Commentaires exceptionnels actifs';
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
    c.DateCreation,
    c.DateModification
FROM dbo.Mobile_CommentaireExceptionnel c
WHERE c.DateTournee = @DateTournee
  AND c.CodeTournee = @CodeTournee
  AND c.Actif = 1
ORDER BY c.DateCreation DESC;

PRINT '5. Sécurité : ROLLS_VIDES ne doit pas exister dans les quantités préparées positives';
SELECT
    q.*
FROM dbo.Mobile_PreRemplissageTournee p
INNER JOIN dbo.Mobile_PreRemplissageQuantite q
    ON q.IdPreRemplissageTournee = p.IdPreRemplissageTournee
WHERE p.DateTournee = @DateTournee
  AND p.CodeTournee = @CodeTournee
  AND q.CodeArticle = N'ROLLS_VIDES'
  AND ISNULL(q.QuantiteLivreePrevue, 0) > 0;

PRINT '6. Chargements mobile réalisés après verrouillage';
SELECT TOP (20)
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
    ch.AdresseIP
FROM dbo.Mobile_ChargementTournee ch
INNER JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = ch.IdLivreur
WHERE ch.DateTournee = @DateTournee
  AND ch.CodeTournee = @CodeTournee
ORDER BY ch.DateChargement DESC;
