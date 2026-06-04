/* ============================================================
   MIGRATION NON DESTRUCTIVE - AJOUT TABLE MOBILE_TOURNEECAMION

   Projet : MobileSLI
   Date   : 2026-06-04

   Objectif :
   - Ajouter une table dédiée au camion et aux kilométrages associés
     à une synchronisation mobile.

   Périmètre :
   - Crée dbo.Mobile_TourneeCamion si elle n'existe pas.
   - Ne supprime aucune table.
   - Ne modifie pas dbo.Mobile_Tournee.
   - Ne crée pas de table camion référentielle.
   - Ajoute les contraintes et index attendus s'ils sont absents.

   Pré-requis :
   - La table dbo.Mobile_Tournee doit déjà exister.

   Remarque :
   - Le script s'exécute sur la base SQL Server courante.
   - Vérifier la base sélectionnée avant exécution dans SSMS.
============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Mobile_Tournee', N'U') IS NULL
    BEGIN
        THROW 51000, N'Pré-requis manquant : la table dbo.Mobile_Tournee est introuvable. Migration Mobile_TourneeCamion interrompue.', 1;
    END;

    IF OBJECT_ID(N'dbo.Mobile_TourneeCamion', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Mobile_TourneeCamion (
            IdTourneeCamion BIGINT IDENTITY(1,1) NOT NULL,
            IdTourneeMobile BIGINT NOT NULL,
            IdCamionSource NVARCHAR(50) NOT NULL,
            CodeCamion NVARCHAR(50) NULL,
            LibelleCamion NVARCHAR(150) NULL,
            Immatriculation NVARCHAR(50) NULL,
            KilometrageDepart INT NOT NULL,
            KilometrageArrivee INT NOT NULL,
            DateDepartMobile DATETIMEOFFSET(0) NOT NULL,
            DateArriveeMobile DATETIMEOFFSET(0) NOT NULL,
            DateCreation DATETIMEOFFSET(0) NOT NULL
                CONSTRAINT DF_Mobile_TourneeCamion_DateCreation DEFAULT SYSDATETIMEOFFSET(),

            CONSTRAINT PK_Mobile_TourneeCamion
                PRIMARY KEY (IdTourneeCamion),

            CONSTRAINT FK_Mobile_TourneeCamion_Tournee
                FOREIGN KEY (IdTourneeMobile)
                REFERENCES dbo.Mobile_Tournee(IdTourneeMobile)
                ON DELETE CASCADE,

            CONSTRAINT UQ_Mobile_TourneeCamion_IdTourneeMobile
                UNIQUE (IdTourneeMobile),

            CONSTRAINT CK_Mobile_TourneeCamion_IdCamionSource_NonVide
                CHECK (LEN(LTRIM(RTRIM(IdCamionSource))) > 0),

            CONSTRAINT CK_Mobile_TourneeCamion_KilometrageDepart
                CHECK (KilometrageDepart >= 0),

            CONSTRAINT CK_Mobile_TourneeCamion_KilometrageArrivee
                CHECK (KilometrageArrivee >= 0),

            CONSTRAINT CK_Mobile_TourneeCamion_KilometrageCoherent
                CHECK (KilometrageArrivee >= KilometrageDepart),

            CONSTRAINT CK_Mobile_TourneeCamion_DatesMobiles
                CHECK (DateArriveeMobile >= DateDepartMobile)
        );

        PRINT N'Table dbo.Mobile_TourneeCamion créée.';
    END
    ELSE
    BEGIN
        PRINT N'Table dbo.Mobile_TourneeCamion déjà existante : aucune suppression ni recréation.';
    END;

    IF COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'IdTourneeCamion') IS NULL
        OR COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'IdTourneeMobile') IS NULL
        OR COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'IdCamionSource') IS NULL
        OR COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'CodeCamion') IS NULL
        OR COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'LibelleCamion') IS NULL
        OR COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'Immatriculation') IS NULL
        OR COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'KilometrageDepart') IS NULL
        OR COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'KilometrageArrivee') IS NULL
        OR COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'DateDepartMobile') IS NULL
        OR COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'DateArriveeMobile') IS NULL
        OR COL_LENGTH(N'dbo.Mobile_TourneeCamion', N'DateCreation') IS NULL
    BEGIN
        THROW 51001, N'La table dbo.Mobile_TourneeCamion existe mais sa structure est incomplète. Migration non destructive interrompue pour éviter une correction risquée.', 1;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND name = N'DF_Mobile_TourneeCamion_DateCreation'
    )
    BEGIN
        ALTER TABLE dbo.Mobile_TourneeCamion
        ADD CONSTRAINT DF_Mobile_TourneeCamion_DateCreation
            DEFAULT SYSDATETIMEOFFSET() FOR DateCreation;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND type = N'PK'
          AND name = N'PK_Mobile_TourneeCamion'
    )
    BEGIN
        ALTER TABLE dbo.Mobile_TourneeCamion
        ADD CONSTRAINT PK_Mobile_TourneeCamion
            PRIMARY KEY (IdTourneeCamion);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND name = N'FK_Mobile_TourneeCamion_Tournee'
    )
    BEGIN
        ALTER TABLE dbo.Mobile_TourneeCamion WITH CHECK
        ADD CONSTRAINT FK_Mobile_TourneeCamion_Tournee
            FOREIGN KEY (IdTourneeMobile)
            REFERENCES dbo.Mobile_Tournee(IdTourneeMobile)
            ON DELETE CASCADE;

        ALTER TABLE dbo.Mobile_TourneeCamion
        CHECK CONSTRAINT FK_Mobile_TourneeCamion_Tournee;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND type = N'UQ'
          AND name = N'UQ_Mobile_TourneeCamion_IdTourneeMobile'
    )
    BEGIN
        ALTER TABLE dbo.Mobile_TourneeCamion
        ADD CONSTRAINT UQ_Mobile_TourneeCamion_IdTourneeMobile
            UNIQUE (IdTourneeMobile);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND name = N'CK_Mobile_TourneeCamion_IdCamionSource_NonVide'
    )
    BEGIN
        ALTER TABLE dbo.Mobile_TourneeCamion WITH CHECK
        ADD CONSTRAINT CK_Mobile_TourneeCamion_IdCamionSource_NonVide
            CHECK (LEN(LTRIM(RTRIM(IdCamionSource))) > 0);

        ALTER TABLE dbo.Mobile_TourneeCamion
        CHECK CONSTRAINT CK_Mobile_TourneeCamion_IdCamionSource_NonVide;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND name = N'CK_Mobile_TourneeCamion_KilometrageDepart'
    )
    BEGIN
        ALTER TABLE dbo.Mobile_TourneeCamion WITH CHECK
        ADD CONSTRAINT CK_Mobile_TourneeCamion_KilometrageDepart
            CHECK (KilometrageDepart >= 0);

        ALTER TABLE dbo.Mobile_TourneeCamion
        CHECK CONSTRAINT CK_Mobile_TourneeCamion_KilometrageDepart;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND name = N'CK_Mobile_TourneeCamion_KilometrageArrivee'
    )
    BEGIN
        ALTER TABLE dbo.Mobile_TourneeCamion WITH CHECK
        ADD CONSTRAINT CK_Mobile_TourneeCamion_KilometrageArrivee
            CHECK (KilometrageArrivee >= 0);

        ALTER TABLE dbo.Mobile_TourneeCamion
        CHECK CONSTRAINT CK_Mobile_TourneeCamion_KilometrageArrivee;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND name = N'CK_Mobile_TourneeCamion_KilometrageCoherent'
    )
    BEGIN
        ALTER TABLE dbo.Mobile_TourneeCamion WITH CHECK
        ADD CONSTRAINT CK_Mobile_TourneeCamion_KilometrageCoherent
            CHECK (KilometrageArrivee >= KilometrageDepart);

        ALTER TABLE dbo.Mobile_TourneeCamion
        CHECK CONSTRAINT CK_Mobile_TourneeCamion_KilometrageCoherent;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND name = N'CK_Mobile_TourneeCamion_DatesMobiles'
    )
    BEGIN
        ALTER TABLE dbo.Mobile_TourneeCamion WITH CHECK
        ADD CONSTRAINT CK_Mobile_TourneeCamion_DatesMobiles
            CHECK (DateArriveeMobile >= DateDepartMobile);

        ALTER TABLE dbo.Mobile_TourneeCamion
        CHECK CONSTRAINT CK_Mobile_TourneeCamion_DatesMobiles;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND name = N'IX_Mobile_TourneeCamion_IdCamionSource'
    )
    BEGIN
        CREATE INDEX IX_Mobile_TourneeCamion_IdCamionSource
        ON dbo.Mobile_TourneeCamion (IdCamionSource);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Mobile_TourneeCamion')
          AND name = N'IX_Mobile_TourneeCamion_Immatriculation'
    )
    BEGIN
        CREATE INDEX IX_Mobile_TourneeCamion_Immatriculation
        ON dbo.Mobile_TourneeCamion (Immatriculation);
    END;

    COMMIT TRANSACTION;

    PRINT N'Migration Mobile_TourneeCamion terminée.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
