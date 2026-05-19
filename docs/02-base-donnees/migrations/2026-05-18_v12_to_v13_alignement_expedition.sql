/* ============================================================
   MIGRATION SQL - PASSAGE BDD SLI v1.2 -> v1.3
   Projet MobileSLI - API ASP.NET Core

   Fichier : 2026-05-18_v12_to_v13_alignement_expedition.sql

   Objectif :
   - Aligner une base existante avec le fonctionnement actuel de l'API.
   - Sécuriser le verrouillage Expédition par lot idempotent.
   - Ajouter ou compléter les colonnes attendues par ExpeditionRepository.cs.
   - Conserver la règle mobile : anti-doublon métier DateTournee + CodeTournee.
   - Interdire ROLLS_VIDES dans les préparations Expédition actives.

   À utiliser uniquement sur une base existante.
   Pour repartir de zéro en développement/test, utiliser plutôt :
   docs/02-base-donnees/complete/BDD_sli_v13_complete.sql

   Important :
   - Faire une sauvegarde avant exécution.
   - Ne jamais exécuter directement en production sans validation.
   - Ce script ne modifie pas les tables internes ABSSolute.
============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================
   1. Vérification des tables socles attendues
============================================================ */
IF OBJECT_ID('dbo.Mobile_PreRemplissageTournee', 'U') IS NULL
BEGIN
    THROW 52001, 'Table dbo.Mobile_PreRemplissageTournee introuvable. Exécuter le script complet v13 ou la base v12 avant cette migration.', 1;
END;
GO

IF OBJECT_ID('dbo.Mobile_PreRemplissageQuantite', 'U') IS NULL
BEGIN
    THROW 52002, 'Table dbo.Mobile_PreRemplissageQuantite introuvable. Exécuter le script complet v13 ou la base v12 avant cette migration.', 1;
END;
GO

/* ============================================================
   2. Table de lots Expédition compatible avec l'API actuelle

   ExpeditionRepository.cs attend notamment :
   - IdLotVerrouillage
   - EmpreintePayload
   - DateTournee
   - CodeTournee
   - IdPreRemplissageTournee
============================================================ */
IF OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Mobile_ExpeditionLotVerrouillage (
        IdLotVerrouillage UNIQUEIDENTIFIER NOT NULL,
        EmpreintePayload CHAR(64) NOT NULL,
        DateTournee DATE NOT NULL,
        CodeTournee NVARCHAR(50) NOT NULL,
        LibelleTournee NVARCHAR(255) NULL,
        StatutLot NVARCHAR(30) NOT NULL
            CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_StatutLot DEFAULT N'VERROUILLE',
        NombreLignes INT NOT NULL
            CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreLignes DEFAULT 0,
        NombreQuantites INT NOT NULL
            CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreQuantites DEFAULT 0,
        IdPreRemplissageTournee BIGINT NULL,
        AdresseIP NVARCHAR(50) NULL,
        DateCreation DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_DateCreation DEFAULT SYSDATETIMEOFFSET(),
        DateModification DATETIMEOFFSET(0) NULL,
        CONSTRAINT PK_Mobile_ExpeditionLotVerrouillage
            PRIMARY KEY (IdLotVerrouillage),
        CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_EmpreintePayload
            CHECK (LEN(LTRIM(RTRIM(EmpreintePayload))) = 64),
        CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_CodeTournee_NonVide
            CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
        CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_StatutLot
            CHECK (StatutLot IN (N'VERROUILLE', N'REJOUE_IDENTIQUE', N'REFUSE')),
        CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_Nombres
            CHECK (NombreLignes >= 0 AND NombreQuantites >= 0)
    );
END;
GO

/* Compléments si une ancienne migration avait créé une version incomplète de la table. */
IF COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'EmpreintePayload') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD EmpreintePayload CHAR(64) NULL;
END;
GO

UPDATE dbo.Mobile_ExpeditionLotVerrouillage
SET EmpreintePayload = REPLICATE('0', 64)
WHERE EmpreintePayload IS NULL;
GO

ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
ALTER COLUMN EmpreintePayload CHAR(64) NOT NULL;
GO

IF COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'CodeTournee') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD CodeTournee NVARCHAR(50) NULL;
END;
GO

UPDATE dbo.Mobile_ExpeditionLotVerrouillage
SET CodeTournee = N'GLOBAL'
WHERE CodeTournee IS NULL OR LEN(LTRIM(RTRIM(CodeTournee))) = 0;
GO

ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
ALTER COLUMN CodeTournee NVARCHAR(50) NOT NULL;
GO

IF COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'LibelleTournee') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD LibelleTournee NVARCHAR(255) NULL;
END;
GO

IF COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'StatutLot') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD StatutLot NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_StatutLot DEFAULT N'VERROUILLE' WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'NombreLignes') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD NombreLignes INT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreLignes DEFAULT 0 WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'NombreQuantites') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD NombreQuantites INT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreQuantites DEFAULT 0 WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'IdPreRemplissageTournee') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD IdPreRemplissageTournee BIGINT NULL;
END;
GO

IF COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'AdresseIP') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD AdresseIP NVARCHAR(50) NULL;
END;
GO

IF COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'DateCreation') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_DateCreation DEFAULT SYSDATETIMEOFFSET() WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.Mobile_ExpeditionLotVerrouillage', 'DateModification') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD DateModification DATETIMEOFFSET(0) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_Mobile_ExpeditionLotVerrouillage_EmpreintePayload'
      AND parent_object_id = OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage')
)
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_EmpreintePayload
        CHECK (LEN(LTRIM(RTRIM(EmpreintePayload))) = 64);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_Mobile_ExpeditionLotVerrouillage_CodeTournee_NonVide'
      AND parent_object_id = OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage')
)
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_Mobile_ExpeditionLotVerrouillage_StatutLot'
      AND parent_object_id = OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage')
)
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    ADD CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_StatutLot
        CHECK (StatutLot IN (N'VERROUILLE', N'REJOUE_IDENTIQUE', N'REFUSE'));
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Mobile_ExpeditionLotVerrouillage_DateCode'
      AND object_id = OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage')
)
BEGIN
    CREATE INDEX IX_Mobile_ExpeditionLotVerrouillage_DateCode
    ON dbo.Mobile_ExpeditionLotVerrouillage (DateTournee, CodeTournee);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_Mobile_ExpeditionLotVerrouillage_DateCodeEmpreinte'
      AND object_id = OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage')
)
AND NOT EXISTS (
    SELECT 1
    FROM (
        SELECT DateTournee, CodeTournee, EmpreintePayload
        FROM dbo.Mobile_ExpeditionLotVerrouillage
        GROUP BY DateTournee, CodeTournee, EmpreintePayload
        HAVING COUNT(*) > 1
    ) doublons
)
BEGIN
    CREATE UNIQUE INDEX UX_Mobile_ExpeditionLotVerrouillage_DateCodeEmpreinte
    ON dbo.Mobile_ExpeditionLotVerrouillage (DateTournee, CodeTournee, EmpreintePayload);
END;
GO

/* ============================================================
   3. Compléments Mobile_PreRemplissageTournee
============================================================ */
IF COL_LENGTH('dbo.Mobile_PreRemplissageTournee', 'IdLotVerrouillage') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_PreRemplissageTournee
    ADD IdLotVerrouillage UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH('dbo.Mobile_PreRemplissageTournee', 'EmpreintePayload') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_PreRemplissageTournee
    ADD EmpreintePayload CHAR(64) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_Mobile_PreRemplissageTournee_EmpreintePayload'
      AND parent_object_id = OBJECT_ID('dbo.Mobile_PreRemplissageTournee')
)
BEGIN
    ALTER TABLE dbo.Mobile_PreRemplissageTournee
    ADD CONSTRAINT CK_Mobile_PreRemplissageTournee_EmpreintePayload
        CHECK (EmpreintePayload IS NULL OR LEN(LTRIM(RTRIM(EmpreintePayload))) = 64);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_Mobile_PreRemplissageTournee_LotVerrouillage'
)
BEGIN
    ALTER TABLE dbo.Mobile_PreRemplissageTournee
    ADD CONSTRAINT FK_Mobile_PreRemplissageTournee_LotVerrouillage
        FOREIGN KEY (IdLotVerrouillage)
        REFERENCES dbo.Mobile_ExpeditionLotVerrouillage(IdLotVerrouillage);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Mobile_PreRemplissageTournee_Lot'
      AND object_id = OBJECT_ID('dbo.Mobile_PreRemplissageTournee')
)
BEGIN
    CREATE INDEX IX_Mobile_PreRemplissageTournee_Lot
    ON dbo.Mobile_PreRemplissageTournee (IdLotVerrouillage);
END;
GO

/* ============================================================
   4. Interdiction des ROLLS_VIDES actifs dans la préparation Expédition
============================================================ */
UPDATE dbo.Mobile_PreRemplissageQuantite
SET
    Actif = 0,
    DateModification = SYSDATETIMEOFFSET()
WHERE CodeArticle = N'ROLLS_VIDES'
  AND Actif = 1;
GO

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_Mobile_PreRemplissageQuantite_RollsVides'
      AND parent_object_id = OBJECT_ID('dbo.Mobile_PreRemplissageQuantite')
)
BEGIN
    ALTER TABLE dbo.Mobile_PreRemplissageQuantite
    DROP CONSTRAINT CK_Mobile_PreRemplissageQuantite_RollsVides;
END;
GO

ALTER TABLE dbo.Mobile_PreRemplissageQuantite
ADD CONSTRAINT CK_Mobile_PreRemplissageQuantite_RollsVides
    CHECK (
        CodeArticle <> N'ROLLS_VIDES'
        OR Actif = 0
    );
GO

/* ============================================================
   5. Vérification rapide
============================================================ */
SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN (
    'Mobile_ExpeditionLotVerrouillage',
    'Mobile_PreRemplissageTournee',
    'Mobile_PreRemplissageQuantite'
)
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

SELECT
    name AS ConstraintName,
    OBJECT_NAME(parent_object_id) AS TableName,
    definition
FROM sys.check_constraints
WHERE OBJECT_NAME(parent_object_id) IN (
    'Mobile_ExpeditionLotVerrouillage',
    'Mobile_PreRemplissageTournee',
    'Mobile_PreRemplissageQuantite'
)
ORDER BY TableName, ConstraintName;
GO

SELECT
    name AS IndexName,
    OBJECT_NAME(object_id) AS TableName,
    is_unique,
    has_filter,
    filter_definition
FROM sys.indexes
WHERE OBJECT_NAME(object_id) IN (
    'Mobile_ExpeditionLotVerrouillage',
    'Mobile_PreRemplissageTournee',
    'Mobile_PreRemplissageQuantite'
)
  AND name IS NOT NULL
ORDER BY TableName, IndexName;
GO
