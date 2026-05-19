/* ============================================================
   BDD SLI - PROJET MOBILE TOURNEE
   Version complete corrigee : 1.3.1

   Objectif :
   - Remplacer completement la structure Mobile_* du projet SLI.
   - Aligner la base SQL Server avec le contrat API/Mobile final
     et avec les routes Expédition :
       GET  /api/expedition/preparations/a-preparer
       POST /api/expedition/preparations/verrouiller
   - Supprimer l'ancien nommage PreRemplissage côté base complete.
   - Créer directement les tables finales :
       Mobile_ExpeditionPreparation
       Mobile_ExpeditionPreparationLigne
       Mobile_ExpeditionPreparationHistorique
   - Conserver les tables de synchronisation mobile, les logs,
     les commentaires exceptionnels et les quantités détaillées.

   IMPORTANT :
   - Script destructif prévu pour un environnement de développement
     ou de test.
   - Il supprime puis recrée les tables Mobile_* et les vues de suivi.
   - Ne pas exécuter sur une base contenant des données à conserver
     sans sauvegarde validée.
   - Les vues et tables ABSSolute ne sont pas modifiées.

   Principes structurants :
   - L'API lit les vues ABSSolute.
   - L'API écrit uniquement dans les tables Mobile_*.
   - Le mobile ne se connecte jamais directement à SQL Server.
   - Mobile_TourneeLigneQuantite est la source principale des
     quantités livrées et récupérées.
   - QuantiteLivreePrevue est nullable :
       NULL = l'expédition n'a rien renseigné ;
       0    = l'expédition a volontairement prévu zéro ;
       > 0  = quantité prévue.
   - Le verrouillage Expédition est idempotent via IdLotVerrouillage.
   - L'anti-doublon mobile métier est DateTournee + CodeTournee.
   - ROLLS_VIDES peut exister côté mobile récupération.
   - ROLLS_VIDES est interdit dans les préparations Expédition actives.
============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================
   SUPPRESSION DES VUES DE SUIVI
============================================================ */
IF OBJECT_ID('dbo.v_Mobile_ExpeditionPreparationSynthese', 'V') IS NOT NULL
    DROP VIEW dbo.v_Mobile_ExpeditionPreparationSynthese;
GO

IF OBJECT_ID('dbo.v_Mobile_ExpeditionPreparationDetail', 'V') IS NOT NULL
    DROP VIEW dbo.v_Mobile_ExpeditionPreparationDetail;
GO

IF OBJECT_ID('dbo.v_Mobile_TotauxTournee', 'V') IS NOT NULL
    DROP VIEW dbo.v_Mobile_TotauxTournee;
GO

IF OBJECT_ID('dbo.v_Mobile_TourneeQuantitesDetaillees', 'V') IS NOT NULL
    DROP VIEW dbo.v_Mobile_TourneeQuantitesDetaillees;
GO

/* ============================================================
   SUPPRESSION DES TABLES MOBILE DANS L'ORDRE DES DEPENDANCES

   Le script nettoie aussi les anciens noms PreRemplissage pour
   éviter les collisions avec une ancienne version de développement.
============================================================ */
IF OBJECT_ID('dbo.Mobile_ExpeditionPreparationHistorique', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ExpeditionPreparationHistorique;

IF OBJECT_ID('dbo.Mobile_ExpeditionPreparationLigne', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ExpeditionPreparationLigne;

IF OBJECT_ID('dbo.Mobile_ExpeditionPreparation', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ExpeditionPreparation;

IF OBJECT_ID('dbo.Mobile_PreRemplissageHistorique', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_PreRemplissageHistorique;

IF OBJECT_ID('dbo.Mobile_PreRemplissageQuantite', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_PreRemplissageQuantite;

IF OBJECT_ID('dbo.Mobile_PreRemplissageTournee', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_PreRemplissageTournee;

IF OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ExpeditionLotVerrouillage;

IF OBJECT_ID('dbo.Mobile_CommentaireExceptionnel', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_CommentaireExceptionnel;

IF OBJECT_ID('dbo.Mobile_TourneeLigneQuantite', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_TourneeLigneQuantite;

IF OBJECT_ID('dbo.Mobile_LogSynchronisation', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_LogSynchronisation;

IF OBJECT_ID('dbo.Mobile_ExportAdmin', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ExportAdmin;

IF OBJECT_ID('dbo.Mobile_TourneeLigne', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_TourneeLigne;

IF OBJECT_ID('dbo.Mobile_Tournee', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_Tournee;

IF OBJECT_ID('dbo.Mobile_ChargementTournee', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ChargementTournee;

IF OBJECT_ID('dbo.Mobile_Livreur', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_Livreur;

IF OBJECT_ID('dbo.Mobile_ArticleSaisissable', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ArticleSaisissable;

IF OBJECT_ID('dbo.Mobile_UtilisateurExpedition', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_UtilisateurExpedition;
GO

/* ============================================================
   TABLE : Mobile_ArticleSaisissable

   Rôle :
   Référentiel des articles gérés par l'application mobile.

   Règles :
   - ROLLS, TAPIS, SACS sont utilisables côté Expédition.
   - ROLLS_VIDES est disponible seulement pour la récupération mobile.
   - La table permet d'ajouter des articles sans modifier la table
     Mobile_TourneeLigneQuantite.
============================================================ */
CREATE TABLE dbo.Mobile_ArticleSaisissable (
    CodeArticle NVARCHAR(50) NOT NULL,
    LibelleArticle NVARCHAR(100) NOT NULL,
    OrdreAffichage INT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_OrdreAffichage DEFAULT 0,
    EstActif BIT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_EstActif DEFAULT 1,
    EstVisibleMobile BIT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_EstVisibleMobile DEFAULT 1,
    EstLivrableMobile BIT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_EstLivrableMobile DEFAULT 1,
    EstRecuperableMobile BIT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_EstRecuperableMobile DEFAULT 1,
    EstUtilisableExpedition BIT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_EstUtilisableExpedition DEFAULT 0,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_ArticleSaisissable
        PRIMARY KEY (CodeArticle),
    CONSTRAINT CK_Mobile_ArticleSaisissable_CodeArticle_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeArticle))) > 0),
    CONSTRAINT CK_Mobile_ArticleSaisissable_LibelleArticle_NonVide
        CHECK (LEN(LTRIM(RTRIM(LibelleArticle))) > 0),
    CONSTRAINT CK_Mobile_ArticleSaisissable_OrdreAffichage
        CHECK (OrdreAffichage >= 0),
    CONSTRAINT CK_Mobile_ArticleSaisissable_Usage
        CHECK (
            EstVisibleMobile = 0
            OR EstLivrableMobile = 1
            OR EstRecuperableMobile = 1
        ),
    CONSTRAINT CK_Mobile_ArticleSaisissable_RollsVides
        CHECK (
            CodeArticle <> N'ROLLS_VIDES'
            OR (
                EstLivrableMobile = 0
                AND EstRecuperableMobile = 1
                AND EstUtilisableExpedition = 0
            )
        )
);
GO

CREATE INDEX IX_Mobile_ArticleSaisissable_ActifOrdre
ON dbo.Mobile_ArticleSaisissable (EstActif, EstVisibleMobile, OrdreAffichage);
GO

CREATE INDEX IX_Mobile_ArticleSaisissable_Expedition
ON dbo.Mobile_ArticleSaisissable (EstUtilisableExpedition, EstActif, OrdreAffichage);
GO

INSERT INTO dbo.Mobile_ArticleSaisissable
    (
        CodeArticle,
        LibelleArticle,
        OrdreAffichage,
        EstActif,
        EstVisibleMobile,
        EstLivrableMobile,
        EstRecuperableMobile,
        EstUtilisableExpedition
    )
VALUES
    (N'ROLLS',       N'Rolls',       1, 1, 1, 1, 1, 1),
    (N'ROLLS_VIDES', N'Rolls vides', 2, 1, 1, 0, 1, 0),
    (N'TAPIS',       N'Tapis',       3, 1, 1, 1, 1, 1),
    (N'SACS',        N'Sacs',        4, 1, 1, 1, 1, 1);
GO

/* ============================================================
   TABLE : Mobile_UtilisateurExpedition

   Rôle :
   Comptes applicatifs de l'interface Expédition.

   Sécurité :
   - Ne jamais stocker de mot de passe en clair.
   - MotDePasseHash doit contenir un hash produit par l'application.
   - Les vrais secrets ne doivent pas être placés dans ce script.
============================================================ */
CREATE TABLE dbo.Mobile_UtilisateurExpedition (
    IdUtilisateurExpedition INT IDENTITY(1,1) NOT NULL,
    Identifiant NVARCHAR(100) NOT NULL,
    NomAffiche NVARCHAR(150) NOT NULL,
    MotDePasseHash NVARCHAR(500) NULL,
    RoleUtilisateur NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_UtilisateurExpedition_Role DEFAULT N'EXPEDITION',
    EstActif BIT NOT NULL
        CONSTRAINT DF_Mobile_UtilisateurExpedition_EstActif DEFAULT 1,
    DerniereConnexion DATETIMEOFFSET(0) NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_UtilisateurExpedition_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_UtilisateurExpedition
        PRIMARY KEY (IdUtilisateurExpedition),
    CONSTRAINT UQ_Mobile_UtilisateurExpedition_Identifiant
        UNIQUE (Identifiant),
    CONSTRAINT CK_Mobile_UtilisateurExpedition_Identifiant_NonVide
        CHECK (LEN(LTRIM(RTRIM(Identifiant))) > 0),
    CONSTRAINT CK_Mobile_UtilisateurExpedition_NomAffiche_NonVide
        CHECK (LEN(LTRIM(RTRIM(NomAffiche))) > 0),
    CONSTRAINT CK_Mobile_UtilisateurExpedition_Role
        CHECK (RoleUtilisateur IN (N'EXPEDITION', N'ADMIN', N'INFORMATIQUE'))
);
GO

CREATE INDEX IX_Mobile_UtilisateurExpedition_EstActif
ON dbo.Mobile_UtilisateurExpedition (EstActif, Identifiant);
GO

/* ============================================================
   TABLE : Mobile_Livreur

   Rôle :
   Référentiel des livreurs/chauffeurs côté application mobile.

   Source métier :
   - v_chauffeurs.DRIVERNUMERO -> CodeLivreur
   - v_chauffeurs.DRIVERNAME    -> NomLivreur
============================================================ */
CREATE TABLE dbo.Mobile_Livreur (
    IdLivreur INT IDENTITY(1,1) NOT NULL,
    CodeLivreur NVARCHAR(50) NOT NULL,
    NomLivreur NVARCHAR(100) NOT NULL,
    EstActif BIT NOT NULL
        CONSTRAINT DF_Mobile_Livreur_EstActif DEFAULT 1,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_Livreur_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_Livreur
        PRIMARY KEY (IdLivreur),
    CONSTRAINT UQ_Mobile_Livreur_CodeLivreur
        UNIQUE (CodeLivreur),
    CONSTRAINT CK_Mobile_Livreur_CodeLivreur_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeLivreur))) > 0),
    CONSTRAINT CK_Mobile_Livreur_NomLivreur_NonVide
        CHECK (LEN(LTRIM(RTRIM(NomLivreur))) > 0)
);
GO

CREATE INDEX IX_Mobile_Livreur_EstActif
ON dbo.Mobile_Livreur (EstActif, CodeLivreur);
GO

/* ============================================================
   TABLE : Mobile_ChargementTournee

   Rôle :
   Historiser les chargements de tournée effectués par le mobile.
============================================================ */
CREATE TABLE dbo.Mobile_ChargementTournee (
    IdChargement BIGINT IDENTITY(1,1) NOT NULL,
    SchemaVersion NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Mobile_ChargementTournee_SchemaVersion DEFAULT N'1.3',
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    IdLivreur INT NOT NULL,
    DateChargement DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ChargementTournee_DateChargement DEFAULT SYSDATETIMEOFFSET(),
    NombrePointsEnvoyes INT NULL,
    NomAppareil NVARCHAR(100) NULL,
    VersionApplication NVARCHAR(50) NULL,
    AdresseIP NVARCHAR(50) NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ChargementTournee_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_Mobile_ChargementTournee
        PRIMARY KEY (IdChargement),
    CONSTRAINT FK_Mobile_ChargementTournee_Livreur
        FOREIGN KEY (IdLivreur)
        REFERENCES dbo.Mobile_Livreur(IdLivreur),
    CONSTRAINT CK_Mobile_ChargementTournee_SchemaVersion_NonVide
        CHECK (LEN(LTRIM(RTRIM(SchemaVersion))) > 0),
    CONSTRAINT CK_Mobile_ChargementTournee_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_ChargementTournee_NombrePoints
        CHECK (NombrePointsEnvoyes IS NULL OR NombrePointsEnvoyes >= 0)
);
GO

CREATE INDEX IX_Mobile_ChargementTournee_DateTournee
ON dbo.Mobile_ChargementTournee (DateTournee, CodeTournee);
GO

CREATE INDEX IX_Mobile_ChargementTournee_Livreur
ON dbo.Mobile_ChargementTournee (IdLivreur);
GO

/* ============================================================
   TABLE : Mobile_Tournee

   Rôle :
   En-tête d'une synchronisation de tournée mobile.

   Protections :
   - UQ_Mobile_Tournee_IdSynchronisation : anti-rejeu technique.
   - UX_Mobile_Tournee_EnvoiUnique : anti-double envoi métier.
     Une seule tournée ENVOYEE est autorisée par DateTournee + CodeTournee.
============================================================ */
CREATE TABLE dbo.Mobile_Tournee (
    IdTourneeMobile BIGINT IDENTITY(1,1) NOT NULL,
    SchemaVersion NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Mobile_Tournee_SchemaVersion DEFAULT N'1.3',
    IdSynchronisation UNIQUEIDENTIFIER NOT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    IdLivreur INT NOT NULL,
    StatutSynchronisation NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_Tournee_StatutSynchronisation DEFAULT N'EN_ATTENTE',
    DateChargementMobile DATETIMEOFFSET(0) NULL,
    DateReceptionApi DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_Tournee_DateReceptionApi DEFAULT SYSDATETIMEOFFSET(),
    DateEnvoi DATETIMEOFFSET(0) NULL,
    EstVerrouillee BIT NOT NULL
        CONSTRAINT DF_Mobile_Tournee_EstVerrouillee DEFAULT 0,
    NombrePointsPrevus INT NULL,
    NombrePointsSaisis INT NULL,
    CommentaireGlobal NVARCHAR(1000) NULL,
    NomAppareil NVARCHAR(100) NULL,
    VersionApplication NVARCHAR(50) NULL,
    AdresseIP NVARCHAR(50) NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_Tournee_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_Tournee
        PRIMARY KEY (IdTourneeMobile),
    CONSTRAINT FK_Mobile_Tournee_Livreur
        FOREIGN KEY (IdLivreur)
        REFERENCES dbo.Mobile_Livreur(IdLivreur),
    CONSTRAINT UQ_Mobile_Tournee_IdSynchronisation
        UNIQUE (IdSynchronisation),
    CONSTRAINT CK_Mobile_Tournee_SchemaVersion_NonVide
        CHECK (LEN(LTRIM(RTRIM(SchemaVersion))) > 0),
    CONSTRAINT CK_Mobile_Tournee_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_Tournee_StatutSynchronisation
        CHECK (StatutSynchronisation IN (
            N'NON_ENVOYEE',
            N'EN_ATTENTE',
            N'ENVOYEE',
            N'ERREUR_ENVOI',
            N'ANNULEE'
        )),
    CONSTRAINT CK_Mobile_Tournee_Verrouillage
        CHECK (
            (
                StatutSynchronisation = N'ENVOYEE'
                AND EstVerrouillee = 1
                AND DateEnvoi IS NOT NULL
            )
            OR
            (
                StatutSynchronisation <> N'ENVOYEE'
            )
        ),
    CONSTRAINT CK_Mobile_Tournee_NombrePointsPrevus
        CHECK (NombrePointsPrevus IS NULL OR NombrePointsPrevus >= 0),
    CONSTRAINT CK_Mobile_Tournee_NombrePointsSaisis
        CHECK (NombrePointsSaisis IS NULL OR NombrePointsSaisis >= 0)
);
GO

CREATE UNIQUE INDEX UX_Mobile_Tournee_EnvoiUnique
ON dbo.Mobile_Tournee (DateTournee, CodeTournee)
WHERE StatutSynchronisation = N'ENVOYEE';
GO

CREATE INDEX IX_Mobile_Tournee_DateTournee
ON dbo.Mobile_Tournee (DateTournee, CodeTournee);
GO

CREATE INDEX IX_Mobile_Tournee_Livreur
ON dbo.Mobile_Tournee (IdLivreur);
GO

CREATE INDEX IX_Mobile_Tournee_Statut
ON dbo.Mobile_Tournee (StatutSynchronisation, DateTournee, CodeTournee);
GO

/* ============================================================
   TABLE : Mobile_TourneeLigne

   Rôle :
   Ligne client / point de livraison / arrêt d'une tournée.

   Cette table conserve :
   - un snapshot des données ABSSolute utiles au mobile ;
   - un snapshot du commentaire exceptionnel envoyé au mobile ;
   - les données terrain saisies par le livreur ;
   - les totaux de compatibilité calculés depuis les quantités.
============================================================ */
CREATE TABLE dbo.Mobile_TourneeLigne (
    IdTourneeLigne BIGINT IDENTITY(1,1) NOT NULL,
    IdTourneeMobile BIGINT NOT NULL,
    IdLigneSource NVARCHAR(300) NOT NULL,

    NumClient NVARCHAR(50) NOT NULL,
    NomClient NVARCHAR(255) NOT NULL,
    NomAffiche NVARCHAR(255) NULL,
    CodePDL NVARCHAR(50) NULL,
    DescriptionPDL NVARCHAR(255) NULL,

    AdresseLigne1 NVARCHAR(255) NULL,
    AdresseLigne2 NVARCHAR(255) NULL,
    AdresseLigne3 NVARCHAR(255) NULL,
    Ville NVARCHAR(100) NULL,
    CodePostal NVARCHAR(20) NULL,

    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    JourTournee INT NULL,
    JourLibelle NVARCHAR(30) NULL,
    SchemaLivraison NVARCHAR(100) NULL,
    OrdreArret INT NULL,
    Horaire NVARCHAR(50) NULL,

    JourTourneeRetour INT NULL,
    JourRetourLibelle NVARCHAR(30) NULL,
    CodeTourneeRetour NVARCHAR(50) NULL,
    LibelleTourneeRetour NVARCHAR(255) NULL,

    Instructions NVARCHAR(1000) NULL,
    CommentaireExceptionnel NVARCHAR(1000) NULL,
    ZoneDechargement NVARCHAR(100) NULL,
    ZoneDechargementAffichee NVARCHAR(150) NULL,
    Zone NVARCHAR(100) NULL,
    PrecisionInfo NVARCHAR(1000) NULL,
    Cle NVARCHAR(100) NULL,
    TypeLinge NVARCHAR(100) NULL,

    EstFerme BIT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_EstFerme DEFAULT 0,
    DateFermeture DATE NULL,
    MotifFermeture NVARCHAR(255) NULL,

    QuantiteLivree INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_QuantiteLivree DEFAULT 0,
    QuantiteReprise INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_QuantiteReprise DEFAULT 0,

    NbExpes INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbExpes DEFAULT 0,
    NbRolls INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbRolls DEFAULT 0,
    NbVetements INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbVetements DEFAULT 0,
    NbTapis INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbTapis DEFAULT 0,
    NbSacs INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbSacs DEFAULT 0,
    NbRecuperes INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbRecuperes DEFAULT 0,

    PrecisionLivreur NVARCHAR(1000) NULL,
    StatutPassage NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_StatutPassage DEFAULT N'A_FAIRE',
    CommentaireLivreur NVARCHAR(1000) NULL,
    HeureValidation DATETIMEOFFSET(0) NULL,
    EstValidee BIT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_EstValidee DEFAULT 0,

    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,

    CONSTRAINT PK_Mobile_TourneeLigne
        PRIMARY KEY (IdTourneeLigne),
    CONSTRAINT FK_Mobile_TourneeLigne_Tournee
        FOREIGN KEY (IdTourneeMobile)
        REFERENCES dbo.Mobile_Tournee(IdTourneeMobile)
        ON DELETE CASCADE,
    CONSTRAINT CK_Mobile_TourneeLigne_IdLigneSource_NonVide
        CHECK (LEN(LTRIM(RTRIM(IdLigneSource))) > 0),
    CONSTRAINT CK_Mobile_TourneeLigne_NumClient_NonVide
        CHECK (LEN(LTRIM(RTRIM(NumClient))) > 0),
    CONSTRAINT CK_Mobile_TourneeLigne_NomClient_NonVide
        CHECK (LEN(LTRIM(RTRIM(NomClient))) > 0),
    CONSTRAINT CK_Mobile_TourneeLigne_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_TourneeLigne_JourTournee
        CHECK (JourTournee IS NULL OR JourTournee BETWEEN 1 AND 7),
    CONSTRAINT CK_Mobile_TourneeLigne_JourTourneeRetour
        CHECK (JourTourneeRetour IS NULL OR JourTourneeRetour BETWEEN 1 AND 7),
    CONSTRAINT CK_Mobile_TourneeLigne_OrdreArret
        CHECK (OrdreArret IS NULL OR OrdreArret >= 0),
    CONSTRAINT CK_Mobile_TourneeLigne_QuantiteLivree
        CHECK (QuantiteLivree >= 0),
    CONSTRAINT CK_Mobile_TourneeLigne_QuantiteReprise
        CHECK (QuantiteReprise >= 0),
    CONSTRAINT CK_Mobile_TourneeLigne_QuantitesCompatibilite
        CHECK (
            NbExpes >= 0
            AND NbRolls >= 0
            AND NbVetements >= 0
            AND NbTapis >= 0
            AND NbSacs >= 0
            AND NbRecuperes >= 0
        ),
    CONSTRAINT CK_Mobile_TourneeLigne_StatutPassage
        CHECK (StatutPassage IN (
            N'A_FAIRE',
            N'FAIT',
            N'NON_FAIT',
            N'ANOMALIE'
        )),
    CONSTRAINT CK_Mobile_TourneeLigne_CommentaireObligatoire
        CHECK (
            StatutPassage NOT IN (N'NON_FAIT', N'ANOMALIE')
            OR
            (
                CommentaireLivreur IS NOT NULL
                AND LEN(LTRIM(RTRIM(CommentaireLivreur))) > 0
            )
        ),
    CONSTRAINT CK_Mobile_TourneeLigne_Validation
        CHECK (
            (
                EstValidee = 0
                AND HeureValidation IS NULL
            )
            OR
            (
                EstValidee = 1
                AND HeureValidation IS NOT NULL
            )
        ),
    CONSTRAINT CK_Mobile_TourneeLigne_Fermeture
        CHECK (
            (
                EstFerme = 0
                AND DateFermeture IS NULL
            )
            OR
            (
                EstFerme = 1
            )
        )
);
GO

CREATE UNIQUE INDEX UX_Mobile_TourneeLigne_IdLigneSource
ON dbo.Mobile_TourneeLigne (IdTourneeMobile, IdLigneSource);
GO

CREATE INDEX IX_Mobile_TourneeLigne_TourneeOrdre
ON dbo.Mobile_TourneeLigne (IdTourneeMobile, OrdreArret);
GO

CREATE INDEX IX_Mobile_TourneeLigne_ClientPDL
ON dbo.Mobile_TourneeLigne (IdTourneeMobile, NumClient, CodePDL);
GO

CREATE INDEX IX_Mobile_TourneeLigne_StatutPassage
ON dbo.Mobile_TourneeLigne (StatutPassage);
GO

CREATE INDEX IX_Mobile_TourneeLigne_ZoneDechargement
ON dbo.Mobile_TourneeLigne (ZoneDechargement, JourTourneeRetour);
GO

/* ============================================================
   TABLE : Mobile_TourneeLigneQuantite

   Rôle :
   Source principale des quantités par article pour une ligne
   de tournée synchronisée.
============================================================ */
CREATE TABLE dbo.Mobile_TourneeLigneQuantite (
    IdQuantite BIGINT IDENTITY(1,1) NOT NULL,
    IdTourneeLigne BIGINT NOT NULL,
    CodeArticle NVARCHAR(50) NOT NULL,
    LibelleArticle NVARCHAR(100) NULL,
    QuantiteLivreePrevue INT NULL,
    QuantiteLivree INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigneQuantite_QuantiteLivree DEFAULT 0,
    QuantiteRecuperee INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigneQuantite_QuantiteRecuperee DEFAULT 0,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigneQuantite_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_TourneeLigneQuantite
        PRIMARY KEY (IdQuantite),
    CONSTRAINT FK_Mobile_TourneeLigneQuantite_Ligne
        FOREIGN KEY (IdTourneeLigne)
        REFERENCES dbo.Mobile_TourneeLigne(IdTourneeLigne)
        ON DELETE CASCADE,
    CONSTRAINT FK_Mobile_TourneeLigneQuantite_Article
        FOREIGN KEY (CodeArticle)
        REFERENCES dbo.Mobile_ArticleSaisissable(CodeArticle),
    CONSTRAINT CK_Mobile_TourneeLigneQuantite_CodeArticle_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeArticle))) > 0),
    CONSTRAINT CK_Mobile_TourneeLigneQuantite_Quantites
        CHECK (
            (QuantiteLivreePrevue IS NULL OR QuantiteLivreePrevue >= 0)
            AND QuantiteLivree >= 0
            AND QuantiteRecuperee >= 0
        )
);
GO

CREATE UNIQUE INDEX UX_Mobile_TourneeLigneQuantite_Article
ON dbo.Mobile_TourneeLigneQuantite (IdTourneeLigne, CodeArticle);
GO

CREATE INDEX IX_Mobile_TourneeLigneQuantite_Ligne
ON dbo.Mobile_TourneeLigneQuantite (IdTourneeLigne);
GO

CREATE INDEX IX_Mobile_TourneeLigneQuantite_CodeArticle
ON dbo.Mobile_TourneeLigneQuantite (CodeArticle);
GO

/* ============================================================
   TABLE : Mobile_ExpeditionLotVerrouillage

   Rôle :
   Journal technique des POST de verrouillage Expédition.

   Objectifs :
   - IdLotVerrouillage rend le POST idempotent.
   - EmpreintePayload permet de refuser le rejeu d'un même lot
     avec un contenu différent.
   - La table journalise le lot sans dépendre circulairement de
     Mobile_ExpeditionPreparation.
============================================================ */
CREATE TABLE dbo.Mobile_ExpeditionLotVerrouillage (
    IdLotVerrouillage UNIQUEIDENTIFIER NOT NULL,
    EmpreintePayload CHAR(64) NOT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    StatutLot NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_StatutLot DEFAULT N'VERROUILLE',
    NombrePreparations INT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombrePreparations DEFAULT 0,
    NombreLignes INT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreLignes DEFAULT 0,
    NombreQuantites INT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreQuantites DEFAULT 0,
    AdresseIP NVARCHAR(50) NULL,
    NomAppareil NVARCHAR(100) NULL,
    VersionApplication NVARCHAR(50) NULL,
    MessageRetour NVARCHAR(1000) NULL,
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
        CHECK (NombrePreparations >= 0 AND NombreLignes >= 0 AND NombreQuantites >= 0)
);
GO

CREATE UNIQUE INDEX UX_Mobile_ExpeditionLotVerrouillage_DateCodeEmpreinte
ON dbo.Mobile_ExpeditionLotVerrouillage (DateTournee, CodeTournee, EmpreintePayload);
GO

CREATE INDEX IX_Mobile_ExpeditionLotVerrouillage_DateCode
ON dbo.Mobile_ExpeditionLotVerrouillage (DateTournee, CodeTournee);
GO

/* ============================================================
   TABLE : Mobile_ExpeditionPreparation

   Rôle :
   En-tête d'une préparation Expédition pour une date et une tournée.

   Cycle :
   - BROUILLON    : saisie encore modifiable côté interface web.
   - VERROUILLEE : préparation figée, consommable par GET mobile.
   - ANNULEE     : préparation invalidée.
============================================================ */
CREATE TABLE dbo.Mobile_ExpeditionPreparation (
    IdPreparationExpedition BIGINT IDENTITY(1,1) NOT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    StatutPreparation NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparation_StatutPreparation DEFAULT N'BROUILLON',
    EstVerrouille BIT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparation_EstVerrouille DEFAULT 0,
    DateVerrouillage DATETIMEOFFSET(0) NULL,
    IdLotVerrouillage UNIQUEIDENTIFIER NULL,
    EmpreintePayload CHAR(64) NULL,
    IdUtilisateurCreation INT NULL,
    IdUtilisateurModification INT NULL,
    IdUtilisateurVerrouillage INT NULL,
    AdresseIPCreation NVARCHAR(50) NULL,
    AdresseIPModification NVARCHAR(50) NULL,
    AdresseIPVerrouillage NVARCHAR(50) NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparation_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_ExpeditionPreparation
        PRIMARY KEY (IdPreparationExpedition),
    CONSTRAINT FK_Mobile_ExpeditionPreparation_LotVerrouillage
        FOREIGN KEY (IdLotVerrouillage)
        REFERENCES dbo.Mobile_ExpeditionLotVerrouillage(IdLotVerrouillage),
    CONSTRAINT FK_Mobile_ExpeditionPreparation_UtilisateurCreation
        FOREIGN KEY (IdUtilisateurCreation)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT FK_Mobile_ExpeditionPreparation_UtilisateurModification
        FOREIGN KEY (IdUtilisateurModification)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT FK_Mobile_ExpeditionPreparation_UtilisateurVerrouillage
        FOREIGN KEY (IdUtilisateurVerrouillage)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT UQ_Mobile_ExpeditionPreparation_DateCode
        UNIQUE (DateTournee, CodeTournee),
    CONSTRAINT CK_Mobile_ExpeditionPreparation_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparation_Statut
        CHECK (StatutPreparation IN (N'BROUILLON', N'VERROUILLEE', N'ANNULEE')),
    CONSTRAINT CK_Mobile_ExpeditionPreparation_EmpreintePayload
        CHECK (EmpreintePayload IS NULL OR LEN(LTRIM(RTRIM(EmpreintePayload))) = 64),
    CONSTRAINT CK_Mobile_ExpeditionPreparation_Verrouillage
        CHECK (
            (
                EstVerrouille = 0
                AND DateVerrouillage IS NULL
                AND IdLotVerrouillage IS NULL
                AND StatutPreparation IN (N'BROUILLON', N'ANNULEE')
            )
            OR
            (
                EstVerrouille = 1
                AND DateVerrouillage IS NOT NULL
                AND IdLotVerrouillage IS NOT NULL
                AND StatutPreparation = N'VERROUILLEE'
            )
        )
);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparation_Date
ON dbo.Mobile_ExpeditionPreparation (DateTournee, CodeTournee);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparation_Verrouillage
ON dbo.Mobile_ExpeditionPreparation (EstVerrouille, DateTournee, CodeTournee);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparation_Lot
ON dbo.Mobile_ExpeditionPreparation (IdLotVerrouillage);
GO

CREATE UNIQUE INDEX UX_Mobile_ExpeditionPreparation_LotUnique
ON dbo.Mobile_ExpeditionPreparation (IdLotVerrouillage)
WHERE IdLotVerrouillage IS NOT NULL;
GO

/* ============================================================
   TABLE : Mobile_ExpeditionPreparationLigne

   Rôle :
   Quantités prévues par l'Expédition, par ligne métier et par article.
   Ces valeurs alimentent quantites[].quantiteLivreePrevue
   dans le JSON de chargement mobile.

   Règles :
   - La ligne appartient à une préparation.
   - Les préparations actives n'acceptent que ROLLS, TAPIS et SACS.
   - ROLLS_VIDES reste exclu du module Expédition.
============================================================ */
CREATE TABLE dbo.Mobile_ExpeditionPreparationLigne (
    IdPreparationExpeditionLigne BIGINT IDENTITY(1,1) NOT NULL,
    IdPreparationExpedition BIGINT NOT NULL,
    IdLigneSource NVARCHAR(300) NOT NULL,
    OrdreArret INT NULL,
    NumClient NVARCHAR(50) NOT NULL,
    NomClient NVARCHAR(255) NULL,
    NomAffiche NVARCHAR(255) NULL,
    CodePDL NVARCHAR(50) NULL,
    DescriptionPDL NVARCHAR(255) NULL,
    CodeArticle NVARCHAR(50) NOT NULL,
    LibelleArticle NVARCHAR(100) NULL,
    QuantiteLivreePrevue INT NULL,
    Actif BIT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparationLigne_Actif DEFAULT 1,
    IdUtilisateurCreation INT NULL,
    IdUtilisateurModification INT NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparationLigne_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_ExpeditionPreparationLigne
        PRIMARY KEY (IdPreparationExpeditionLigne),
    CONSTRAINT FK_Mobile_ExpeditionPreparationLigne_Preparation
        FOREIGN KEY (IdPreparationExpedition)
        REFERENCES dbo.Mobile_ExpeditionPreparation(IdPreparationExpedition)
        ON DELETE CASCADE,
    CONSTRAINT FK_Mobile_ExpeditionPreparationLigne_Article
        FOREIGN KEY (CodeArticle)
        REFERENCES dbo.Mobile_ArticleSaisissable(CodeArticle),
    CONSTRAINT FK_Mobile_ExpeditionPreparationLigne_UtilisateurCreation
        FOREIGN KEY (IdUtilisateurCreation)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT FK_Mobile_ExpeditionPreparationLigne_UtilisateurModification
        FOREIGN KEY (IdUtilisateurModification)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_IdLigneSource_NonVide
        CHECK (LEN(LTRIM(RTRIM(IdLigneSource))) > 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_NumClient_NonVide
        CHECK (LEN(LTRIM(RTRIM(NumClient))) > 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_CodeArticle_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeArticle))) > 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_ArticleAutorise
        CHECK (CodeArticle IN (N'ROLLS', N'TAPIS', N'SACS')),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_OrdreArret
        CHECK (OrdreArret IS NULL OR OrdreArret >= 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_Quantite
        CHECK (QuantiteLivreePrevue IS NULL OR QuantiteLivreePrevue >= 0)
);
GO

CREATE UNIQUE INDEX UX_Mobile_ExpeditionPreparationLigne_LigneArticle_Actif
ON dbo.Mobile_ExpeditionPreparationLigne (IdPreparationExpedition, IdLigneSource, CodeArticle)
WHERE Actif = 1;
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationLigne_ClientPDL
ON dbo.Mobile_ExpeditionPreparationLigne (IdPreparationExpedition, NumClient, CodePDL);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationLigne_Article
ON dbo.Mobile_ExpeditionPreparationLigne (CodeArticle);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationLigne_LigneSource
ON dbo.Mobile_ExpeditionPreparationLigne (IdLigneSource);
GO

/* ============================================================
   TABLE : Mobile_ExpeditionPreparationHistorique

   Rôle :
   Historique des créations, modifications, suppressions,
   verrouillages et tentatives de modification après verrouillage.
============================================================ */
CREATE TABLE dbo.Mobile_ExpeditionPreparationHistorique (
    IdHistorique BIGINT IDENTITY(1,1) NOT NULL,
    IdPreparationExpedition BIGINT NULL,
    IdPreparationExpeditionLigne BIGINT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    IdLigneSource NVARCHAR(300) NULL,
    NumClient NVARCHAR(50) NULL,
    CodePDL NVARCHAR(50) NULL,
    CodeArticle NVARCHAR(50) NULL,
    AncienneQuantiteLivreePrevue INT NULL,
    NouvelleQuantiteLivreePrevue INT NULL,
    ActionHistorique NVARCHAR(50) NOT NULL,
    IdUtilisateur INT NULL,
    Commentaire NVARCHAR(1000) NULL,
    AdresseIP NVARCHAR(50) NULL,
    DateEvenement DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparationHistorique_DateEvenement DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_Mobile_ExpeditionPreparationHistorique
        PRIMARY KEY (IdHistorique),
    CONSTRAINT FK_Mobile_ExpeditionPreparationHistorique_Preparation
        FOREIGN KEY (IdPreparationExpedition)
        REFERENCES dbo.Mobile_ExpeditionPreparation(IdPreparationExpedition),
    CONSTRAINT FK_Mobile_ExpeditionPreparationHistorique_Ligne
        FOREIGN KEY (IdPreparationExpeditionLigne)
        REFERENCES dbo.Mobile_ExpeditionPreparationLigne(IdPreparationExpeditionLigne),
    CONSTRAINT FK_Mobile_ExpeditionPreparationHistorique_Utilisateur
        FOREIGN KEY (IdUtilisateur)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT CK_Mobile_ExpeditionPreparationHistorique_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationHistorique_Action
        CHECK (ActionHistorique IN (
            N'CREATION',
            N'MODIFICATION',
            N'SUPPRESSION',
            N'VERROUILLAGE',
            N'REJEU_VERROUILLAGE_IDENTIQUE',
            N'TENTATIVE_MODIFICATION_APRES_VERROUILLAGE',
            N'REFUS_VERROUILLAGE'
        )),
    CONSTRAINT CK_Mobile_ExpeditionPreparationHistorique_AncienneQuantite
        CHECK (AncienneQuantiteLivreePrevue IS NULL OR AncienneQuantiteLivreePrevue >= 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationHistorique_NouvelleQuantite
        CHECK (NouvelleQuantiteLivreePrevue IS NULL OR NouvelleQuantiteLivreePrevue >= 0)
);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationHistorique_DateTournee
ON dbo.Mobile_ExpeditionPreparationHistorique (DateTournee, CodeTournee, DateEvenement DESC);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationHistorique_Utilisateur
ON dbo.Mobile_ExpeditionPreparationHistorique (IdUtilisateur, DateEvenement DESC);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationHistorique_Preparation
ON dbo.Mobile_ExpeditionPreparationHistorique (IdPreparationExpedition, DateEvenement DESC);
GO

/* ============================================================
   TABLE : Mobile_CommentaireExceptionnel

   Rôle :
   Commentaires propres au projet mobile, liés à une date et
   à un client, éventuellement à un point de livraison ou une ligne.
============================================================ */
CREATE TABLE dbo.Mobile_CommentaireExceptionnel (
    IdCommentaireExceptionnel BIGINT IDENTITY(1,1) NOT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NULL,
    IdLigneSource NVARCHAR(300) NULL,
    NumClient NVARCHAR(50) NOT NULL,
    CodePDL NVARCHAR(50) NULL,
    Commentaire NVARCHAR(1000) NOT NULL,
    Actif BIT NOT NULL
        CONSTRAINT DF_Mobile_CommentaireExceptionnel_Actif DEFAULT 1,
    CreePar NVARCHAR(100) NULL,
    ModifiePar NVARCHAR(100) NULL,
    IdUtilisateurCreation INT NULL,
    IdUtilisateurModification INT NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_CommentaireExceptionnel_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_CommentaireExceptionnel
        PRIMARY KEY (IdCommentaireExceptionnel),
    CONSTRAINT FK_Mobile_CommentaireExceptionnel_UtilisateurCreation
        FOREIGN KEY (IdUtilisateurCreation)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT FK_Mobile_CommentaireExceptionnel_UtilisateurModification
        FOREIGN KEY (IdUtilisateurModification)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT CK_Mobile_CommentaireExceptionnel_CodeTournee_NonVide
        CHECK (CodeTournee IS NULL OR LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_CommentaireExceptionnel_IdLigneSource_NonVide
        CHECK (IdLigneSource IS NULL OR LEN(LTRIM(RTRIM(IdLigneSource))) > 0),
    CONSTRAINT CK_Mobile_CommentaireExceptionnel_NumClient_NonVide
        CHECK (LEN(LTRIM(RTRIM(NumClient))) > 0),
    CONSTRAINT CK_Mobile_CommentaireExceptionnel_Commentaire_NonVide
        CHECK (LEN(LTRIM(RTRIM(Commentaire))) > 0)
);
GO

CREATE UNIQUE INDEX UX_Mobile_CommentaireExceptionnel_Ligne_Actif
ON dbo.Mobile_CommentaireExceptionnel (DateTournee, CodeTournee, IdLigneSource)
WHERE Actif = 1 AND IdLigneSource IS NOT NULL;
GO

CREATE UNIQUE INDEX UX_Mobile_CommentaireExceptionnel_ClientPDL_Actif
ON dbo.Mobile_CommentaireExceptionnel (DateTournee, CodeTournee, NumClient, CodePDL)
WHERE Actif = 1 AND IdLigneSource IS NULL;
GO

CREATE INDEX IX_Mobile_CommentaireExceptionnel_DateClient
ON dbo.Mobile_CommentaireExceptionnel (DateTournee, CodeTournee, NumClient, CodePDL, Actif);
GO

CREATE INDEX IX_Mobile_CommentaireExceptionnel_IdLigneSource
ON dbo.Mobile_CommentaireExceptionnel (DateTournee, CodeTournee, IdLigneSource, Actif);
GO

/* ============================================================
   TABLE : Mobile_LogSynchronisation

   Rôle :
   Journaliser les événements techniques et métier importants.
============================================================ */
CREATE TABLE dbo.Mobile_LogSynchronisation (
    IdLog BIGINT IDENTITY(1,1) NOT NULL,
    IdTourneeMobile BIGINT NULL,
    IdLivreur INT NULL,
    IdSynchronisation UNIQUEIDENTIFIER NULL,
    DateEvenement DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_LogSynchronisation_DateEvenement DEFAULT SYSDATETIMEOFFSET(),
    TypeEvenement NVARCHAR(70) NOT NULL,
    Niveau NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Mobile_LogSynchronisation_Niveau DEFAULT N'INFO',
    Message NVARCHAR(1000) NOT NULL,
    DetailTechnique NVARCHAR(MAX) NULL,
    AdresseIP NVARCHAR(50) NULL,
    NomAppareil NVARCHAR(100) NULL,
    VersionApplication NVARCHAR(50) NULL,
    CONSTRAINT PK_Mobile_LogSynchronisation
        PRIMARY KEY (IdLog),
    CONSTRAINT FK_Mobile_LogSynchronisation_Tournee
        FOREIGN KEY (IdTourneeMobile)
        REFERENCES dbo.Mobile_Tournee(IdTourneeMobile)
        ON DELETE SET NULL,
    CONSTRAINT FK_Mobile_LogSynchronisation_Livreur
        FOREIGN KEY (IdLivreur)
        REFERENCES dbo.Mobile_Livreur(IdLivreur),
    CONSTRAINT CK_Mobile_LogSynchronisation_Niveau
        CHECK (Niveau IN (N'INFO', N'WARNING', N'ERROR')),
    CONSTRAINT CK_Mobile_LogSynchronisation_TypeEvenement
        CHECK (TypeEvenement IN (
            N'CHARGEMENT_TOURNEE',
            N'ENVOI_TOURNEE',
            N'ENVOI_REUSSI',
            N'ERREUR_ENVOI',
            N'DOUBLE_ENVOI',
            N'ERREUR_SQL',
            N'VALIDATION_API',
            N'ERREUR_VALIDATION',
            N'EXPORT_ADMIN',
            N'TOURNEE_VERROUILLEE',
            N'LECTURE_VUE_ABSSOLUTE',
            N'ERREUR_LECTURE_VUE',
            N'CONSULTATION_ADMIN',
            N'CORRECTION_ADMIN',
            N'CHARGEMENT_PRE_REMPLISSAGE',
            N'CREATION_PRE_REMPLISSAGE',
            N'MODIFICATION_PRE_REMPLISSAGE',
            N'BLOCAGE_PRE_REMPLISSAGE',
            N'ERREUR_PRE_REMPLISSAGE',
            N'CHARGEMENT_EXPEDITION_PREPARATION',
            N'CREATION_EXPEDITION_PREPARATION',
            N'MODIFICATION_EXPEDITION_PREPARATION',
            N'VERROUILLAGE_EXPEDITION_PREPARATION',
            N'REJEU_EXPEDITION_PREPARATION',
            N'REFUS_EXPEDITION_PREPARATION',
            N'ERREUR_EXPEDITION_PREPARATION',
            N'AUTH_EXPEDITION',
            N'COMMENTAIRE_EXCEPTIONNEL'
        )),
    CONSTRAINT CK_Mobile_LogSynchronisation_Message_NonVide
        CHECK (LEN(LTRIM(RTRIM(Message))) > 0)
);
GO

CREATE INDEX IX_Mobile_LogSynchronisation_Date
ON dbo.Mobile_LogSynchronisation (DateEvenement DESC);
GO

CREATE INDEX IX_Mobile_LogSynchronisation_Synchronisation
ON dbo.Mobile_LogSynchronisation (IdSynchronisation);
GO

CREATE INDEX IX_Mobile_LogSynchronisation_Tournee
ON dbo.Mobile_LogSynchronisation (IdTourneeMobile);
GO

CREATE INDEX IX_Mobile_LogSynchronisation_Livreur
ON dbo.Mobile_LogSynchronisation (IdLivreur);
GO

CREATE INDEX IX_Mobile_LogSynchronisation_TypeNiveau
ON dbo.Mobile_LogSynchronisation (TypeEvenement, Niveau, DateEvenement DESC);
GO

/* ============================================================
   TABLE : Mobile_ExportAdmin

   Rôle :
   Historique des exports administratifs.
============================================================ */
CREATE TABLE dbo.Mobile_ExportAdmin (
    IdExport BIGINT IDENTITY(1,1) NOT NULL,
    DateExport DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ExportAdmin_DateExport DEFAULT SYSDATETIMEOFFSET(),
    TypeExport NVARCHAR(20) NOT NULL,
    DateDebut DATE NULL,
    DateFin DATE NULL,
    CheminFichier NVARCHAR(500) NULL,
    NombreTournees INT NULL,
    NombreLignes INT NULL,
    DemandePar NVARCHAR(100) NULL,
    CONSTRAINT PK_Mobile_ExportAdmin
        PRIMARY KEY (IdExport),
    CONSTRAINT CK_Mobile_ExportAdmin_TypeExport
        CHECK (TypeExport IN (N'CSV', N'JSON', N'EXCEL')),
    CONSTRAINT CK_Mobile_ExportAdmin_Dates
        CHECK (DateDebut IS NULL OR DateFin IS NULL OR DateDebut <= DateFin),
    CONSTRAINT CK_Mobile_ExportAdmin_NombreTournees
        CHECK (NombreTournees IS NULL OR NombreTournees >= 0),
    CONSTRAINT CK_Mobile_ExportAdmin_NombreLignes
        CHECK (NombreLignes IS NULL OR NombreLignes >= 0)
);
GO

CREATE INDEX IX_Mobile_ExportAdmin_DateExport
ON dbo.Mobile_ExportAdmin (DateExport DESC);
GO

CREATE INDEX IX_Mobile_ExportAdmin_TypeExport
ON dbo.Mobile_ExportAdmin (TypeExport);
GO

/* ============================================================
   VUE : v_Mobile_TourneeQuantitesDetaillees

   Rôle :
   Vue administrative pour consulter le détail des quantités
   par tournée, ligne et article.
============================================================ */
CREATE VIEW dbo.v_Mobile_TourneeQuantitesDetaillees
AS
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
    t.DateChargementMobile,
    t.DateReceptionApi,
    t.DateEnvoi,
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
    tl.CommentaireExceptionnel,
    q.CodeArticle,
    q.LibelleArticle,
    q.QuantiteLivreePrevue,
    q.QuantiteLivree,
    q.QuantiteRecuperee,
    CASE
        WHEN q.QuantiteLivreePrevue IS NULL THEN NULL
        ELSE q.QuantiteLivree - q.QuantiteLivreePrevue
    END AS EcartLivrePrevuReel
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = t.IdLivreur
INNER JOIN dbo.Mobile_TourneeLigne tl
    ON tl.IdTourneeMobile = t.IdTourneeMobile
LEFT JOIN dbo.Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = tl.IdTourneeLigne;
GO

/* ============================================================
   VUE : v_Mobile_TotauxTournee

   Rôle :
   Vue administrative de synthèse par tournée et article.
============================================================ */
CREATE VIEW dbo.v_Mobile_TotauxTournee
AS
SELECT
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
    l.CodeLivreur,
    l.NomLivreur,
    t.StatutSynchronisation,
    t.EstVerrouillee,
    q.CodeArticle,
    MAX(q.LibelleArticle) AS LibelleArticle,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 0 ELSE q.QuantiteLivreePrevue END) AS TotalLivrePrevuRenseigne,
    SUM(q.QuantiteLivree) AS TotalLivre,
    SUM(q.QuantiteRecuperee) AS TotalRecupere,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 0 ELSE q.QuantiteLivree - q.QuantiteLivreePrevue END) AS TotalEcartSurValeursRenseignees,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 1 ELSE 0 END) AS NombreValeursPrevuesNonRenseignees,
    COUNT_BIG(*) AS NombreLignesQuantite
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = t.IdLivreur
INNER JOIN dbo.Mobile_TourneeLigne tl
    ON tl.IdTourneeMobile = t.IdTourneeMobile
INNER JOIN dbo.Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = tl.IdTourneeLigne
GROUP BY
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
    l.CodeLivreur,
    l.NomLivreur,
    t.StatutSynchronisation,
    t.EstVerrouillee,
    q.CodeArticle;
GO

/* ============================================================
   VUE : v_Mobile_ExpeditionPreparationDetail

   Rôle :
   Vue de suivi des préparations Expédition détaillées.
============================================================ */
CREATE VIEW dbo.v_Mobile_ExpeditionPreparationDetail
AS
SELECT
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage,
    p.IdLotVerrouillage,
    p.EmpreintePayload,
    p.DateCreation AS DateCreationPreparation,
    p.DateModification AS DateModificationPreparation,
    pl.IdPreparationExpeditionLigne,
    pl.IdLigneSource,
    pl.OrdreArret,
    pl.NumClient,
    pl.NomClient,
    pl.NomAffiche,
    pl.CodePDL,
    pl.DescriptionPDL,
    pl.CodeArticle,
    pl.LibelleArticle,
    pl.QuantiteLivreePrevue,
    pl.Actif,
    pl.DateCreation AS DateCreationLigne,
    pl.DateModification AS DateModificationLigne
FROM dbo.Mobile_ExpeditionPreparation p
LEFT JOIN dbo.Mobile_ExpeditionPreparationLigne pl
    ON pl.IdPreparationExpedition = p.IdPreparationExpedition;
GO

/* ============================================================
   VUE : v_Mobile_ExpeditionPreparationSynthese

   Rôle :
   Vue de synthèse par préparation, article et statut.
============================================================ */
CREATE VIEW dbo.v_Mobile_ExpeditionPreparationSynthese
AS
SELECT
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage,
    p.IdLotVerrouillage,
    pl.CodeArticle,
    MAX(pl.LibelleArticle) AS LibelleArticle,
    COUNT_BIG(pl.IdPreparationExpeditionLigne) AS NombreLignes,
    SUM(CASE WHEN pl.QuantiteLivreePrevue IS NULL THEN 0 ELSE pl.QuantiteLivreePrevue END) AS TotalQuantiteLivreePrevueRenseignee,
    SUM(CASE WHEN pl.QuantiteLivreePrevue IS NULL THEN 1 ELSE 0 END) AS NombreQuantitesNonRenseignees
FROM dbo.Mobile_ExpeditionPreparation p
LEFT JOIN dbo.Mobile_ExpeditionPreparationLigne pl
    ON pl.IdPreparationExpedition = p.IdPreparationExpedition
    AND pl.Actif = 1
GROUP BY
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage,
    p.IdLotVerrouillage,
    pl.CodeArticle;
GO

/* ============================================================
   VERIFICATION RAPIDE DES OBJETS CREES
============================================================ */
SELECT
    expected.ObjectName AS TableAttendue,
    CASE
        WHEN t.name IS NOT NULL THEN N'OK'
        ELSE N'ABSENTE'
    END AS Etat
FROM (
    VALUES
        (N'Mobile_ArticleSaisissable'),
        (N'Mobile_UtilisateurExpedition'),
        (N'Mobile_Livreur'),
        (N'Mobile_ChargementTournee'),
        (N'Mobile_Tournee'),
        (N'Mobile_TourneeLigne'),
        (N'Mobile_TourneeLigneQuantite'),
        (N'Mobile_ExpeditionLotVerrouillage'),
        (N'Mobile_ExpeditionPreparation'),
        (N'Mobile_ExpeditionPreparationLigne'),
        (N'Mobile_ExpeditionPreparationHistorique'),
        (N'Mobile_CommentaireExceptionnel'),
        (N'Mobile_LogSynchronisation'),
        (N'Mobile_ExportAdmin')
) expected(ObjectName)
LEFT JOIN sys.tables t
    ON t.name = expected.ObjectName
ORDER BY expected.ObjectName;
GO

SELECT
    expected.ObjectName AS VueAttendue,
    CASE
        WHEN v.name IS NOT NULL THEN N'OK'
        ELSE N'ABSENTE'
    END AS Etat
FROM (
    VALUES
        (N'v_Mobile_TourneeQuantitesDetaillees'),
        (N'v_Mobile_TotauxTournee'),
        (N'v_Mobile_ExpeditionPreparationDetail'),
        (N'v_Mobile_ExpeditionPreparationSynthese')
) expected(ObjectName)
LEFT JOIN sys.views v
    ON v.name = expected.ObjectName
ORDER BY expected.ObjectName;
GO

SELECT
    i.name AS IndexName,
    OBJECT_NAME(i.object_id) AS TableName,
    i.is_unique,
    i.has_filter,
    i.filter_definition
FROM sys.indexes i
WHERE OBJECT_NAME(i.object_id) LIKE N'Mobile_%'
  AND i.name IS NOT NULL
ORDER BY TableName, IndexName;
GO
USE [bdd_eric];
GO

/* ============================================================
   BDD SLI - PROJET MOBILE TOURNEE
   Version complete corrigee : 1.3.1

   Objectif :
   - Remplacer completement la structure Mobile_* du projet SLI.
   - Aligner la base SQL Server avec le contrat API/Mobile final
     et avec les routes Expédition :
       GET  /api/expedition/preparations/a-preparer
       POST /api/expedition/preparations/verrouiller
   - Supprimer l'ancien nommage PreRemplissage côté base complete.
   - Créer directement les tables finales :
       Mobile_ExpeditionPreparation
       Mobile_ExpeditionPreparationLigne
       Mobile_ExpeditionPreparationHistorique
   - Conserver les tables de synchronisation mobile, les logs,
     les commentaires exceptionnels et les quantités détaillées.

   IMPORTANT :
   - Script destructif prévu pour un environnement de développement
     ou de test.
   - Il supprime puis recrée les tables Mobile_* et les vues de suivi.
   - Ne pas exécuter sur une base contenant des données à conserver
     sans sauvegarde validée.
   - Les vues et tables ABSSolute ne sont pas modifiées.

   Principes structurants :
   - L'API lit les vues ABSSolute.
   - L'API écrit uniquement dans les tables Mobile_*.
   - Le mobile ne se connecte jamais directement à SQL Server.
   - Mobile_TourneeLigneQuantite est la source principale des
     quantités livrées et récupérées.
   - QuantiteLivreePrevue est nullable :
       NULL = l'expédition n'a rien renseigné ;
       0    = l'expédition a volontairement prévu zéro ;
       > 0  = quantité prévue.
   - Le verrouillage Expédition est idempotent via IdLotVerrouillage.
   - L'anti-doublon mobile métier est DateTournee + CodeTournee.
   - ROLLS_VIDES peut exister côté mobile récupération.
   - ROLLS_VIDES est interdit dans les préparations Expédition actives.
============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================
   SUPPRESSION DES VUES DE SUIVI
============================================================ */
IF OBJECT_ID('dbo.v_Mobile_ExpeditionPreparationSynthese', 'V') IS NOT NULL
    DROP VIEW dbo.v_Mobile_ExpeditionPreparationSynthese;
GO

IF OBJECT_ID('dbo.v_Mobile_ExpeditionPreparationDetail', 'V') IS NOT NULL
    DROP VIEW dbo.v_Mobile_ExpeditionPreparationDetail;
GO

IF OBJECT_ID('dbo.v_Mobile_TotauxTournee', 'V') IS NOT NULL
    DROP VIEW dbo.v_Mobile_TotauxTournee;
GO

IF OBJECT_ID('dbo.v_Mobile_TourneeQuantitesDetaillees', 'V') IS NOT NULL
    DROP VIEW dbo.v_Mobile_TourneeQuantitesDetaillees;
GO

/* ============================================================
   SUPPRESSION DES TABLES MOBILE DANS L'ORDRE DES DEPENDANCES

   Le script nettoie aussi les anciens noms PreRemplissage pour
   éviter les collisions avec une ancienne version de développement.
============================================================ */
IF OBJECT_ID('dbo.Mobile_ExpeditionPreparationHistorique', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ExpeditionPreparationHistorique;

IF OBJECT_ID('dbo.Mobile_ExpeditionPreparationLigne', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ExpeditionPreparationLigne;

IF OBJECT_ID('dbo.Mobile_ExpeditionPreparation', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ExpeditionPreparation;

IF OBJECT_ID('dbo.Mobile_PreRemplissageHistorique', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_PreRemplissageHistorique;

IF OBJECT_ID('dbo.Mobile_PreRemplissageQuantite', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_PreRemplissageQuantite;

IF OBJECT_ID('dbo.Mobile_PreRemplissageTournee', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_PreRemplissageTournee;

IF OBJECT_ID('dbo.Mobile_ExpeditionLotVerrouillage', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ExpeditionLotVerrouillage;

IF OBJECT_ID('dbo.Mobile_CommentaireExceptionnel', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_CommentaireExceptionnel;

IF OBJECT_ID('dbo.Mobile_TourneeLigneQuantite', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_TourneeLigneQuantite;

IF OBJECT_ID('dbo.Mobile_LogSynchronisation', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_LogSynchronisation;

IF OBJECT_ID('dbo.Mobile_ExportAdmin', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ExportAdmin;

IF OBJECT_ID('dbo.Mobile_TourneeLigne', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_TourneeLigne;

IF OBJECT_ID('dbo.Mobile_Tournee', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_Tournee;

IF OBJECT_ID('dbo.Mobile_ChargementTournee', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ChargementTournee;

IF OBJECT_ID('dbo.Mobile_Livreur', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_Livreur;

IF OBJECT_ID('dbo.Mobile_ArticleSaisissable', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_ArticleSaisissable;

IF OBJECT_ID('dbo.Mobile_UtilisateurExpedition', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_UtilisateurExpedition;
GO

/* ============================================================
   TABLE : Mobile_ArticleSaisissable

   Rôle :
   Référentiel des articles gérés par l'application mobile.

   Règles :
   - ROLLS, TAPIS, SACS sont utilisables côté Expédition.
   - ROLLS_VIDES est disponible seulement pour la récupération mobile.
   - La table permet d'ajouter des articles sans modifier la table
     Mobile_TourneeLigneQuantite.
============================================================ */
CREATE TABLE dbo.Mobile_ArticleSaisissable (
    CodeArticle NVARCHAR(50) NOT NULL,
    LibelleArticle NVARCHAR(100) NOT NULL,
    OrdreAffichage INT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_OrdreAffichage DEFAULT 0,
    EstActif BIT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_EstActif DEFAULT 1,
    EstVisibleMobile BIT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_EstVisibleMobile DEFAULT 1,
    EstLivrableMobile BIT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_EstLivrableMobile DEFAULT 1,
    EstRecuperableMobile BIT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_EstRecuperableMobile DEFAULT 1,
    EstUtilisableExpedition BIT NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_EstUtilisableExpedition DEFAULT 0,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ArticleSaisissable_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_ArticleSaisissable
        PRIMARY KEY (CodeArticle),
    CONSTRAINT CK_Mobile_ArticleSaisissable_CodeArticle_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeArticle))) > 0),
    CONSTRAINT CK_Mobile_ArticleSaisissable_LibelleArticle_NonVide
        CHECK (LEN(LTRIM(RTRIM(LibelleArticle))) > 0),
    CONSTRAINT CK_Mobile_ArticleSaisissable_OrdreAffichage
        CHECK (OrdreAffichage >= 0),
    CONSTRAINT CK_Mobile_ArticleSaisissable_Usage
        CHECK (
            EstVisibleMobile = 0
            OR EstLivrableMobile = 1
            OR EstRecuperableMobile = 1
        ),
    CONSTRAINT CK_Mobile_ArticleSaisissable_RollsVides
        CHECK (
            CodeArticle <> N'ROLLS_VIDES'
            OR (
                EstLivrableMobile = 0
                AND EstRecuperableMobile = 1
                AND EstUtilisableExpedition = 0
            )
        )
);
GO

CREATE INDEX IX_Mobile_ArticleSaisissable_ActifOrdre
ON dbo.Mobile_ArticleSaisissable (EstActif, EstVisibleMobile, OrdreAffichage);
GO

CREATE INDEX IX_Mobile_ArticleSaisissable_Expedition
ON dbo.Mobile_ArticleSaisissable (EstUtilisableExpedition, EstActif, OrdreAffichage);
GO

INSERT INTO dbo.Mobile_ArticleSaisissable
    (
        CodeArticle,
        LibelleArticle,
        OrdreAffichage,
        EstActif,
        EstVisibleMobile,
        EstLivrableMobile,
        EstRecuperableMobile,
        EstUtilisableExpedition
    )
VALUES
    (N'ROLLS',       N'Rolls',       1, 1, 1, 1, 1, 1),
    (N'ROLLS_VIDES', N'Rolls vides', 2, 1, 1, 0, 1, 0),
    (N'TAPIS',       N'Tapis',       3, 1, 1, 1, 1, 1),
    (N'SACS',        N'Sacs',        4, 1, 1, 1, 1, 1);
GO

/* ============================================================
   TABLE : Mobile_UtilisateurExpedition

   Rôle :
   Comptes applicatifs de l'interface Expédition.

   Sécurité :
   - Ne jamais stocker de mot de passe en clair.
   - MotDePasseHash doit contenir un hash produit par l'application.
   - Les vrais secrets ne doivent pas être placés dans ce script.
============================================================ */
CREATE TABLE dbo.Mobile_UtilisateurExpedition (
    IdUtilisateurExpedition INT IDENTITY(1,1) NOT NULL,
    Identifiant NVARCHAR(100) NOT NULL,
    NomAffiche NVARCHAR(150) NOT NULL,
    MotDePasseHash NVARCHAR(500) NULL,
    RoleUtilisateur NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_UtilisateurExpedition_Role DEFAULT N'EXPEDITION',
    EstActif BIT NOT NULL
        CONSTRAINT DF_Mobile_UtilisateurExpedition_EstActif DEFAULT 1,
    DerniereConnexion DATETIMEOFFSET(0) NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_UtilisateurExpedition_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_UtilisateurExpedition
        PRIMARY KEY (IdUtilisateurExpedition),
    CONSTRAINT UQ_Mobile_UtilisateurExpedition_Identifiant
        UNIQUE (Identifiant),
    CONSTRAINT CK_Mobile_UtilisateurExpedition_Identifiant_NonVide
        CHECK (LEN(LTRIM(RTRIM(Identifiant))) > 0),
    CONSTRAINT CK_Mobile_UtilisateurExpedition_NomAffiche_NonVide
        CHECK (LEN(LTRIM(RTRIM(NomAffiche))) > 0),
    CONSTRAINT CK_Mobile_UtilisateurExpedition_Role
        CHECK (RoleUtilisateur IN (N'EXPEDITION', N'ADMIN', N'INFORMATIQUE'))
);
GO

CREATE INDEX IX_Mobile_UtilisateurExpedition_EstActif
ON dbo.Mobile_UtilisateurExpedition (EstActif, Identifiant);
GO

/* ============================================================
   TABLE : Mobile_Livreur

   Rôle :
   Référentiel des livreurs/chauffeurs côté application mobile.

   Source métier :
   - v_chauffeurs.DRIVERNUMERO -> CodeLivreur
   - v_chauffeurs.DRIVERNAME    -> NomLivreur
============================================================ */
CREATE TABLE dbo.Mobile_Livreur (
    IdLivreur INT IDENTITY(1,1) NOT NULL,
    CodeLivreur NVARCHAR(50) NOT NULL,
    NomLivreur NVARCHAR(100) NOT NULL,
    EstActif BIT NOT NULL
        CONSTRAINT DF_Mobile_Livreur_EstActif DEFAULT 1,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_Livreur_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_Livreur
        PRIMARY KEY (IdLivreur),
    CONSTRAINT UQ_Mobile_Livreur_CodeLivreur
        UNIQUE (CodeLivreur),
    CONSTRAINT CK_Mobile_Livreur_CodeLivreur_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeLivreur))) > 0),
    CONSTRAINT CK_Mobile_Livreur_NomLivreur_NonVide
        CHECK (LEN(LTRIM(RTRIM(NomLivreur))) > 0)
);
GO

CREATE INDEX IX_Mobile_Livreur_EstActif
ON dbo.Mobile_Livreur (EstActif, CodeLivreur);
GO

/* ============================================================
   TABLE : Mobile_ChargementTournee

   Rôle :
   Historiser les chargements de tournée effectués par le mobile.
============================================================ */
CREATE TABLE dbo.Mobile_ChargementTournee (
    IdChargement BIGINT IDENTITY(1,1) NOT NULL,
    SchemaVersion NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Mobile_ChargementTournee_SchemaVersion DEFAULT N'1.3',
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    IdLivreur INT NOT NULL,
    DateChargement DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ChargementTournee_DateChargement DEFAULT SYSDATETIMEOFFSET(),
    NombrePointsEnvoyes INT NULL,
    NomAppareil NVARCHAR(100) NULL,
    VersionApplication NVARCHAR(50) NULL,
    AdresseIP NVARCHAR(50) NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ChargementTournee_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_Mobile_ChargementTournee
        PRIMARY KEY (IdChargement),
    CONSTRAINT FK_Mobile_ChargementTournee_Livreur
        FOREIGN KEY (IdLivreur)
        REFERENCES dbo.Mobile_Livreur(IdLivreur),
    CONSTRAINT CK_Mobile_ChargementTournee_SchemaVersion_NonVide
        CHECK (LEN(LTRIM(RTRIM(SchemaVersion))) > 0),
    CONSTRAINT CK_Mobile_ChargementTournee_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_ChargementTournee_NombrePoints
        CHECK (NombrePointsEnvoyes IS NULL OR NombrePointsEnvoyes >= 0)
);
GO

CREATE INDEX IX_Mobile_ChargementTournee_DateTournee
ON dbo.Mobile_ChargementTournee (DateTournee, CodeTournee);
GO

CREATE INDEX IX_Mobile_ChargementTournee_Livreur
ON dbo.Mobile_ChargementTournee (IdLivreur);
GO

/* ============================================================
   TABLE : Mobile_Tournee

   Rôle :
   En-tête d'une synchronisation de tournée mobile.

   Protections :
   - UQ_Mobile_Tournee_IdSynchronisation : anti-rejeu technique.
   - UX_Mobile_Tournee_EnvoiUnique : anti-double envoi métier.
     Une seule tournée ENVOYEE est autorisée par DateTournee + CodeTournee.
============================================================ */
CREATE TABLE dbo.Mobile_Tournee (
    IdTourneeMobile BIGINT IDENTITY(1,1) NOT NULL,
    SchemaVersion NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Mobile_Tournee_SchemaVersion DEFAULT N'1.3',
    IdSynchronisation UNIQUEIDENTIFIER NOT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    IdLivreur INT NOT NULL,
    StatutSynchronisation NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_Tournee_StatutSynchronisation DEFAULT N'EN_ATTENTE',
    DateChargementMobile DATETIMEOFFSET(0) NULL,
    DateReceptionApi DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_Tournee_DateReceptionApi DEFAULT SYSDATETIMEOFFSET(),
    DateEnvoi DATETIMEOFFSET(0) NULL,
    EstVerrouillee BIT NOT NULL
        CONSTRAINT DF_Mobile_Tournee_EstVerrouillee DEFAULT 0,
    NombrePointsPrevus INT NULL,
    NombrePointsSaisis INT NULL,
    CommentaireGlobal NVARCHAR(1000) NULL,
    NomAppareil NVARCHAR(100) NULL,
    VersionApplication NVARCHAR(50) NULL,
    AdresseIP NVARCHAR(50) NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_Tournee_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_Tournee
        PRIMARY KEY (IdTourneeMobile),
    CONSTRAINT FK_Mobile_Tournee_Livreur
        FOREIGN KEY (IdLivreur)
        REFERENCES dbo.Mobile_Livreur(IdLivreur),
    CONSTRAINT UQ_Mobile_Tournee_IdSynchronisation
        UNIQUE (IdSynchronisation),
    CONSTRAINT CK_Mobile_Tournee_SchemaVersion_NonVide
        CHECK (LEN(LTRIM(RTRIM(SchemaVersion))) > 0),
    CONSTRAINT CK_Mobile_Tournee_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_Tournee_StatutSynchronisation
        CHECK (StatutSynchronisation IN (
            N'NON_ENVOYEE',
            N'EN_ATTENTE',
            N'ENVOYEE',
            N'ERREUR_ENVOI',
            N'ANNULEE'
        )),
    CONSTRAINT CK_Mobile_Tournee_Verrouillage
        CHECK (
            (
                StatutSynchronisation = N'ENVOYEE'
                AND EstVerrouillee = 1
                AND DateEnvoi IS NOT NULL
            )
            OR
            (
                StatutSynchronisation <> N'ENVOYEE'
            )
        ),
    CONSTRAINT CK_Mobile_Tournee_NombrePointsPrevus
        CHECK (NombrePointsPrevus IS NULL OR NombrePointsPrevus >= 0),
    CONSTRAINT CK_Mobile_Tournee_NombrePointsSaisis
        CHECK (NombrePointsSaisis IS NULL OR NombrePointsSaisis >= 0)
);
GO

CREATE UNIQUE INDEX UX_Mobile_Tournee_EnvoiUnique
ON dbo.Mobile_Tournee (DateTournee, CodeTournee)
WHERE StatutSynchronisation = N'ENVOYEE';
GO

CREATE INDEX IX_Mobile_Tournee_DateTournee
ON dbo.Mobile_Tournee (DateTournee, CodeTournee);
GO

CREATE INDEX IX_Mobile_Tournee_Livreur
ON dbo.Mobile_Tournee (IdLivreur);
GO

CREATE INDEX IX_Mobile_Tournee_Statut
ON dbo.Mobile_Tournee (StatutSynchronisation, DateTournee, CodeTournee);
GO

/* ============================================================
   TABLE : Mobile_TourneeLigne

   Rôle :
   Ligne client / point de livraison / arrêt d'une tournée.

   Cette table conserve :
   - un snapshot des données ABSSolute utiles au mobile ;
   - un snapshot du commentaire exceptionnel envoyé au mobile ;
   - les données terrain saisies par le livreur ;
   - les totaux de compatibilité calculés depuis les quantités.
============================================================ */
CREATE TABLE dbo.Mobile_TourneeLigne (
    IdTourneeLigne BIGINT IDENTITY(1,1) NOT NULL,
    IdTourneeMobile BIGINT NOT NULL,
    IdLigneSource NVARCHAR(300) NOT NULL,

    NumClient NVARCHAR(50) NOT NULL,
    NomClient NVARCHAR(255) NOT NULL,
    NomAffiche NVARCHAR(255) NULL,
    CodePDL NVARCHAR(50) NULL,
    DescriptionPDL NVARCHAR(255) NULL,

    AdresseLigne1 NVARCHAR(255) NULL,
    AdresseLigne2 NVARCHAR(255) NULL,
    AdresseLigne3 NVARCHAR(255) NULL,
    Ville NVARCHAR(100) NULL,
    CodePostal NVARCHAR(20) NULL,

    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    JourTournee INT NULL,
    JourLibelle NVARCHAR(30) NULL,
    SchemaLivraison NVARCHAR(100) NULL,
    OrdreArret INT NULL,
    Horaire NVARCHAR(50) NULL,

    JourTourneeRetour INT NULL,
    JourRetourLibelle NVARCHAR(30) NULL,
    CodeTourneeRetour NVARCHAR(50) NULL,
    LibelleTourneeRetour NVARCHAR(255) NULL,

    Instructions NVARCHAR(1000) NULL,
    CommentaireExceptionnel NVARCHAR(1000) NULL,
    ZoneDechargement NVARCHAR(100) NULL,
    ZoneDechargementAffichee NVARCHAR(150) NULL,
    Zone NVARCHAR(100) NULL,
    PrecisionInfo NVARCHAR(1000) NULL,
    Cle NVARCHAR(100) NULL,
    TypeLinge NVARCHAR(100) NULL,

    EstFerme BIT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_EstFerme DEFAULT 0,
    DateFermeture DATE NULL,
    MotifFermeture NVARCHAR(255) NULL,

    QuantiteLivree INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_QuantiteLivree DEFAULT 0,
    QuantiteReprise INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_QuantiteReprise DEFAULT 0,

    NbExpes INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbExpes DEFAULT 0,
    NbRolls INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbRolls DEFAULT 0,
    NbVetements INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbVetements DEFAULT 0,
    NbTapis INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbTapis DEFAULT 0,
    NbSacs INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbSacs DEFAULT 0,
    NbRecuperes INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_NbRecuperes DEFAULT 0,

    PrecisionLivreur NVARCHAR(1000) NULL,
    StatutPassage NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_StatutPassage DEFAULT N'A_FAIRE',
    CommentaireLivreur NVARCHAR(1000) NULL,
    HeureValidation DATETIMEOFFSET(0) NULL,
    EstValidee BIT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_EstValidee DEFAULT 0,

    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,

    CONSTRAINT PK_Mobile_TourneeLigne
        PRIMARY KEY (IdTourneeLigne),
    CONSTRAINT FK_Mobile_TourneeLigne_Tournee
        FOREIGN KEY (IdTourneeMobile)
        REFERENCES dbo.Mobile_Tournee(IdTourneeMobile)
        ON DELETE CASCADE,
    CONSTRAINT CK_Mobile_TourneeLigne_IdLigneSource_NonVide
        CHECK (LEN(LTRIM(RTRIM(IdLigneSource))) > 0),
    CONSTRAINT CK_Mobile_TourneeLigne_NumClient_NonVide
        CHECK (LEN(LTRIM(RTRIM(NumClient))) > 0),
    CONSTRAINT CK_Mobile_TourneeLigne_NomClient_NonVide
        CHECK (LEN(LTRIM(RTRIM(NomClient))) > 0),
    CONSTRAINT CK_Mobile_TourneeLigne_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_TourneeLigne_JourTournee
        CHECK (JourTournee IS NULL OR JourTournee BETWEEN 1 AND 7),
    CONSTRAINT CK_Mobile_TourneeLigne_JourTourneeRetour
        CHECK (JourTourneeRetour IS NULL OR JourTourneeRetour BETWEEN 1 AND 7),
    CONSTRAINT CK_Mobile_TourneeLigne_OrdreArret
        CHECK (OrdreArret IS NULL OR OrdreArret >= 0),
    CONSTRAINT CK_Mobile_TourneeLigne_QuantiteLivree
        CHECK (QuantiteLivree >= 0),
    CONSTRAINT CK_Mobile_TourneeLigne_QuantiteReprise
        CHECK (QuantiteReprise >= 0),
    CONSTRAINT CK_Mobile_TourneeLigne_QuantitesCompatibilite
        CHECK (
            NbExpes >= 0
            AND NbRolls >= 0
            AND NbVetements >= 0
            AND NbTapis >= 0
            AND NbSacs >= 0
            AND NbRecuperes >= 0
        ),
    CONSTRAINT CK_Mobile_TourneeLigne_StatutPassage
        CHECK (StatutPassage IN (
            N'A_FAIRE',
            N'FAIT',
            N'NON_FAIT',
            N'ANOMALIE'
        )),
    CONSTRAINT CK_Mobile_TourneeLigne_CommentaireObligatoire
        CHECK (
            StatutPassage NOT IN (N'NON_FAIT', N'ANOMALIE')
            OR
            (
                CommentaireLivreur IS NOT NULL
                AND LEN(LTRIM(RTRIM(CommentaireLivreur))) > 0
            )
        ),
    CONSTRAINT CK_Mobile_TourneeLigne_Validation
        CHECK (
            (
                EstValidee = 0
                AND HeureValidation IS NULL
            )
            OR
            (
                EstValidee = 1
                AND HeureValidation IS NOT NULL
            )
        ),
    CONSTRAINT CK_Mobile_TourneeLigne_Fermeture
        CHECK (
            (
                EstFerme = 0
                AND DateFermeture IS NULL
            )
            OR
            (
                EstFerme = 1
            )
        )
);
GO

CREATE UNIQUE INDEX UX_Mobile_TourneeLigne_IdLigneSource
ON dbo.Mobile_TourneeLigne (IdTourneeMobile, IdLigneSource);
GO

CREATE INDEX IX_Mobile_TourneeLigne_TourneeOrdre
ON dbo.Mobile_TourneeLigne (IdTourneeMobile, OrdreArret);
GO

CREATE INDEX IX_Mobile_TourneeLigne_ClientPDL
ON dbo.Mobile_TourneeLigne (IdTourneeMobile, NumClient, CodePDL);
GO

CREATE INDEX IX_Mobile_TourneeLigne_StatutPassage
ON dbo.Mobile_TourneeLigne (StatutPassage);
GO

CREATE INDEX IX_Mobile_TourneeLigne_ZoneDechargement
ON dbo.Mobile_TourneeLigne (ZoneDechargement, JourTourneeRetour);
GO

/* ============================================================
   TABLE : Mobile_TourneeLigneQuantite

   Rôle :
   Source principale des quantités par article pour une ligne
   de tournée synchronisée.
============================================================ */
CREATE TABLE dbo.Mobile_TourneeLigneQuantite (
    IdQuantite BIGINT IDENTITY(1,1) NOT NULL,
    IdTourneeLigne BIGINT NOT NULL,
    CodeArticle NVARCHAR(50) NOT NULL,
    LibelleArticle NVARCHAR(100) NULL,
    QuantiteLivreePrevue INT NULL,
    QuantiteLivree INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigneQuantite_QuantiteLivree DEFAULT 0,
    QuantiteRecuperee INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigneQuantite_QuantiteRecuperee DEFAULT 0,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigneQuantite_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_TourneeLigneQuantite
        PRIMARY KEY (IdQuantite),
    CONSTRAINT FK_Mobile_TourneeLigneQuantite_Ligne
        FOREIGN KEY (IdTourneeLigne)
        REFERENCES dbo.Mobile_TourneeLigne(IdTourneeLigne)
        ON DELETE CASCADE,
    CONSTRAINT FK_Mobile_TourneeLigneQuantite_Article
        FOREIGN KEY (CodeArticle)
        REFERENCES dbo.Mobile_ArticleSaisissable(CodeArticle),
    CONSTRAINT CK_Mobile_TourneeLigneQuantite_CodeArticle_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeArticle))) > 0),
    CONSTRAINT CK_Mobile_TourneeLigneQuantite_Quantites
        CHECK (
            (QuantiteLivreePrevue IS NULL OR QuantiteLivreePrevue >= 0)
            AND QuantiteLivree >= 0
            AND QuantiteRecuperee >= 0
        )
);
GO

CREATE UNIQUE INDEX UX_Mobile_TourneeLigneQuantite_Article
ON dbo.Mobile_TourneeLigneQuantite (IdTourneeLigne, CodeArticle);
GO

CREATE INDEX IX_Mobile_TourneeLigneQuantite_Ligne
ON dbo.Mobile_TourneeLigneQuantite (IdTourneeLigne);
GO

CREATE INDEX IX_Mobile_TourneeLigneQuantite_CodeArticle
ON dbo.Mobile_TourneeLigneQuantite (CodeArticle);
GO

/* ============================================================
   TABLE : Mobile_ExpeditionLotVerrouillage

   Rôle :
   Journal technique des POST de verrouillage Expédition.

   Objectifs :
   - IdLotVerrouillage rend le POST idempotent.
   - EmpreintePayload permet de refuser le rejeu d'un même lot
     avec un contenu différent.
   - La table journalise le lot sans dépendre circulairement de
     Mobile_ExpeditionPreparation.
============================================================ */
CREATE TABLE dbo.Mobile_ExpeditionLotVerrouillage (
    IdLotVerrouillage UNIQUEIDENTIFIER NOT NULL,
    EmpreintePayload CHAR(64) NOT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    StatutLot NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_StatutLot DEFAULT N'VERROUILLE',
    NombrePreparations INT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombrePreparations DEFAULT 0,
    NombreLignes INT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreLignes DEFAULT 0,
    NombreQuantites INT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionLotVerrouillage_NombreQuantites DEFAULT 0,
    AdresseIP NVARCHAR(50) NULL,
    NomAppareil NVARCHAR(100) NULL,
    VersionApplication NVARCHAR(50) NULL,
    MessageRetour NVARCHAR(1000) NULL,
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
        CHECK (NombrePreparations >= 0 AND NombreLignes >= 0 AND NombreQuantites >= 0)
);
GO

CREATE UNIQUE INDEX UX_Mobile_ExpeditionLotVerrouillage_DateCodeEmpreinte
ON dbo.Mobile_ExpeditionLotVerrouillage (DateTournee, CodeTournee, EmpreintePayload);
GO

CREATE INDEX IX_Mobile_ExpeditionLotVerrouillage_DateCode
ON dbo.Mobile_ExpeditionLotVerrouillage (DateTournee, CodeTournee);
GO

/* ============================================================
   TABLE : Mobile_ExpeditionPreparation

   Rôle :
   En-tête d'une préparation Expédition pour une date et une tournée.

   Cycle :
   - BROUILLON    : saisie encore modifiable côté interface web.
   - VERROUILLEE : préparation figée, consommable par GET mobile.
   - ANNULEE     : préparation invalidée.
============================================================ */
CREATE TABLE dbo.Mobile_ExpeditionPreparation (
    IdPreparationExpedition BIGINT IDENTITY(1,1) NOT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    StatutPreparation NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparation_StatutPreparation DEFAULT N'BROUILLON',
    EstVerrouille BIT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparation_EstVerrouille DEFAULT 0,
    DateVerrouillage DATETIMEOFFSET(0) NULL,
    IdLotVerrouillage UNIQUEIDENTIFIER NULL,
    EmpreintePayload CHAR(64) NULL,
    IdUtilisateurCreation INT NULL,
    IdUtilisateurModification INT NULL,
    IdUtilisateurVerrouillage INT NULL,
    AdresseIPCreation NVARCHAR(50) NULL,
    AdresseIPModification NVARCHAR(50) NULL,
    AdresseIPVerrouillage NVARCHAR(50) NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparation_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_ExpeditionPreparation
        PRIMARY KEY (IdPreparationExpedition),
    CONSTRAINT FK_Mobile_ExpeditionPreparation_LotVerrouillage
        FOREIGN KEY (IdLotVerrouillage)
        REFERENCES dbo.Mobile_ExpeditionLotVerrouillage(IdLotVerrouillage),
    CONSTRAINT FK_Mobile_ExpeditionPreparation_UtilisateurCreation
        FOREIGN KEY (IdUtilisateurCreation)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT FK_Mobile_ExpeditionPreparation_UtilisateurModification
        FOREIGN KEY (IdUtilisateurModification)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT FK_Mobile_ExpeditionPreparation_UtilisateurVerrouillage
        FOREIGN KEY (IdUtilisateurVerrouillage)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT UQ_Mobile_ExpeditionPreparation_DateCode
        UNIQUE (DateTournee, CodeTournee),
    CONSTRAINT CK_Mobile_ExpeditionPreparation_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparation_Statut
        CHECK (StatutPreparation IN (N'BROUILLON', N'VERROUILLEE', N'ANNULEE')),
    CONSTRAINT CK_Mobile_ExpeditionPreparation_EmpreintePayload
        CHECK (EmpreintePayload IS NULL OR LEN(LTRIM(RTRIM(EmpreintePayload))) = 64),
    CONSTRAINT CK_Mobile_ExpeditionPreparation_Verrouillage
        CHECK (
            (
                EstVerrouille = 0
                AND DateVerrouillage IS NULL
                AND IdLotVerrouillage IS NULL
                AND StatutPreparation IN (N'BROUILLON', N'ANNULEE')
            )
            OR
            (
                EstVerrouille = 1
                AND DateVerrouillage IS NOT NULL
                AND IdLotVerrouillage IS NOT NULL
                AND StatutPreparation = N'VERROUILLEE'
            )
        )
);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparation_Date
ON dbo.Mobile_ExpeditionPreparation (DateTournee, CodeTournee);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparation_Verrouillage
ON dbo.Mobile_ExpeditionPreparation (EstVerrouille, DateTournee, CodeTournee);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparation_Lot
ON dbo.Mobile_ExpeditionPreparation (IdLotVerrouillage);
GO

CREATE UNIQUE INDEX UX_Mobile_ExpeditionPreparation_LotUnique
ON dbo.Mobile_ExpeditionPreparation (IdLotVerrouillage)
WHERE IdLotVerrouillage IS NOT NULL;
GO

/* ============================================================
   TABLE : Mobile_ExpeditionPreparationLigne

   Rôle :
   Quantités prévues par l'Expédition, par ligne métier et par article.
   Ces valeurs alimentent quantites[].quantiteLivreePrevue
   dans le JSON de chargement mobile.

   Règles :
   - La ligne appartient à une préparation.
   - Les préparations actives n'acceptent que ROLLS, TAPIS et SACS.
   - ROLLS_VIDES reste exclu du module Expédition.
============================================================ */
CREATE TABLE dbo.Mobile_ExpeditionPreparationLigne (
    IdPreparationExpeditionLigne BIGINT IDENTITY(1,1) NOT NULL,
    IdPreparationExpedition BIGINT NOT NULL,
    IdLigneSource NVARCHAR(300) NOT NULL,
    OrdreArret INT NULL,
    NumClient NVARCHAR(50) NOT NULL,
    NomClient NVARCHAR(255) NULL,
    NomAffiche NVARCHAR(255) NULL,
    CodePDL NVARCHAR(50) NULL,
    DescriptionPDL NVARCHAR(255) NULL,
    CodeArticle NVARCHAR(50) NOT NULL,
    LibelleArticle NVARCHAR(100) NULL,
    QuantiteLivreePrevue INT NULL,
    Actif BIT NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparationLigne_Actif DEFAULT 1,
    IdUtilisateurCreation INT NULL,
    IdUtilisateurModification INT NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparationLigne_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_ExpeditionPreparationLigne
        PRIMARY KEY (IdPreparationExpeditionLigne),
    CONSTRAINT FK_Mobile_ExpeditionPreparationLigne_Preparation
        FOREIGN KEY (IdPreparationExpedition)
        REFERENCES dbo.Mobile_ExpeditionPreparation(IdPreparationExpedition)
        ON DELETE CASCADE,
    CONSTRAINT FK_Mobile_ExpeditionPreparationLigne_Article
        FOREIGN KEY (CodeArticle)
        REFERENCES dbo.Mobile_ArticleSaisissable(CodeArticle),
    CONSTRAINT FK_Mobile_ExpeditionPreparationLigne_UtilisateurCreation
        FOREIGN KEY (IdUtilisateurCreation)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT FK_Mobile_ExpeditionPreparationLigne_UtilisateurModification
        FOREIGN KEY (IdUtilisateurModification)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_IdLigneSource_NonVide
        CHECK (LEN(LTRIM(RTRIM(IdLigneSource))) > 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_NumClient_NonVide
        CHECK (LEN(LTRIM(RTRIM(NumClient))) > 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_CodeArticle_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeArticle))) > 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_ArticleAutorise
        CHECK (CodeArticle IN (N'ROLLS', N'TAPIS', N'SACS')),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_OrdreArret
        CHECK (OrdreArret IS NULL OR OrdreArret >= 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationLigne_Quantite
        CHECK (QuantiteLivreePrevue IS NULL OR QuantiteLivreePrevue >= 0)
);
GO

CREATE UNIQUE INDEX UX_Mobile_ExpeditionPreparationLigne_LigneArticle_Actif
ON dbo.Mobile_ExpeditionPreparationLigne (IdPreparationExpedition, IdLigneSource, CodeArticle)
WHERE Actif = 1;
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationLigne_ClientPDL
ON dbo.Mobile_ExpeditionPreparationLigne (IdPreparationExpedition, NumClient, CodePDL);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationLigne_Article
ON dbo.Mobile_ExpeditionPreparationLigne (CodeArticle);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationLigne_LigneSource
ON dbo.Mobile_ExpeditionPreparationLigne (IdLigneSource);
GO

/* ============================================================
   TABLE : Mobile_ExpeditionPreparationHistorique

   Rôle :
   Historique des créations, modifications, suppressions,
   verrouillages et tentatives de modification après verrouillage.
============================================================ */
CREATE TABLE dbo.Mobile_ExpeditionPreparationHistorique (
    IdHistorique BIGINT IDENTITY(1,1) NOT NULL,
    IdPreparationExpedition BIGINT NULL,
    IdPreparationExpeditionLigne BIGINT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    IdLigneSource NVARCHAR(300) NULL,
    NumClient NVARCHAR(50) NULL,
    CodePDL NVARCHAR(50) NULL,
    CodeArticle NVARCHAR(50) NULL,
    AncienneQuantiteLivreePrevue INT NULL,
    NouvelleQuantiteLivreePrevue INT NULL,
    ActionHistorique NVARCHAR(50) NOT NULL,
    IdUtilisateur INT NULL,
    Commentaire NVARCHAR(1000) NULL,
    AdresseIP NVARCHAR(50) NULL,
    DateEvenement DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ExpeditionPreparationHistorique_DateEvenement DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_Mobile_ExpeditionPreparationHistorique
        PRIMARY KEY (IdHistorique),
    CONSTRAINT FK_Mobile_ExpeditionPreparationHistorique_Preparation
        FOREIGN KEY (IdPreparationExpedition)
        REFERENCES dbo.Mobile_ExpeditionPreparation(IdPreparationExpedition),
    CONSTRAINT FK_Mobile_ExpeditionPreparationHistorique_Ligne
        FOREIGN KEY (IdPreparationExpeditionLigne)
        REFERENCES dbo.Mobile_ExpeditionPreparationLigne(IdPreparationExpeditionLigne),
    CONSTRAINT FK_Mobile_ExpeditionPreparationHistorique_Utilisateur
        FOREIGN KEY (IdUtilisateur)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT CK_Mobile_ExpeditionPreparationHistorique_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationHistorique_Action
        CHECK (ActionHistorique IN (
            N'CREATION',
            N'MODIFICATION',
            N'SUPPRESSION',
            N'VERROUILLAGE',
            N'REJEU_VERROUILLAGE_IDENTIQUE',
            N'TENTATIVE_MODIFICATION_APRES_VERROUILLAGE',
            N'REFUS_VERROUILLAGE'
        )),
    CONSTRAINT CK_Mobile_ExpeditionPreparationHistorique_AncienneQuantite
        CHECK (AncienneQuantiteLivreePrevue IS NULL OR AncienneQuantiteLivreePrevue >= 0),
    CONSTRAINT CK_Mobile_ExpeditionPreparationHistorique_NouvelleQuantite
        CHECK (NouvelleQuantiteLivreePrevue IS NULL OR NouvelleQuantiteLivreePrevue >= 0)
);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationHistorique_DateTournee
ON dbo.Mobile_ExpeditionPreparationHistorique (DateTournee, CodeTournee, DateEvenement DESC);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationHistorique_Utilisateur
ON dbo.Mobile_ExpeditionPreparationHistorique (IdUtilisateur, DateEvenement DESC);
GO

CREATE INDEX IX_Mobile_ExpeditionPreparationHistorique_Preparation
ON dbo.Mobile_ExpeditionPreparationHistorique (IdPreparationExpedition, DateEvenement DESC);
GO

/* ============================================================
   TABLE : Mobile_CommentaireExceptionnel

   Rôle :
   Commentaires propres au projet mobile, liés à une date et
   à un client, éventuellement à un point de livraison ou une ligne.
============================================================ */
CREATE TABLE dbo.Mobile_CommentaireExceptionnel (
    IdCommentaireExceptionnel BIGINT IDENTITY(1,1) NOT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NULL,
    IdLigneSource NVARCHAR(300) NULL,
    NumClient NVARCHAR(50) NOT NULL,
    CodePDL NVARCHAR(50) NULL,
    Commentaire NVARCHAR(1000) NOT NULL,
    Actif BIT NOT NULL
        CONSTRAINT DF_Mobile_CommentaireExceptionnel_Actif DEFAULT 1,
    CreePar NVARCHAR(100) NULL,
    ModifiePar NVARCHAR(100) NULL,
    IdUtilisateurCreation INT NULL,
    IdUtilisateurModification INT NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_CommentaireExceptionnel_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_CommentaireExceptionnel
        PRIMARY KEY (IdCommentaireExceptionnel),
    CONSTRAINT FK_Mobile_CommentaireExceptionnel_UtilisateurCreation
        FOREIGN KEY (IdUtilisateurCreation)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT FK_Mobile_CommentaireExceptionnel_UtilisateurModification
        FOREIGN KEY (IdUtilisateurModification)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT CK_Mobile_CommentaireExceptionnel_CodeTournee_NonVide
        CHECK (CodeTournee IS NULL OR LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_CommentaireExceptionnel_IdLigneSource_NonVide
        CHECK (IdLigneSource IS NULL OR LEN(LTRIM(RTRIM(IdLigneSource))) > 0),
    CONSTRAINT CK_Mobile_CommentaireExceptionnel_NumClient_NonVide
        CHECK (LEN(LTRIM(RTRIM(NumClient))) > 0),
    CONSTRAINT CK_Mobile_CommentaireExceptionnel_Commentaire_NonVide
        CHECK (LEN(LTRIM(RTRIM(Commentaire))) > 0)
);
GO

CREATE UNIQUE INDEX UX_Mobile_CommentaireExceptionnel_Ligne_Actif
ON dbo.Mobile_CommentaireExceptionnel (DateTournee, CodeTournee, IdLigneSource)
WHERE Actif = 1 AND IdLigneSource IS NOT NULL;
GO

CREATE UNIQUE INDEX UX_Mobile_CommentaireExceptionnel_ClientPDL_Actif
ON dbo.Mobile_CommentaireExceptionnel (DateTournee, CodeTournee, NumClient, CodePDL)
WHERE Actif = 1 AND IdLigneSource IS NULL;
GO

CREATE INDEX IX_Mobile_CommentaireExceptionnel_DateClient
ON dbo.Mobile_CommentaireExceptionnel (DateTournee, CodeTournee, NumClient, CodePDL, Actif);
GO

CREATE INDEX IX_Mobile_CommentaireExceptionnel_IdLigneSource
ON dbo.Mobile_CommentaireExceptionnel (DateTournee, CodeTournee, IdLigneSource, Actif);
GO

/* ============================================================
   TABLE : Mobile_LogSynchronisation

   Rôle :
   Journaliser les événements techniques et métier importants.
============================================================ */
CREATE TABLE dbo.Mobile_LogSynchronisation (
    IdLog BIGINT IDENTITY(1,1) NOT NULL,
    IdTourneeMobile BIGINT NULL,
    IdLivreur INT NULL,
    IdSynchronisation UNIQUEIDENTIFIER NULL,
    DateEvenement DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_LogSynchronisation_DateEvenement DEFAULT SYSDATETIMEOFFSET(),
    TypeEvenement NVARCHAR(70) NOT NULL,
    Niveau NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Mobile_LogSynchronisation_Niveau DEFAULT N'INFO',
    Message NVARCHAR(1000) NOT NULL,
    DetailTechnique NVARCHAR(MAX) NULL,
    AdresseIP NVARCHAR(50) NULL,
    NomAppareil NVARCHAR(100) NULL,
    VersionApplication NVARCHAR(50) NULL,
    CONSTRAINT PK_Mobile_LogSynchronisation
        PRIMARY KEY (IdLog),
    CONSTRAINT FK_Mobile_LogSynchronisation_Tournee
        FOREIGN KEY (IdTourneeMobile)
        REFERENCES dbo.Mobile_Tournee(IdTourneeMobile)
        ON DELETE SET NULL,
    CONSTRAINT FK_Mobile_LogSynchronisation_Livreur
        FOREIGN KEY (IdLivreur)
        REFERENCES dbo.Mobile_Livreur(IdLivreur),
    CONSTRAINT CK_Mobile_LogSynchronisation_Niveau
        CHECK (Niveau IN (N'INFO', N'WARNING', N'ERROR')),
    CONSTRAINT CK_Mobile_LogSynchronisation_TypeEvenement
        CHECK (TypeEvenement IN (
            N'CHARGEMENT_TOURNEE',
            N'ENVOI_TOURNEE',
            N'ENVOI_REUSSI',
            N'ERREUR_ENVOI',
            N'DOUBLE_ENVOI',
            N'ERREUR_SQL',
            N'VALIDATION_API',
            N'ERREUR_VALIDATION',
            N'EXPORT_ADMIN',
            N'TOURNEE_VERROUILLEE',
            N'LECTURE_VUE_ABSSOLUTE',
            N'ERREUR_LECTURE_VUE',
            N'CONSULTATION_ADMIN',
            N'CORRECTION_ADMIN',
            N'CHARGEMENT_PRE_REMPLISSAGE',
            N'CREATION_PRE_REMPLISSAGE',
            N'MODIFICATION_PRE_REMPLISSAGE',
            N'BLOCAGE_PRE_REMPLISSAGE',
            N'ERREUR_PRE_REMPLISSAGE',
            N'CHARGEMENT_EXPEDITION_PREPARATION',
            N'CREATION_EXPEDITION_PREPARATION',
            N'MODIFICATION_EXPEDITION_PREPARATION',
            N'VERROUILLAGE_EXPEDITION_PREPARATION',
            N'REJEU_EXPEDITION_PREPARATION',
            N'REFUS_EXPEDITION_PREPARATION',
            N'ERREUR_EXPEDITION_PREPARATION',
            N'AUTH_EXPEDITION',
            N'COMMENTAIRE_EXCEPTIONNEL'
        )),
    CONSTRAINT CK_Mobile_LogSynchronisation_Message_NonVide
        CHECK (LEN(LTRIM(RTRIM(Message))) > 0)
);
GO

CREATE INDEX IX_Mobile_LogSynchronisation_Date
ON dbo.Mobile_LogSynchronisation (DateEvenement DESC);
GO

CREATE INDEX IX_Mobile_LogSynchronisation_Synchronisation
ON dbo.Mobile_LogSynchronisation (IdSynchronisation);
GO

CREATE INDEX IX_Mobile_LogSynchronisation_Tournee
ON dbo.Mobile_LogSynchronisation (IdTourneeMobile);
GO

CREATE INDEX IX_Mobile_LogSynchronisation_Livreur
ON dbo.Mobile_LogSynchronisation (IdLivreur);
GO

CREATE INDEX IX_Mobile_LogSynchronisation_TypeNiveau
ON dbo.Mobile_LogSynchronisation (TypeEvenement, Niveau, DateEvenement DESC);
GO

/* ============================================================
   TABLE : Mobile_ExportAdmin

   Rôle :
   Historique des exports administratifs.
============================================================ */
CREATE TABLE dbo.Mobile_ExportAdmin (
    IdExport BIGINT IDENTITY(1,1) NOT NULL,
    DateExport DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_ExportAdmin_DateExport DEFAULT SYSDATETIMEOFFSET(),
    TypeExport NVARCHAR(20) NOT NULL,
    DateDebut DATE NULL,
    DateFin DATE NULL,
    CheminFichier NVARCHAR(500) NULL,
    NombreTournees INT NULL,
    NombreLignes INT NULL,
    DemandePar NVARCHAR(100) NULL,
    CONSTRAINT PK_Mobile_ExportAdmin
        PRIMARY KEY (IdExport),
    CONSTRAINT CK_Mobile_ExportAdmin_TypeExport
        CHECK (TypeExport IN (N'CSV', N'JSON', N'EXCEL')),
    CONSTRAINT CK_Mobile_ExportAdmin_Dates
        CHECK (DateDebut IS NULL OR DateFin IS NULL OR DateDebut <= DateFin),
    CONSTRAINT CK_Mobile_ExportAdmin_NombreTournees
        CHECK (NombreTournees IS NULL OR NombreTournees >= 0),
    CONSTRAINT CK_Mobile_ExportAdmin_NombreLignes
        CHECK (NombreLignes IS NULL OR NombreLignes >= 0)
);
GO

CREATE INDEX IX_Mobile_ExportAdmin_DateExport
ON dbo.Mobile_ExportAdmin (DateExport DESC);
GO

CREATE INDEX IX_Mobile_ExportAdmin_TypeExport
ON dbo.Mobile_ExportAdmin (TypeExport);
GO

/* ============================================================
   VUE : v_Mobile_TourneeQuantitesDetaillees

   Rôle :
   Vue administrative pour consulter le détail des quantités
   par tournée, ligne et article.
============================================================ */
CREATE VIEW dbo.v_Mobile_TourneeQuantitesDetaillees
AS
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
    t.DateChargementMobile,
    t.DateReceptionApi,
    t.DateEnvoi,
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
    tl.CommentaireExceptionnel,
    q.CodeArticle,
    q.LibelleArticle,
    q.QuantiteLivreePrevue,
    q.QuantiteLivree,
    q.QuantiteRecuperee,
    CASE
        WHEN q.QuantiteLivreePrevue IS NULL THEN NULL
        ELSE q.QuantiteLivree - q.QuantiteLivreePrevue
    END AS EcartLivrePrevuReel
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = t.IdLivreur
INNER JOIN dbo.Mobile_TourneeLigne tl
    ON tl.IdTourneeMobile = t.IdTourneeMobile
LEFT JOIN dbo.Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = tl.IdTourneeLigne;
GO

/* ============================================================
   VUE : v_Mobile_TotauxTournee

   Rôle :
   Vue administrative de synthèse par tournée et article.
============================================================ */
CREATE VIEW dbo.v_Mobile_TotauxTournee
AS
SELECT
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
    l.CodeLivreur,
    l.NomLivreur,
    t.StatutSynchronisation,
    t.EstVerrouillee,
    q.CodeArticle,
    MAX(q.LibelleArticle) AS LibelleArticle,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 0 ELSE q.QuantiteLivreePrevue END) AS TotalLivrePrevuRenseigne,
    SUM(q.QuantiteLivree) AS TotalLivre,
    SUM(q.QuantiteRecuperee) AS TotalRecupere,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 0 ELSE q.QuantiteLivree - q.QuantiteLivreePrevue END) AS TotalEcartSurValeursRenseignees,
    SUM(CASE WHEN q.QuantiteLivreePrevue IS NULL THEN 1 ELSE 0 END) AS NombreValeursPrevuesNonRenseignees,
    COUNT_BIG(*) AS NombreLignesQuantite
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = t.IdLivreur
INNER JOIN dbo.Mobile_TourneeLigne tl
    ON tl.IdTourneeMobile = t.IdTourneeMobile
INNER JOIN dbo.Mobile_TourneeLigneQuantite q
    ON q.IdTourneeLigne = tl.IdTourneeLigne
GROUP BY
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
    l.CodeLivreur,
    l.NomLivreur,
    t.StatutSynchronisation,
    t.EstVerrouillee,
    q.CodeArticle;
GO

/* ============================================================
   VUE : v_Mobile_ExpeditionPreparationDetail

   Rôle :
   Vue de suivi des préparations Expédition détaillées.
============================================================ */
CREATE VIEW dbo.v_Mobile_ExpeditionPreparationDetail
AS
SELECT
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage,
    p.IdLotVerrouillage,
    p.EmpreintePayload,
    p.DateCreation AS DateCreationPreparation,
    p.DateModification AS DateModificationPreparation,
    pl.IdPreparationExpeditionLigne,
    pl.IdLigneSource,
    pl.OrdreArret,
    pl.NumClient,
    pl.NomClient,
    pl.NomAffiche,
    pl.CodePDL,
    pl.DescriptionPDL,
    pl.CodeArticle,
    pl.LibelleArticle,
    pl.QuantiteLivreePrevue,
    pl.Actif,
    pl.DateCreation AS DateCreationLigne,
    pl.DateModification AS DateModificationLigne
FROM dbo.Mobile_ExpeditionPreparation p
LEFT JOIN dbo.Mobile_ExpeditionPreparationLigne pl
    ON pl.IdPreparationExpedition = p.IdPreparationExpedition;
GO

/* ============================================================
   VUE : v_Mobile_ExpeditionPreparationSynthese

   Rôle :
   Vue de synthèse par préparation, article et statut.
============================================================ */
CREATE VIEW dbo.v_Mobile_ExpeditionPreparationSynthese
AS
SELECT
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage,
    p.IdLotVerrouillage,
    pl.CodeArticle,
    MAX(pl.LibelleArticle) AS LibelleArticle,
    COUNT_BIG(pl.IdPreparationExpeditionLigne) AS NombreLignes,
    SUM(CASE WHEN pl.QuantiteLivreePrevue IS NULL THEN 0 ELSE pl.QuantiteLivreePrevue END) AS TotalQuantiteLivreePrevueRenseignee,
    SUM(CASE WHEN pl.QuantiteLivreePrevue IS NULL THEN 1 ELSE 0 END) AS NombreQuantitesNonRenseignees
FROM dbo.Mobile_ExpeditionPreparation p
LEFT JOIN dbo.Mobile_ExpeditionPreparationLigne pl
    ON pl.IdPreparationExpedition = p.IdPreparationExpedition
    AND pl.Actif = 1
GROUP BY
    p.IdPreparationExpedition,
    p.DateTournee,
    p.CodeTournee,
    p.LibelleTournee,
    p.StatutPreparation,
    p.EstVerrouille,
    p.DateVerrouillage,
    p.IdLotVerrouillage,
    pl.CodeArticle;
GO

/* ============================================================
   VERIFICATION RAPIDE DES OBJETS CREES
============================================================ */
SELECT
    expected.ObjectName AS TableAttendue,
    CASE
        WHEN t.name IS NOT NULL THEN N'OK'
        ELSE N'ABSENTE'
    END AS Etat
FROM (
    VALUES
        (N'Mobile_ArticleSaisissable'),
        (N'Mobile_UtilisateurExpedition'),
        (N'Mobile_Livreur'),
        (N'Mobile_ChargementTournee'),
        (N'Mobile_Tournee'),
        (N'Mobile_TourneeLigne'),
        (N'Mobile_TourneeLigneQuantite'),
        (N'Mobile_ExpeditionLotVerrouillage'),
        (N'Mobile_ExpeditionPreparation'),
        (N'Mobile_ExpeditionPreparationLigne'),
        (N'Mobile_ExpeditionPreparationHistorique'),
        (N'Mobile_CommentaireExceptionnel'),
        (N'Mobile_LogSynchronisation'),
        (N'Mobile_ExportAdmin')
) expected(ObjectName)
LEFT JOIN sys.tables t
    ON t.name = expected.ObjectName
ORDER BY expected.ObjectName;
GO

SELECT
    expected.ObjectName AS VueAttendue,
    CASE
        WHEN v.name IS NOT NULL THEN N'OK'
        ELSE N'ABSENTE'
    END AS Etat
FROM (
    VALUES
        (N'v_Mobile_TourneeQuantitesDetaillees'),
        (N'v_Mobile_TotauxTournee'),
        (N'v_Mobile_ExpeditionPreparationDetail'),
        (N'v_Mobile_ExpeditionPreparationSynthese')
) expected(ObjectName)
LEFT JOIN sys.views v
    ON v.name = expected.ObjectName
ORDER BY expected.ObjectName;
GO

SELECT
    i.name AS IndexName,
    OBJECT_NAME(i.object_id) AS TableName,
    i.is_unique,
    i.has_filter,
    i.filter_definition
FROM sys.indexes i
WHERE OBJECT_NAME(i.object_id) LIKE N'Mobile_%'
  AND i.name IS NOT NULL
ORDER BY TableName, IndexName;
GO
