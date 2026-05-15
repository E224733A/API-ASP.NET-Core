/* ============================================================
   Vérification API / BD - Préparation Expédition verrouillée
   Objectif : vérifier que le mobile ne reçoit que les préparations
   verrouillées et que les données attendues sont bien présentes.
============================================================ */

DECLARE @DateTournee date = '2026-05-07';
DECLARE @CodeTournee nvarchar(50) = N'4006';

------------------------------------------------------------
-- 1. Vérifier le référentiel articles
------------------------------------------------------------
SELECT
    CodeArticle,
    LibelleArticle,
    OrdreAffichage,
    EstActif,
    EstVisibleMobile
FROM dbo.Mobile_ArticleSaisissable
WHERE CodeArticle IN (N'ROLLS', N'ROLLS_VIDES', N'TAPIS', N'SACS')
ORDER BY OrdreAffichage;

------------------------------------------------------------
-- 2. Vérifier les lots Expédition reçus
------------------------------------------------------------
SELECT
    IdLotVerrouillage,
    EmpreintePayload,
    DateTournee,
    CodeTournee,
    LibelleTournee,
    StatutLot,
    NombreLignes,
    NombreQuantites,
    IdPreRemplissageTournee,
    AdresseIP,
    DateCreation,
    DateModification
FROM dbo.Mobile_ExpeditionLotVerrouillage
WHERE DateTournee = @DateTournee
  AND CodeTournee = @CodeTournee
ORDER BY DateCreation DESC;

------------------------------------------------------------
-- 3. Vérifier l'en-tête de préparation verrouillée
------------------------------------------------------------
SELECT
    IdPreRemplissageTournee,
    DateTournee,
    CodeTournee,
    LibelleTournee,
    EstVerrouille,
    DateVerrouillage,
    IdLotVerrouillage,
    EmpreintePayload,
    DateCreation,
    DateModification
FROM dbo.Mobile_PreRemplissageTournee
WHERE DateTournee = @DateTournee
  AND CodeTournee = @CodeTournee;

------------------------------------------------------------
-- 4. Vérifier les quantités qui pourront être injectées au GET mobile
-- IMPORTANT : la requête reprend le filtre obligatoire p.EstVerrouille = 1.
------------------------------------------------------------
SELECT
    p.IdPreRemplissageTournee,
    p.EstVerrouille,
    q.IdLigneSource,
    q.OrdreArret,
    q.NumClient,
    q.CodePDL,
    q.CodeArticle,
    q.LibelleArticle,
    q.QuantiteLivreePrevue,
    q.Actif
FROM dbo.Mobile_PreRemplissageTournee p
INNER JOIN dbo.Mobile_PreRemplissageQuantite q
    ON q.IdPreRemplissageTournee = p.IdPreRemplissageTournee
WHERE p.DateTournee = @DateTournee
  AND p.CodeTournee = @CodeTournee
  AND p.EstVerrouille = 1
  AND q.Actif = 1
ORDER BY q.OrdreArret, q.IdLigneSource, q.CodeArticle;

------------------------------------------------------------
-- 5. Vérifier qu'aucun ROLLS_VIDES positif n'a été préparé côté Expédition
------------------------------------------------------------
SELECT
    p.DateTournee,
    p.CodeTournee,
    q.IdLigneSource,
    q.CodeArticle,
    q.QuantiteLivreePrevue
FROM dbo.Mobile_PreRemplissageTournee p
INNER JOIN dbo.Mobile_PreRemplissageQuantite q
    ON q.IdPreRemplissageTournee = p.IdPreRemplissageTournee
WHERE q.CodeArticle = N'ROLLS_VIDES'
  AND ISNULL(q.QuantiteLivreePrevue, 0) > 0;

------------------------------------------------------------
-- 6. Vérifier les commentaires exceptionnels séparés des instructions ABSSolute
------------------------------------------------------------
SELECT
    DateTournee,
    CodeTournee,
    IdLigneSource,
    NumClient,
    CodePDL,
    Commentaire,
    Actif,
    DateCreation,
    DateModification
FROM dbo.Mobile_CommentaireExceptionnel
WHERE DateTournee = @DateTournee
  AND CodeTournee = @CodeTournee
  AND Actif = 1
ORDER BY IdLigneSource, NumClient, CodePDL;

------------------------------------------------------------
-- 7. Résumé attendu pour validation rapide
------------------------------------------------------------
SELECT
    CASE
        WHEN EXISTS (
            SELECT 1
            FROM dbo.Mobile_PreRemplissageTournee
            WHERE DateTournee = @DateTournee
              AND CodeTournee = @CodeTournee
              AND EstVerrouille = 1
        ) THEN N'OK - préparation verrouillée disponible pour le GET mobile'
        ELSE N'KO - aucune préparation verrouillée disponible pour le GET mobile'
    END AS EtatPreparationMobile;
