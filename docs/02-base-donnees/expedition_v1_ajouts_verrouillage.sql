/* ============================================================
   AJOUTS API EXPEDITION - VERROUILLAGE PAR LOT
   Projet MobileSLI

   Objectif :
   - Ajouter le suivi idempotent du POST global de verrouillage.
   - Associer les préparations sauvegardées en BD à un idLotVerrouillage.
   - Conserver la règle : le backend web déclenche, l'API vérifie,
     SQL Server trace.

   À exécuter après le script BDD_sli_v12_corrigee.sql.
   Script non destructif : il ajoute les objets manquants si nécessaire.
============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================
   TABLE : Mobile_ExpeditionLotVerrouillage
   Role :
   Journal technique et métier des lots de verrouillage envoyés
   par l'application web Expédition.

   Cette table permet l'idempotence : un même idLotVerrouillage
   ne doit jamais créer deux sauvegardes définitives.
============================================================ */
IF OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Mobile_ExpeditionLotVerrouillage (
        IdLotVerrouillage UNIQUEIDENTIFIER NOT NULL,
        SchemaVersion NVARCHAR(20) NOT NULL,
        DateTournee DATE NOT NULL,
        DateReceptionApi DATETIMEOFFSET(0) NOT NULL,
        DateDeclenchementWeb DATETIMEOFFSET(0) NULL,
        FuseauHoraireWeb NVARCHAR(100) NULL,
        DateTraitement DATETIMEOFFSET(0) NULL,
        Statut NVARCHAR(30) NOT NULL,
        NombreTournees INT NOT NULL
            CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreTournees DEFAULT 0,
        NombreLignes INT NOT NULL
            CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreLignes DEFAULT 0,
        NombreQuantites INT NOT NULL
            CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreQuantites DEFAULT 0,
        AdresseIP NVARCHAR(50) NULL,
        Message NVARCHAR(1000) NULL,
        DateCreation DATETIMEOFFSET(0) NOT NULL
            CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_DateCreation DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Mobile_ExpeditionLotVerrouillage
            PRIMARY KEY (IdLotVerrouillage),
        CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_Statut
            CHECK (Statut IN (N'EN_COURS', N'SUCCESS', N'ERROR')),
        CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_Nombres
            CHECK (NombreTournees >= 0 AND NombreLignes >= 0 AND NombreQuantites >= 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Mobile_ExpeditionLotVerrouillage_DateTournee'
      AND object_id = OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage')
)
BEGIN
    CREATE INDEX IX_Mobile_ExpeditionLotVerrouillage_DateTournee
    ON dbo.Mobile_ExpeditionLotVerrouillage (DateTournee, DateReceptionApi DESC);
END;
GO

/* ============================================================
   Ajout du rattachement du pré-remplissage à son lot de verrouillage
============================================================ */
IF COL_LENGTH('dbo.Mobile_PreRemplissageTournee', 'IdLotVerrouillage') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_PreRemplissageTournee
    ADD IdLotVerrouillage UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH('dbo.Mobile_PreRemplissageTournee', 'DateReceptionApi') IS NULL
BEGIN
    ALTER TABLE dbo.Mobile_PreRemplissageTournee
    ADD DateReceptionApi DATETIMEOFFSET(0) NULL;
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
    WHERE name = 'IX_Mobile_PreRemplissageTournee_IdLotVerrouillage'
      AND object_id = OBJECT_ID('dbo.Mobile_PreRemplissageTournee')
)
BEGIN
    CREATE INDEX IX_Mobile_PreRemplissageTournee_IdLotVerrouillage
    ON dbo.Mobile_PreRemplissageTournee (IdLotVerrouillage)
    WHERE IdLotVerrouillage IS NOT NULL;
END;
GO

/* ============================================================
   Sécurisation recommandée pour le GET mobile :
   seules les préparations verrouillées doivent être injectées
   dans le chargement mobile.

   Si le repository mobile utilise encore une requête sans filtre,
   ajouter dans la requête GetPreRemplissagesAsync :

       AND p.EstVerrouille = 1

   La présente section ne modifie pas le code C#, elle documente
   la règle à conserver côté API.
============================================================ */
