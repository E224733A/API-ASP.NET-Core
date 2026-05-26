/* ============================================================================
   Migration MobileSLI - ROLLS_VIDES côté Expédition
   Date : 2026-05-26

   Décision métier :
   - ROLLS signifie "Chariots".
   - ROLLS_VIDES signifie "Chariots vides".
   - Les chariots vides demandés par les clients deviennent une quantité livrée
     prévue saisie par l'Expédition.
   - Le même code article ROLLS_VIDES peut donc porter :
       quantiteLivreePrevue,
       quantiteLivree,
       quantiteRecuperee.

   Important :
   - Exécuter ce script sur la base mobile de l'API, pas sur les tables ABSSolute.
   - Faire une sauvegarde avant exécution en production.
   - Le script est non destructif pour les données métier.
============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Mobile_ArticleSaisissable', N'U') IS NULL
BEGIN
    THROW 50001, 'Table dbo.Mobile_ArticleSaisissable introuvable. Vérifier la base sélectionnée.', 1;
END;

/*
   Ancienne contrainte v13 :
   ROLLS_VIDES devait être non livrable et non utilisable Expédition.
   Cette règle n'est plus correcte avec la nouvelle décision métier.
*/
DECLARE @ConstraintName sysname;

SELECT @ConstraintName = cc.name
FROM sys.check_constraints AS cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Mobile_ArticleSaisissable')
  AND cc.name = N'CK_Mobile_ArticleSaisissable_RollsVides';

IF @ConstraintName IS NOT NULL
BEGIN
    DECLARE @DropConstraintSql nvarchar(max) =
        N'ALTER TABLE dbo.Mobile_ArticleSaisissable DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
    EXEC sys.sp_executesql @DropConstraintSql;
END;

DECLARE @HasExtendedUsageColumns bit = CASE
    WHEN COL_LENGTH(N'dbo.Mobile_ArticleSaisissable', N'EstLivrableMobile') IS NOT NULL
     AND COL_LENGTH(N'dbo.Mobile_ArticleSaisissable', N'EstRecuperableMobile') IS NOT NULL
     AND COL_LENGTH(N'dbo.Mobile_ArticleSaisissable', N'EstUtilisableExpedition') IS NOT NULL
    THEN 1
    ELSE 0
END;

IF @HasExtendedUsageColumns = 1
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM dbo.Mobile_ArticleSaisissable
        WHERE CodeArticle = N'ROLLS'
    )
    BEGIN
        INSERT INTO dbo.Mobile_ArticleSaisissable (
            CodeArticle,
            LibelleArticle,
            OrdreAffichage,
            EstActif,
            EstVisibleMobile,
            EstLivrableMobile,
            EstRecuperableMobile,
            EstUtilisableExpedition
        )
        VALUES (
            N'ROLLS',
            N'Chariots',
            1,
            1,
            1,
            1,
            1,
            1
        );
    END
    ELSE
    BEGIN
        UPDATE dbo.Mobile_ArticleSaisissable
        SET
            LibelleArticle = N'Chariots',
            OrdreAffichage = 1,
            EstActif = 1,
            EstVisibleMobile = 1,
            EstLivrableMobile = 1,
            EstRecuperableMobile = 1,
            EstUtilisableExpedition = 1,
            DateModification = SYSDATETIMEOFFSET()
        WHERE CodeArticle = N'ROLLS';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.Mobile_ArticleSaisissable
        WHERE CodeArticle = N'ROLLS_VIDES'
    )
    BEGIN
        INSERT INTO dbo.Mobile_ArticleSaisissable (
            CodeArticle,
            LibelleArticle,
            OrdreAffichage,
            EstActif,
            EstVisibleMobile,
            EstLivrableMobile,
            EstRecuperableMobile,
            EstUtilisableExpedition
        )
        VALUES (
            N'ROLLS_VIDES',
            N'Chariots vides',
            2,
            1,
            1,
            1,
            1,
            1
        );
    END
    ELSE
    BEGIN
        UPDATE dbo.Mobile_ArticleSaisissable
        SET
            LibelleArticle = N'Chariots vides',
            OrdreAffichage = 2,
            EstActif = 1,
            EstVisibleMobile = 1,
            EstLivrableMobile = 1,
            EstRecuperableMobile = 1,
            EstUtilisableExpedition = 1,
            DateModification = SYSDATETIMEOFFSET()
        WHERE CodeArticle = N'ROLLS_VIDES';
    END;
END
ELSE
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM dbo.Mobile_ArticleSaisissable
        WHERE CodeArticle = N'ROLLS'
    )
    BEGIN
        INSERT INTO dbo.Mobile_ArticleSaisissable (
            CodeArticle,
            LibelleArticle,
            OrdreAffichage,
            EstActif,
            EstVisibleMobile
        )
        VALUES (
            N'ROLLS',
            N'Chariots',
            1,
            1,
            1
        );
    END
    ELSE
    BEGIN
        UPDATE dbo.Mobile_ArticleSaisissable
        SET
            LibelleArticle = N'Chariots',
            OrdreAffichage = 1,
            EstActif = 1,
            EstVisibleMobile = 1
        WHERE CodeArticle = N'ROLLS';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.Mobile_ArticleSaisissable
        WHERE CodeArticle = N'ROLLS_VIDES'
    )
    BEGIN
        INSERT INTO dbo.Mobile_ArticleSaisissable (
            CodeArticle,
            LibelleArticle,
            OrdreAffichage,
            EstActif,
            EstVisibleMobile
        )
        VALUES (
            N'ROLLS_VIDES',
            N'Chariots vides',
            2,
            1,
            1
        );
    END
    ELSE
    BEGIN
        UPDATE dbo.Mobile_ArticleSaisissable
        SET
            LibelleArticle = N'Chariots vides',
            OrdreAffichage = 2,
            EstActif = 1,
            EstVisibleMobile = 1
        WHERE CodeArticle = N'ROLLS_VIDES';
    END;
END;

COMMIT TRANSACTION;

IF COL_LENGTH(N'dbo.Mobile_ArticleSaisissable', N'EstLivrableMobile') IS NOT NULL
BEGIN
    SELECT
        CodeArticle,
        LibelleArticle,
        OrdreAffichage,
        EstActif,
        EstVisibleMobile,
        EstLivrableMobile,
        EstRecuperableMobile,
        EstUtilisableExpedition
    FROM dbo.Mobile_ArticleSaisissable
    WHERE CodeArticle IN (N'ROLLS', N'ROLLS_VIDES', N'TAPIS', N'SACS')
    ORDER BY OrdreAffichage, CodeArticle;
END
ELSE
BEGIN
    SELECT
        CodeArticle,
        LibelleArticle,
        OrdreAffichage,
        EstActif,
        EstVisibleMobile
    FROM dbo.Mobile_ArticleSaisissable
    WHERE CodeArticle IN (N'ROLLS', N'ROLLS_VIDES', N'TAPIS', N'SACS')
    ORDER BY OrdreAffichage, CodeArticle;
END;
