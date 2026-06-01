/* ============================================================
   NETTOYAGE CIBLE DES DONNEES DE TEST - MobileSLI

   Version sans SQLCMD.
   Compatible avec une requete SQL classique dans SSMS.

   Objectif :
   - Supprimer uniquement les donnees de test identifiees par :
       1) DateTournee
       2) Prefixe de CodeTournee
   - Ne pas toucher aux vues/tables ABSSolute.
   - Ne pas supprimer les referentiels :
       - Mobile_Livreur
       - Mobile_ArticleSaisissable
   - Laisser @ExecuteDelete = 'NON' pour verifier avant suppression.
   - Passer @ExecuteDelete = 'OUI' uniquement apres verification.

   Exemple :
   - DateTournee = 2026-01-01
   - CodeTourneePrefix = K6000000
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* ============================================================
   PARAMETRES A MODIFIER
   ============================================================ */

DECLARE @DateTournee date = '2026-01-01';
DECLARE @CodeTourneePrefix nvarchar(50) = N'K6000000';
DECLARE @ExecuteDelete nvarchar(10) = N'NON'; -- NON = simulation, OUI = suppression reelle
DECLARE @InclureLotsGlobaux nvarchar(10) = N'NON'; -- OUI uniquement si tu veux aussi supprimer le lot GLOBAL de cette date

/* ============================================================
   CONTROLES
   ============================================================ */

SET @ExecuteDelete = UPPER(@ExecuteDelete);
SET @InclureLotsGlobaux = UPPER(@InclureLotsGlobaux);

IF @ExecuteDelete NOT IN (N'OUI', N'NON')
    THROW 50001, 'ExecuteDelete doit valoir OUI ou NON.', 1;

IF @InclureLotsGlobaux NOT IN (N'OUI', N'NON')
    THROW 50002, 'InclureLotsGlobaux doit valoir OUI ou NON.', 1;

PRINT '============================================================';
PRINT 'Nettoyage cible des donnees de test MobileSLI';
PRINT '============================================================';
PRINT CONCAT('DateTournee        : ', CONVERT(varchar(10), @DateTournee, 23));
PRINT CONCAT('CodeTourneePrefix  : ', @CodeTourneePrefix);
PRINT CONCAT('ExecuteDelete      : ', @ExecuteDelete);
PRINT CONCAT('InclureLotsGlobaux : ', @InclureLotsGlobaux);
PRINT '============================================================';

/* ============================================================
   TABLES TEMPORAIRES
   ============================================================ */

IF OBJECT_ID('tempdb..#Tournees') IS NOT NULL DROP TABLE #Tournees;
IF OBJECT_ID('tempdb..#LignesTournee') IS NOT NULL DROP TABLE #LignesTournee;
IF OBJECT_ID('tempdb..#Preparations') IS NOT NULL DROP TABLE #Preparations;
IF OBJECT_ID('tempdb..#Lots') IS NOT NULL DROP TABLE #Lots;
IF OBJECT_ID('tempdb..#Verification') IS NOT NULL DROP TABLE #Verification;

CREATE TABLE #Tournees
(
    IdTourneeMobile int NOT NULL PRIMARY KEY,
    DateTournee date NULL,
    CodeTournee nvarchar(100) NULL
);

CREATE TABLE #LignesTournee
(
    IdTourneeLigneMobile int NOT NULL PRIMARY KEY,
    IdTourneeMobile int NOT NULL
);

CREATE TABLE #Preparations
(
    IdPreparationExpedition int NOT NULL PRIMARY KEY,
    IdLotVerrouillage nvarchar(100) NULL,
    DateTournee date NULL,
    CodeTournee nvarchar(100) NULL
);

CREATE TABLE #Lots
(
    IdLotVerrouillage nvarchar(100) NOT NULL PRIMARY KEY
);

CREATE TABLE #Verification
(
    Element nvarchar(200) NOT NULL,
    Nombre int NOT NULL
);

DECLARE @Sql nvarchar(max);

/* ============================================================
   1) IDENTIFICATION DES TOURNEES MOBILES CIBLEES
   ============================================================ */

IF OBJECT_ID('dbo.Mobile_Tournee', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_Tournee', 'IdTourneeMobile') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_Tournee', 'DateTournee') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_Tournee', 'CodeTournee') IS NOT NULL
BEGIN
    SET @Sql = N'
        INSERT INTO #Tournees (IdTourneeMobile, DateTournee, CodeTournee)
        SELECT t.IdTourneeMobile, t.DateTournee, t.CodeTournee
        FROM dbo.Mobile_Tournee t
        WHERE t.DateTournee = @DateTournee
          AND t.CodeTournee LIKE @CodeTourneePrefix + N''%'';
    ';

    EXEC sys.sp_executesql
        @Sql,
        N'@DateTournee date, @CodeTourneePrefix nvarchar(50)',
        @DateTournee = @DateTournee,
        @CodeTourneePrefix = @CodeTourneePrefix;
END
ELSE
BEGIN
    PRINT 'Info : Mobile_Tournee absente ou colonnes attendues absentes.';
END

IF OBJECT_ID('dbo.Mobile_TourneeLigne', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_TourneeLigne', 'IdTourneeLigneMobile') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_TourneeLigne', 'IdTourneeMobile') IS NOT NULL
BEGIN
    SET @Sql = N'
        INSERT INTO #LignesTournee (IdTourneeLigneMobile, IdTourneeMobile)
        SELECT l.IdTourneeLigneMobile, l.IdTourneeMobile
        FROM dbo.Mobile_TourneeLigne l
        INNER JOIN #Tournees t ON t.IdTourneeMobile = l.IdTourneeMobile;
    ';

    EXEC sys.sp_executesql @Sql;
END
ELSE
BEGIN
    PRINT 'Info : Mobile_TourneeLigne absente ou colonnes attendues absentes.';
END

/* ============================================================
   2) IDENTIFICATION DES LOTS EXPEDITION CIBLES
   ============================================================ */

IF OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'IdLotVerrouillage') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'DateTournee') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'CodeTourneeLot') IS NOT NULL
BEGIN
    SET @Sql = N'
        INSERT INTO #Lots (IdLotVerrouillage)
        SELECT CONVERT(nvarchar(100), l.IdLotVerrouillage)
        FROM dbo.Mobile_ExpeditionLotVerrouillage l
        WHERE l.DateTournee = @DateTournee
          AND
          (
              l.CodeTourneeLot LIKE @CodeTourneePrefix + N''%''
              OR (@InclureLotsGlobaux = N''OUI'' AND l.CodeTourneeLot = N''GLOBAL'')
          )
          AND NOT EXISTS
          (
              SELECT 1
              FROM #Lots x
              WHERE x.IdLotVerrouillage = CONVERT(nvarchar(100), l.IdLotVerrouillage)
          );
    ';

    EXEC sys.sp_executesql
        @Sql,
        N'@DateTournee date, @CodeTourneePrefix nvarchar(50), @InclureLotsGlobaux nvarchar(10)',
        @DateTournee = @DateTournee,
        @CodeTourneePrefix = @CodeTourneePrefix,
        @InclureLotsGlobaux = @InclureLotsGlobaux;
END
ELSE
BEGIN
    PRINT 'Info : Mobile_ExpeditionLotVerrouillage absente ou colonnes DateTournee/CodeTourneeLot absentes.';
END

/* ============================================================
   3) IDENTIFICATION DES PREPARATIONS EXPEDITION CIBLEES
   ============================================================ */

IF OBJECT_ID('dbo.Mobile_ExpeditionPreparation', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'IdPreparationExpedition') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'DateTournee') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'CodeTournee') IS NOT NULL
BEGIN
    SET @Sql = N'
        INSERT INTO #Preparations
        (
            IdPreparationExpedition,
            IdLotVerrouillage,
            DateTournee,
            CodeTournee
        )
        SELECT
            p.IdPreparationExpedition,
            ' + CASE
                    WHEN COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'IdLotVerrouillage') IS NOT NULL
                    THEN N'CONVERT(nvarchar(100), p.IdLotVerrouillage)'
                    ELSE N'NULL'
                END + N',
            p.DateTournee,
            p.CodeTournee
        FROM dbo.Mobile_ExpeditionPreparation p
        WHERE p.DateTournee = @DateTournee
          AND p.CodeTournee LIKE @CodeTourneePrefix + N''%''
          AND NOT EXISTS
          (
              SELECT 1
              FROM #Preparations x
              WHERE x.IdPreparationExpedition = p.IdPreparationExpedition
          );
    ';

    EXEC sys.sp_executesql
        @Sql,
        N'@DateTournee date, @CodeTourneePrefix nvarchar(50)',
        @DateTournee = @DateTournee,
        @CodeTourneePrefix = @CodeTourneePrefix;
END
ELSE
BEGIN
    PRINT 'Info : Mobile_ExpeditionPreparation absente ou sans DateTournee/CodeTournee.';
END

IF OBJECT_ID('dbo.Mobile_ExpeditionPreparation', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'IdPreparationExpedition') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'IdLotVerrouillage') IS NOT NULL
BEGIN
    SET @Sql = N'
        INSERT INTO #Preparations
        (
            IdPreparationExpedition,
            IdLotVerrouillage,
            DateTournee,
            CodeTournee
        )
        SELECT
            p.IdPreparationExpedition,
            CONVERT(nvarchar(100), p.IdLotVerrouillage),
            ' + CASE
                    WHEN COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'DateTournee') IS NOT NULL
                    THEN N'p.DateTournee'
                    ELSE N'NULL'
                END + N',
            ' + CASE
                    WHEN COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'CodeTournee') IS NOT NULL
                    THEN N'p.CodeTournee'
                    ELSE N'NULL'
                END + N'
        FROM dbo.Mobile_ExpeditionPreparation p
        INNER JOIN #Lots l ON l.IdLotVerrouillage = CONVERT(nvarchar(100), p.IdLotVerrouillage)
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM #Preparations x
            WHERE x.IdPreparationExpedition = p.IdPreparationExpedition
        );
    ';

    EXEC sys.sp_executesql @Sql;
END

IF OBJECT_ID('dbo.Mobile_ExpeditionPreparation', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'IdLotVerrouillage') IS NOT NULL
BEGIN
    INSERT INTO #Lots (IdLotVerrouillage)
    SELECT DISTINCT p.IdLotVerrouillage
    FROM #Preparations p
    WHERE p.IdLotVerrouillage IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM #Lots l
          WHERE l.IdLotVerrouillage = p.IdLotVerrouillage
      );
END

/* ============================================================
   4) VERIFICATION AVANT SUPPRESSION
   ============================================================ */

PRINT '============================================================';
PRINT 'ELEMENTS CIBLES AVANT NETTOYAGE';
PRINT '============================================================';

SELECT 'Mobile_Tournee' AS Element, COUNT(*) AS Nombre
FROM #Tournees

UNION ALL

SELECT 'Mobile_TourneeLigne', COUNT(*)
FROM #LignesTournee

UNION ALL

SELECT 'Mobile_ExpeditionPreparation', COUNT(*)
FROM #Preparations

UNION ALL

SELECT 'Mobile_ExpeditionLotVerrouillage', COUNT(*)
FROM #Lots;

IF OBJECT_ID('dbo.Mobile_TourneeLigneQuantite', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_TourneeLigneQuantite', 'IdTourneeLigneMobile') IS NOT NULL
BEGIN
    SET @Sql = N'
        SELECT ''Mobile_TourneeLigneQuantite'' AS Element, COUNT(*) AS Nombre
        FROM dbo.Mobile_TourneeLigneQuantite q
        INNER JOIN #LignesTournee l ON l.IdTourneeLigneMobile = q.IdTourneeLigneMobile;
    ';

    EXEC sys.sp_executesql @Sql;
END

IF OBJECT_ID('dbo.Mobile_ChargementTournee', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ChargementTournee', 'DateTournee') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ChargementTournee', 'CodeTournee') IS NOT NULL
BEGIN
    SET @Sql = N'
        SELECT ''Mobile_ChargementTournee'' AS Element, COUNT(*) AS Nombre
        FROM dbo.Mobile_ChargementTournee c
        WHERE c.DateTournee = @DateTournee
          AND c.CodeTournee LIKE @CodeTourneePrefix + N''%'';
    ';

    EXEC sys.sp_executesql
        @Sql,
        N'@DateTournee date, @CodeTourneePrefix nvarchar(50)',
        @DateTournee = @DateTournee,
        @CodeTourneePrefix = @CodeTourneePrefix;
END

IF OBJECT_ID('dbo.Mobile_ExportAdmin', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExportAdmin', 'DateTournee') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExportAdmin', 'CodeTournee') IS NOT NULL
BEGIN
    SET @Sql = N'
        SELECT ''Mobile_ExportAdmin'' AS Element, COUNT(*) AS Nombre
        FROM dbo.Mobile_ExportAdmin e
        WHERE e.DateTournee = @DateTournee
          AND e.CodeTournee LIKE @CodeTourneePrefix + N''%'';
    ';

    EXEC sys.sp_executesql
        @Sql,
        N'@DateTournee date, @CodeTourneePrefix nvarchar(50)',
        @DateTournee = @DateTournee,
        @CodeTourneePrefix = @CodeTourneePrefix;
END

IF OBJECT_ID('dbo.Mobile_LogSynchronisation', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_LogSynchronisation', 'DateTournee') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_LogSynchronisation', 'CodeTournee') IS NOT NULL
BEGIN
    SET @Sql = N'
        SELECT ''Mobile_LogSynchronisation'' AS Element, COUNT(*) AS Nombre
        FROM dbo.Mobile_LogSynchronisation l
        WHERE l.DateTournee = @DateTournee
          AND l.CodeTournee LIKE @CodeTourneePrefix + N''%'';
    ';

    EXEC sys.sp_executesql
        @Sql,
        N'@DateTournee date, @CodeTourneePrefix nvarchar(50)',
        @DateTournee = @DateTournee,
        @CodeTourneePrefix = @CodeTourneePrefix;
END
ELSE IF OBJECT_ID('dbo.Mobile_LogSynchronisation', 'U') IS NOT NULL
        AND COL_LENGTH('dbo.Mobile_LogSynchronisation', 'IdTourneeMobile') IS NOT NULL
BEGIN
    SET @Sql = N'
        SELECT ''Mobile_LogSynchronisation'' AS Element, COUNT(*) AS Nombre
        FROM dbo.Mobile_LogSynchronisation l
        INNER JOIN #Tournees t ON t.IdTourneeMobile = l.IdTourneeMobile;
    ';

    EXEC sys.sp_executesql @Sql;
END

IF OBJECT_ID('dbo.Mobile_ExpeditionPreparationLigne', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionPreparationLigne', 'IdPreparationExpedition') IS NOT NULL
BEGIN
    SET @Sql = N'
        SELECT ''Mobile_ExpeditionPreparationLigne'' AS Element, COUNT(*) AS Nombre
        FROM dbo.Mobile_ExpeditionPreparationLigne l
        INNER JOIN #Preparations p ON p.IdPreparationExpedition = l.IdPreparationExpedition;
    ';

    EXEC sys.sp_executesql @Sql;
END

/* ============================================================
   5) MODE SIMULATION PAR DEFAUT
   ============================================================ */

IF @ExecuteDelete = N'NON'
BEGIN
    PRINT '============================================================';
    PRINT 'MODE SIMULATION : aucune suppression effectuee.';
    PRINT 'Verifier les compteurs ci-dessus.';
    PRINT 'Pour supprimer reellement, modifier :';
    PRINT 'DECLARE @ExecuteDelete nvarchar(10) = N''OUI'';';
    PRINT '============================================================';
    RETURN;
END

/* ============================================================
   6) SUPPRESSION REELLE DANS L'ORDRE DES DEPENDANCES
   ============================================================ */

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '============================================================';
    PRINT 'SUPPRESSION REELLE DES DONNEES CIBLEES';
    PRINT '============================================================';

    IF OBJECT_ID('dbo.Mobile_TourneeLigneQuantite', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_TourneeLigneQuantite', 'IdTourneeLigneMobile') IS NOT NULL
    BEGIN
        SET @Sql = N'
            DELETE q
            FROM dbo.Mobile_TourneeLigneQuantite q
            INNER JOIN #LignesTournee l ON l.IdTourneeLigneMobile = q.IdTourneeLigneMobile;
        ';

        EXEC sys.sp_executesql @Sql;
        PRINT 'Mobile_TourneeLigneQuantite nettoyee.';
    END

    IF OBJECT_ID('dbo.Mobile_LogSynchronisation', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_LogSynchronisation', 'DateTournee') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_LogSynchronisation', 'CodeTournee') IS NOT NULL
    BEGIN
        SET @Sql = N'
            DELETE l
            FROM dbo.Mobile_LogSynchronisation l
            WHERE l.DateTournee = @DateTournee
              AND l.CodeTournee LIKE @CodeTourneePrefix + N''%'';
        ';

        EXEC sys.sp_executesql
            @Sql,
            N'@DateTournee date, @CodeTourneePrefix nvarchar(50)',
            @DateTournee = @DateTournee,
            @CodeTourneePrefix = @CodeTourneePrefix;

        PRINT 'Mobile_LogSynchronisation nettoyee par DateTournee/CodeTournee.';
    END
    ELSE IF OBJECT_ID('dbo.Mobile_LogSynchronisation', 'U') IS NOT NULL
            AND COL_LENGTH('dbo.Mobile_LogSynchronisation', 'IdTourneeMobile') IS NOT NULL
    BEGIN
        SET @Sql = N'
            DELETE l
            FROM dbo.Mobile_LogSynchronisation l
            INNER JOIN #Tournees t ON t.IdTourneeMobile = l.IdTourneeMobile;
        ';

        EXEC sys.sp_executesql @Sql;
        PRINT 'Mobile_LogSynchronisation nettoyee par IdTourneeMobile.';
    END

    IF OBJECT_ID('dbo.Mobile_ExportAdmin', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_ExportAdmin', 'DateTournee') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_ExportAdmin', 'CodeTournee') IS NOT NULL
    BEGIN
        SET @Sql = N'
            DELETE e
            FROM dbo.Mobile_ExportAdmin e
            WHERE e.DateTournee = @DateTournee
              AND e.CodeTournee LIKE @CodeTourneePrefix + N''%'';
        ';

        EXEC sys.sp_executesql
            @Sql,
            N'@DateTournee date, @CodeTourneePrefix nvarchar(50)',
            @DateTournee = @DateTournee,
            @CodeTourneePrefix = @CodeTourneePrefix;

        PRINT 'Mobile_ExportAdmin nettoyee.';
    END

    IF OBJECT_ID('dbo.Mobile_TourneeLigne', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_TourneeLigne', 'IdTourneeLigneMobile') IS NOT NULL
    BEGIN
        SET @Sql = N'
            DELETE l
            FROM dbo.Mobile_TourneeLigne l
            INNER JOIN #LignesTournee c ON c.IdTourneeLigneMobile = l.IdTourneeLigneMobile;
        ';

        EXEC sys.sp_executesql @Sql;
        PRINT 'Mobile_TourneeLigne nettoyee.';
    END

    IF OBJECT_ID('dbo.Mobile_Tournee', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_Tournee', 'IdTourneeMobile') IS NOT NULL
    BEGIN
        SET @Sql = N'
            DELETE t
            FROM dbo.Mobile_Tournee t
            INNER JOIN #Tournees c ON c.IdTourneeMobile = t.IdTourneeMobile;
        ';

        EXEC sys.sp_executesql @Sql;
        PRINT 'Mobile_Tournee nettoyee.';
    END

    IF OBJECT_ID('dbo.Mobile_ChargementTournee', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_ChargementTournee', 'DateTournee') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_ChargementTournee', 'CodeTournee') IS NOT NULL
    BEGIN
        SET @Sql = N'
            DELETE c
            FROM dbo.Mobile_ChargementTournee c
            WHERE c.DateTournee = @DateTournee
              AND c.CodeTournee LIKE @CodeTourneePrefix + N''%'';
        ';

        EXEC sys.sp_executesql
            @Sql,
            N'@DateTournee date, @CodeTourneePrefix nvarchar(50)',
            @DateTournee = @DateTournee,
            @CodeTourneePrefix = @CodeTourneePrefix;

        PRINT 'Mobile_ChargementTournee nettoyee.';
    END

    IF OBJECT_ID('dbo.Mobile_ExpeditionPreparationLigne', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_ExpeditionPreparationLigne', 'IdPreparationExpedition') IS NOT NULL
    BEGIN
        SET @Sql = N'
            DELETE l
            FROM dbo.Mobile_ExpeditionPreparationLigne l
            INNER JOIN #Preparations p ON p.IdPreparationExpedition = l.IdPreparationExpedition;
        ';

        EXEC sys.sp_executesql @Sql;
        PRINT 'Mobile_ExpeditionPreparationLigne nettoyee.';
    END

    IF OBJECT_ID('dbo.Mobile_ExpeditionPreparation', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'IdPreparationExpedition') IS NOT NULL
    BEGIN
        SET @Sql = N'
            DELETE p
            FROM dbo.Mobile_ExpeditionPreparation p
            INNER JOIN #Preparations c ON c.IdPreparationExpedition = p.IdPreparationExpedition;
        ';

        EXEC sys.sp_executesql @Sql;
        PRINT 'Mobile_ExpeditionPreparation nettoyee.';
    END

    IF OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'IdLotVerrouillage') IS NOT NULL
    BEGIN
        SET @Sql = N'
            DELETE lot
            FROM dbo.Mobile_ExpeditionLotVerrouillage lot
            INNER JOIN #Lots c ON c.IdLotVerrouillage = CONVERT(nvarchar(100), lot.IdLotVerrouillage);
        ';

        EXEC sys.sp_executesql @Sql;
        PRINT 'Mobile_ExpeditionLotVerrouillage nettoyee.';
    END

    COMMIT TRANSACTION;

    PRINT '============================================================';
    PRINT 'Nettoyage cible termine avec succes.';
    PRINT '============================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '============================================================';
    PRINT 'ERREUR : nettoyage annule, transaction rollback.';
    PRINT '============================================================';

    SELECT
        ERROR_NUMBER() AS ErrorNumber,
        ERROR_MESSAGE() AS ErrorMessage,
        ERROR_LINE() AS ErrorLine,
        ERROR_PROCEDURE() AS ErrorProcedure;

    THROW;
END CATCH;

/* ============================================================
   7) VERIFICATION APRES NETTOYAGE
   ============================================================ */

PRINT '============================================================';
PRINT 'VERIFICATION APRES NETTOYAGE';
PRINT '============================================================';

IF OBJECT_ID('dbo.Mobile_Tournee', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_Tournee', 'IdTourneeMobile') IS NOT NULL
BEGIN
    SET @Sql = N'
        INSERT INTO #Verification (Element, Nombre)
        SELECT ''Mobile_Tournee restantes ciblees'', COUNT(*)
        FROM #Tournees t
        WHERE EXISTS
        (
            SELECT 1
            FROM dbo.Mobile_Tournee mt
            WHERE mt.IdTourneeMobile = t.IdTourneeMobile
        );
    ';

    EXEC sys.sp_executesql @Sql;
END

IF OBJECT_ID('dbo.Mobile_TourneeLigne', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_TourneeLigne', 'IdTourneeLigneMobile') IS NOT NULL
BEGIN
    SET @Sql = N'
        INSERT INTO #Verification (Element, Nombre)
        SELECT ''Mobile_TourneeLigne restantes ciblees'', COUNT(*)
        FROM #LignesTournee l
        WHERE EXISTS
        (
            SELECT 1
            FROM dbo.Mobile_TourneeLigne ml
            WHERE ml.IdTourneeLigneMobile = l.IdTourneeLigneMobile
        );
    ';

    EXEC sys.sp_executesql @Sql;
END

IF OBJECT_ID('dbo.Mobile_ExpeditionPreparation', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionPreparation', 'IdPreparationExpedition') IS NOT NULL
BEGIN
    SET @Sql = N'
        INSERT INTO #Verification (Element, Nombre)
        SELECT ''Mobile_ExpeditionPreparation restantes ciblees'', COUNT(*)
        FROM #Preparations p
        WHERE EXISTS
        (
            SELECT 1
            FROM dbo.Mobile_ExpeditionPreparation mp
            WHERE mp.IdPreparationExpedition = p.IdPreparationExpedition
        );
    ';

    EXEC sys.sp_executesql @Sql;
END

IF OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'IdLotVerrouillage') IS NOT NULL
BEGIN
    SET @Sql = N'
        INSERT INTO #Verification (Element, Nombre)
        SELECT ''Mobile_ExpeditionLotVerrouillage restants cibles'', COUNT(*)
        FROM #Lots l
        WHERE EXISTS
        (
            SELECT 1
            FROM dbo.Mobile_ExpeditionLotVerrouillage ml
            WHERE CONVERT(nvarchar(100), ml.IdLotVerrouillage) = l.IdLotVerrouillage
        );
    ';

    EXEC sys.sp_executesql @Sql;
END

SELECT Element, Nombre
FROM #Verification
ORDER BY Element;

PRINT 'Verification terminee.';