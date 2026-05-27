SET NOCOUNT ON;

DECLARE @DateTournee date = CONVERT(date, '$(DateTournee)', 23);

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
    q.CodeArticle,
    q.Libelle,
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
PRINT '=== Fin verification synchronisation mobile v1.2 ===';
