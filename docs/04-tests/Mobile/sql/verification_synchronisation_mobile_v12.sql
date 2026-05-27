SET NOCOUNT ON;

DECLARE @DateTourneeTexte nvarchar(50) = N'$(DateTournee)';
DECLARE @DateTournee date;

IF @DateTourneeTexte IS NULL
   OR LTRIM(RTRIM(@DateTourneeTexte)) = N''
   OR @DateTourneeTexte = N'$(DateTournee)'
BEGIN
    SET @DateTournee = CONVERT(date, GETDATE());
END
ELSE
BEGIN
    SET @DateTournee = TRY_CONVERT(date, @DateTourneeTexte, 23);
END;

IF @DateTournee IS NULL
BEGIN
    THROW 50001, 'DateTournee invalide. Format attendu : yyyy-MM-dd. Exemple : 2026-05-27.', 1;
END;

PRINT '=== Verification synchronisation mobile v1.2 ===';
PRINT 'Date tournee testee : ' + CONVERT(varchar(10), @DateTournee, 120);
PRINT '';

PRINT '--- 1. Tournees mobiles de test pour la date ---';

SELECT
    IdTourneeMobile,
    IdSynchronisation,
    DateTournee,
    CodeTournee,
    LibelleTournee,
    StatutSynchronisation,
    DateChargementMobile,
    DateEnvoi,
    DateReceptionApi,
    NombrePointsPrevus,
    NombrePointsSaisis,
    NomAppareil,
    VersionApplication,
    CommentaireGlobal
FROM Mobile_Tournee
WHERE DateTournee = @DateTournee
  AND (
        CommentaireGlobal LIKE '%TEST API MOBILE%'
        OR NomAppareil LIKE '%Test PowerShell%'
        OR CodeTournee LIKE 'M%'
      )
ORDER BY DateReceptionApi DESC;

PRINT '';
PRINT '--- 2. Resume par statut ---';

SELECT
    StatutSynchronisation,
    COUNT(*) AS NombreTournees
FROM Mobile_Tournee
WHERE DateTournee = @DateTournee
  AND (
        CommentaireGlobal LIKE '%TEST API MOBILE%'
        OR NomAppareil LIKE '%Test PowerShell%'
        OR CodeTournee LIKE 'M%'
      )
GROUP BY StatutSynchronisation
ORDER BY StatutSynchronisation;

PRINT '';
PRINT '--- 3. Lignes sauvegardees ---';

SELECT
    t.CodeTournee,
    COUNT(l.IdTourneeLigne) AS NombreLignes
FROM Mobile_Tournee t
LEFT JOIN Mobile_TourneeLigne l
    ON l.IdTourneeMobile = t.IdTourneeMobile
WHERE t.DateTournee = @DateTournee
  AND (
        t.CommentaireGlobal LIKE '%TEST API MOBILE%'
        OR t.NomAppareil LIKE '%Test PowerShell%'
        OR t.CodeTournee LIKE 'M%'
      )
GROUP BY t.CodeTournee
ORDER BY t.CodeTournee;

PRINT '';
PRINT '--- 4. Quantites par article ---';

SELECT
    t.CodeTournee,
    q.CodeArticle,
    SUM(ISNULL(q.QuantiteLivreePrevue, 0)) AS TotalQuantiteLivreePrevue,
    SUM(q.QuantiteLivree) AS TotalQuantiteLivree,
    SUM(q.QuantiteRecuperee) AS TotalQuantiteRecuperee,
    COUNT(*) AS NombreOccurrences
FROM Mobile_Tournee t
INNER JOIN Mobile_TourneeLigne l
    ON l.IdTourneeMobile = t.IdTourneeMobile
INNER JOIN Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = l.IdTourneeLigne
WHERE t.DateTournee = @DateTournee
  AND (
        t.CommentaireGlobal LIKE '%TEST API MOBILE%'
        OR t.NomAppareil LIKE '%Test PowerShell%'
        OR t.CodeTournee LIKE 'M%'
      )
GROUP BY
    t.CodeTournee,
    q.CodeArticle
ORDER BY
    t.CodeTournee,
    q.CodeArticle;

PRINT '';
PRINT '--- 5. Controle ROLLS_VIDES ---';

SELECT
    t.CodeTournee,
    l.OrdreArret,
    l.IdLigneSource,
    q.CodeArticle,
    q.QuantiteLivreePrevue,
    q.QuantiteLivree,
    q.QuantiteRecuperee
FROM Mobile_Tournee t
INNER JOIN Mobile_TourneeLigne l
    ON l.IdTourneeMobile = t.IdTourneeMobile
INNER JOIN Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = l.IdTourneeLigne
WHERE t.DateTournee = @DateTournee
  AND q.CodeArticle = 'ROLLS_VIDES'
  AND (
        t.CommentaireGlobal LIKE '%TEST API MOBILE%'
        OR t.NomAppareil LIKE '%Test PowerShell%'
        OR t.CodeTournee LIKE 'M%'
      )
ORDER BY
    t.CodeTournee,
    l.OrdreArret;

PRINT '';
PRINT '--- 6. Doublons par IdSynchronisation ---';

SELECT
    IdSynchronisation,
    COUNT(*) AS NombreOccurrences
FROM Mobile_Tournee
WHERE DateTournee = @DateTournee
  AND (
        CommentaireGlobal LIKE '%TEST API MOBILE%'
        OR NomAppareil LIKE '%Test PowerShell%'
        OR CodeTournee LIKE 'M%'
      )
GROUP BY IdSynchronisation
HAVING COUNT(*) > 1;

PRINT '';
PRINT '--- 7. Doublons par DateTournee + CodeTournee en ENVOYEE ---';

SELECT
    DateTournee,
    CodeTournee,
    COUNT(*) AS NombreOccurrences
FROM Mobile_Tournee
WHERE DateTournee = @DateTournee
  AND StatutSynchronisation = 'ENVOYEE'
  AND (
        CommentaireGlobal LIKE '%TEST API MOBILE%'
        OR NomAppareil LIKE '%Test PowerShell%'
        OR CodeTournee LIKE 'M%'
      )
GROUP BY
    DateTournee,
    CodeTournee
HAVING COUNT(*) > 1;

PRINT '';
PRINT '--- 8. Controle de coherence lignes / quantites ---';

SELECT
    t.CodeTournee,
    COUNT(DISTINCT l.IdTourneeLigne) AS NombreLignes,
    COUNT(q.CodeArticle) AS NombreQuantites
FROM Mobile_Tournee t
LEFT JOIN Mobile_TourneeLigne l
    ON l.IdTourneeMobile = t.IdTourneeMobile
LEFT JOIN Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = l.IdTourneeLigne
WHERE t.DateTournee = @DateTournee
  AND (
        t.CommentaireGlobal LIKE '%TEST API MOBILE%'
        OR t.NomAppareil LIKE '%Test PowerShell%'
        OR t.CodeTournee LIKE 'M%'
      )
GROUP BY
    t.CodeTournee
ORDER BY
    t.CodeTournee;

PRINT '';
PRINT '--- 9. Synthese generale des tests API Mobile ---';

SELECT
    COUNT(DISTINCT t.IdTourneeMobile) AS NombreTourneesSauvegardees,
    COUNT(DISTINCT l.IdTourneeLigne) AS NombreLignesSauvegardees,
    COUNT(q.CodeArticle) AS NombreQuantitesSauvegardees
FROM Mobile_Tournee t
LEFT JOIN Mobile_TourneeLigne l
    ON l.IdTourneeMobile = t.IdTourneeMobile
LEFT JOIN Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = l.IdTourneeLigne
WHERE t.DateTournee = @DateTournee
  AND (
        t.CommentaireGlobal LIKE '%TEST API MOBILE%'
        OR t.NomAppareil LIKE '%Test PowerShell%'
        OR t.CodeTournee LIKE 'M%'
      );

PRINT '';
PRINT '=== Fin verification synchronisation mobile v1.2 ===';