/* ============================================================
   BDD SLI - PROJET MOBILE TOURNEE
   Version corrigee : 1.2

   Objectif :
   - Aligner la base SQL Server avec le cahier des charges final
     et la configuration API v1.2.
   - Conserver une architecture evolutive : mobile, API,
     interface expedition, commentaires exceptionnels, logs,
     quantites detaillees par article.
   - Ne jamais stocker de secrets applicatifs ou de chaines de
     connexion dans ce script SQL.

   IMPORTANT :
   - Script destructif prevu pour un environnement de developpement
     ou de test.
   - Il supprime puis recree les tables Mobile_* et les vues de suivi.
   - Ne pas executer en production sans sauvegarde et sans validation
     par le responsable informatique.

   Principes structurants :
   - Les tables ABSSolute ne sont pas modifiees.
   - Les vues ABSSolute restent lues uniquement par l'API.
   - Les donnees propres au projet mobile sont stockees dans des
     tables dediees.
   - Mobile_TourneeLigneQuantite devient la source principale des
     quantites livrees/recuperees.
   - QuantiteLivreePrevue est nullable :
       NULL = l'expedition n'a rien renseigne ;
       0    = l'expedition a volontairement prevu zero ;
       > 0  = quantite prevue.
============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================
   SUPPRESSION DES VUES DE SUIVI
============================================================ */
IF OBJECT_ID('dbo.v_Mobile_TotauxTournee', 'V') IS NOT NULL
    DROP VIEW dbo.v_Mobile_TotauxTournee;
GO

IF OBJECT_ID('dbo.v_Mobile_TourneeQuantitesDetaillees', 'V') IS NOT NULL
    DROP VIEW dbo.v_Mobile_TourneeQuantitesDetaillees;
GO

/* ============================================================
   SUPPRESSION DES TABLES MOBILE DANS L'ORDRE DES DEPENDANCES
============================================================ */
IF OBJECT_ID('dbo.Mobile_PreRemplissageHistorique', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_PreRemplissageHistorique;

IF OBJECT_ID('dbo.Mobile_PreRemplissageQuantite', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_PreRemplissageQuantite;

IF OBJECT_ID('dbo.Mobile_PreRemplissageTournee', 'U') IS NOT NULL
    DROP TABLE dbo.Mobile_PreRemplissageTournee;

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
   Role :
   Liste des articles affichables dans l'application mobile.
   Cette table permet d'ajouter un nouvel article sans modifier
   la structure SQL principale.
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
        CHECK (OrdreAffichage >= 0)
);
GO

CREATE INDEX IX_Mobile_ArticleSaisissable_ActifOrdre
ON dbo.Mobile_ArticleSaisissable (EstActif, EstVisibleMobile, OrdreAffichage);
GO

INSERT INTO dbo.Mobile_ArticleSaisissable
    (CodeArticle, LibelleArticle, OrdreAffichage, EstActif, EstVisibleMobile)
VALUES
    (N'ROLLS',       N'Rolls',       1, 1, 1),
    (N'ROLLS_VIDES', N'Rolls vides', 2, 1, 1),
    (N'TAPIS',       N'Tapis',       3, 1, 1),
    (N'SACS',        N'Sacs',        4, 1, 1);
GO

/* ============================================================
   TABLE : Mobile_UtilisateurExpedition
   Role :
   Comptes de l'interface expedition.

   Important :
   Le mot de passe ne doit jamais etre stocke en clair.
   Le champ MotDePasseHash doit contenir un hash gere par
   l'application web, jamais le mot de passe saisi.
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
   Role :
   Livreurs / chauffeurs utilisant l'application mobile.

   Source principale : v_chauffeurs
   Mapping conseille :
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

/* ============================================================
   TABLE : Mobile_ChargementTournee
   Role :
   Historise les chargements de tournee effectues le matin.
   Cela permet de diagnostiquer ce qui a ete envoye au mobile.
============================================================ */
CREATE TABLE dbo.Mobile_ChargementTournee (
    IdChargement BIGINT IDENTITY(1,1) NOT NULL,
    SchemaVersion NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Mobile_ChargementTournee_SchemaVersion DEFAULT N'1.2',
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
   Role :
   En-tete d'une synchronisation de tournee mobile.

   Protections :
   - UQ_Mobile_Tournee_IdSynchronisation : anti-rejeu technique.
   - UX_Mobile_Tournee_EnvoiUnique : anti-double envoi metier.
     Une seule tournee ENVOYEE est autorisee par DateTournee + CodeTournee,
     meme si le CodeLivreur est different.
============================================================ */
CREATE TABLE dbo.Mobile_Tournee (
    IdTourneeMobile BIGINT IDENTITY(1,1) NOT NULL,
    SchemaVersion NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Mobile_Tournee_SchemaVersion DEFAULT N'1.2',
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

/* ============================================================
   TABLE : Mobile_TourneeLigne
   Role :
   Ligne client / point de livraison / arret d'une tournee.

   Cette table conserve :
   - un snapshot des donnees ABSSolute utiles au mobile ;
   - un snapshot du commentaire exceptionnel envoye au mobile ;
   - les donnees de validation terrain du livreur ;
   - les totaux de compatibilite calcules depuis quantites[].
============================================================ */
CREATE TABLE dbo.Mobile_TourneeLigne (
    IdTourneeLigne BIGINT IDENTITY(1,1) NOT NULL,
    IdTourneeMobile BIGINT NOT NULL,
    IdLigneSource NVARCHAR(300) NOT NULL,

    /* Donnees client / PDL */
    NumClient NVARCHAR(50) NOT NULL,
    NomClient NVARCHAR(255) NOT NULL,
    NomAffiche NVARCHAR(255) NULL,
    CodePDL NVARCHAR(50) NULL,
    DescriptionPDL NVARCHAR(255) NULL,

    /* Adresse du point de livraison */
    AdresseLigne1 NVARCHAR(255) NULL,
    AdresseLigne2 NVARCHAR(255) NULL,
    AdresseLigne3 NVARCHAR(255) NULL,
    Ville NVARCHAR(100) NULL,
    CodePostal NVARCHAR(20) NULL,

    /* Informations de tournee */
    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    JourTournee INT NULL,
    JourLibelle NVARCHAR(30) NULL,
    SchemaLivraison NVARCHAR(100) NULL,
    OrdreArret INT NULL,
    Horaire NVARCHAR(50) NULL,

    /* Informations de retour */
    JourTourneeRetour INT NULL,
    JourRetourLibelle NVARCHAR(30) NULL,
    CodeTourneeRetour NVARCHAR(50) NULL,
    LibelleTourneeRetour NVARCHAR(255) NULL,

    /* Informations utiles au livreur */
    Instructions NVARCHAR(1000) NULL,
    CommentaireExceptionnel NVARCHAR(1000) NULL,
    ZoneDechargement NVARCHAR(100) NULL,
    ZoneDechargementAffichee NVARCHAR(150) NULL,
    Zone NVARCHAR(100) NULL,
    PrecisionInfo NVARCHAR(1000) NULL,
    Cle NVARCHAR(100) NULL,
    TypeLinge NVARCHAR(100) NULL,

    /* Fermeture */
    EstFerme BIT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_EstFerme DEFAULT 0,
    DateFermeture DATE NULL,
    MotifFermeture NVARCHAR(255) NULL,

    /* Totaux de compatibilite calcules depuis Mobile_TourneeLigneQuantite */
    QuantiteLivree INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_QuantiteLivree DEFAULT 0,
    QuantiteReprise INT NOT NULL
        CONSTRAINT DF_Mobile_TourneeLigne_QuantiteReprise DEFAULT 0,

    /* Anciennes colonnes conservees pour compatibilite temporaire */
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

    /* Saisie terrain par le livreur */
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
    CONSTRAINT CK_Mobile_TourneeLigne_QuantitesDetaillees
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
   Role :
   Source principale des quantites par article pour une ligne
   de tournee synchronisee.

   Colonnes :
   - QuantiteLivreePrevue : valeur proposee par l'expedition.
   - QuantiteLivree       : valeur reelle saisie ou confirmee.
   - QuantiteRecuperee    : valeur recuperee sur le terrain.
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
   TABLE : Mobile_PreRemplissageTournee
   Role :
   En-tete de preparation expedition pour une date et une tournee.
   Les modifications apres minuit doivent etre refusees par
   l'application web/API. La base conserve le verrouillage et
   l'historique.
============================================================ */
CREATE TABLE dbo.Mobile_PreRemplissageTournee (
    IdPreRemplissageTournee BIGINT IDENTITY(1,1) NOT NULL,
    DateTournee DATE NOT NULL,
    CodeTournee NVARCHAR(50) NOT NULL,
    LibelleTournee NVARCHAR(255) NULL,
    EstVerrouille BIT NOT NULL
        CONSTRAINT DF_Mobile_PreRemplissageTournee_EstVerrouille DEFAULT 0,
    DateVerrouillage DATETIMEOFFSET(0) NULL,
    IdUtilisateurCreation INT NULL,
    IdUtilisateurModification INT NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_PreRemplissageTournee_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_PreRemplissageTournee
        PRIMARY KEY (IdPreRemplissageTournee),
    CONSTRAINT FK_Mobile_PreRemplissageTournee_UtilisateurCreation
        FOREIGN KEY (IdUtilisateurCreation)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT FK_Mobile_PreRemplissageTournee_UtilisateurModification
        FOREIGN KEY (IdUtilisateurModification)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT UQ_Mobile_PreRemplissageTournee_DateCode
        UNIQUE (DateTournee, CodeTournee),
    CONSTRAINT CK_Mobile_PreRemplissageTournee_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_PreRemplissageTournee_Verrouillage
        CHECK (
            (EstVerrouille = 0 AND DateVerrouillage IS NULL)
            OR
            (EstVerrouille = 1 AND DateVerrouillage IS NOT NULL)
        )
);
GO

CREATE INDEX IX_Mobile_PreRemplissageTournee_Date
ON dbo.Mobile_PreRemplissageTournee (DateTournee, CodeTournee);
GO

CREATE INDEX IX_Mobile_PreRemplissageTournee_Verrouillage
ON dbo.Mobile_PreRemplissageTournee (EstVerrouille, DateTournee);
GO

/* ============================================================
   TABLE : Mobile_PreRemplissageQuantite
   Role :
   Quantites prevues par l'expedition, par ligne de tournee
   et par article. Ces valeurs alimentent QuantiteLivreePrevue
   dans le JSON de chargement mobile.
============================================================ */
CREATE TABLE dbo.Mobile_PreRemplissageQuantite (
    IdPreRemplissageQuantite BIGINT IDENTITY(1,1) NOT NULL,
    IdPreRemplissageTournee BIGINT NOT NULL,
    IdLigneSource NVARCHAR(300) NOT NULL,
    OrdreArret INT NULL,
    NumClient NVARCHAR(50) NOT NULL,
    NomClient NVARCHAR(255) NULL,
    CodePDL NVARCHAR(50) NULL,
    DescriptionPDL NVARCHAR(255) NULL,
    CodeArticle NVARCHAR(50) NOT NULL,
    LibelleArticle NVARCHAR(100) NULL,
    QuantiteLivreePrevue INT NULL,
    Actif BIT NOT NULL
        CONSTRAINT DF_Mobile_PreRemplissageQuantite_Actif DEFAULT 1,
    IdUtilisateurCreation INT NULL,
    IdUtilisateurModification INT NULL,
    DateCreation DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_PreRemplissageQuantite_DateCreation DEFAULT SYSDATETIMEOFFSET(),
    DateModification DATETIMEOFFSET(0) NULL,
    CONSTRAINT PK_Mobile_PreRemplissageQuantite
        PRIMARY KEY (IdPreRemplissageQuantite),
    CONSTRAINT FK_Mobile_PreRemplissageQuantite_Tournee
        FOREIGN KEY (IdPreRemplissageTournee)
        REFERENCES dbo.Mobile_PreRemplissageTournee(IdPreRemplissageTournee)
        ON DELETE CASCADE,
    CONSTRAINT FK_Mobile_PreRemplissageQuantite_Article
        FOREIGN KEY (CodeArticle)
        REFERENCES dbo.Mobile_ArticleSaisissable(CodeArticle),
    CONSTRAINT FK_Mobile_PreRemplissageQuantite_UtilisateurCreation
        FOREIGN KEY (IdUtilisateurCreation)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT FK_Mobile_PreRemplissageQuantite_UtilisateurModification
        FOREIGN KEY (IdUtilisateurModification)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT CK_Mobile_PreRemplissageQuantite_IdLigneSource_NonVide
        CHECK (LEN(LTRIM(RTRIM(IdLigneSource))) > 0),
    CONSTRAINT CK_Mobile_PreRemplissageQuantite_NumClient_NonVide
        CHECK (LEN(LTRIM(RTRIM(NumClient))) > 0),
    CONSTRAINT CK_Mobile_PreRemplissageQuantite_CodeArticle_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeArticle))) > 0),
    CONSTRAINT CK_Mobile_PreRemplissageQuantite_OrdreArret
        CHECK (OrdreArret IS NULL OR OrdreArret >= 0),
    CONSTRAINT CK_Mobile_PreRemplissageQuantite_QuantiteLivreePrevue
        CHECK (QuantiteLivreePrevue IS NULL OR QuantiteLivreePrevue >= 0)
);
GO

CREATE UNIQUE INDEX UX_Mobile_PreRemplissageQuantite_LigneArticle_Actif
ON dbo.Mobile_PreRemplissageQuantite (IdPreRemplissageTournee, IdLigneSource, CodeArticle)
WHERE Actif = 1;
GO

CREATE INDEX IX_Mobile_PreRemplissageQuantite_ClientPDL
ON dbo.Mobile_PreRemplissageQuantite (IdPreRemplissageTournee, NumClient, CodePDL);
GO

CREATE INDEX IX_Mobile_PreRemplissageQuantite_Article
ON dbo.Mobile_PreRemplissageQuantite (CodeArticle);
GO

/* ============================================================
   TABLE : Mobile_PreRemplissageHistorique
   Role :
   Historique des creations, modifications, suppressions et
   blocages des pre-remplissages expedition.
============================================================ */
CREATE TABLE dbo.Mobile_PreRemplissageHistorique (
    IdHistorique BIGINT IDENTITY(1,1) NOT NULL,
    IdPreRemplissageTournee BIGINT NULL,
    IdPreRemplissageQuantite BIGINT NULL,
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
        CONSTRAINT DF_Mobile_PreRemplissageHistorique_DateEvenement DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_Mobile_PreRemplissageHistorique
        PRIMARY KEY (IdHistorique),
    CONSTRAINT FK_Mobile_PreRemplissageHistorique_Tournee
        FOREIGN KEY (IdPreRemplissageTournee)
        REFERENCES dbo.Mobile_PreRemplissageTournee(IdPreRemplissageTournee),
    CONSTRAINT FK_Mobile_PreRemplissageHistorique_Quantite
        FOREIGN KEY (IdPreRemplissageQuantite)
        REFERENCES dbo.Mobile_PreRemplissageQuantite(IdPreRemplissageQuantite),
    CONSTRAINT FK_Mobile_PreRemplissageHistorique_Utilisateur
        FOREIGN KEY (IdUtilisateur)
        REFERENCES dbo.Mobile_UtilisateurExpedition(IdUtilisateurExpedition),
    CONSTRAINT CK_Mobile_PreRemplissageHistorique_CodeTournee_NonVide
        CHECK (LEN(LTRIM(RTRIM(CodeTournee))) > 0),
    CONSTRAINT CK_Mobile_PreRemplissageHistorique_Action
        CHECK (ActionHistorique IN (
            N'CREATION',
            N'MODIFICATION',
            N'SUPPRESSION',
            N'VERROUILLAGE',
            N'TENTATIVE_MODIFICATION_APRES_BLOCAGE'
        )),
    CONSTRAINT CK_Mobile_PreRemplissageHistorique_AncienneQuantite
        CHECK (AncienneQuantiteLivreePrevue IS NULL OR AncienneQuantiteLivreePrevue >= 0),
    CONSTRAINT CK_Mobile_PreRemplissageHistorique_NouvelleQuantite
        CHECK (NouvelleQuantiteLivreePrevue IS NULL OR NouvelleQuantiteLivreePrevue >= 0)
);
GO

CREATE INDEX IX_Mobile_PreRemplissageHistorique_DateTournee
ON dbo.Mobile_PreRemplissageHistorique (DateTournee, CodeTournee, DateEvenement DESC);
GO

CREATE INDEX IX_Mobile_PreRemplissageHistorique_Utilisateur
ON dbo.Mobile_PreRemplissageHistorique (IdUtilisateur, DateEvenement DESC);
GO

/* ============================================================
   TABLE : Mobile_CommentaireExceptionnel
   Role :
   Commentaires propres au projet mobile, lies a une date et
   a un client, eventuellement a un point de livraison.

   Ces commentaires ne viennent pas d'ABSSolute.
============================================================ */
CREATE TABLE dbo.Mobile_CommentaireExceptionnel (
    IdCommentaireExceptionnel BIGINT IDENTITY(1,1) NOT NULL,
    DateTournee DATE NOT NULL,
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
    CONSTRAINT CK_Mobile_CommentaireExceptionnel_NumClient_NonVide
        CHECK (LEN(LTRIM(RTRIM(NumClient))) > 0),
    CONSTRAINT CK_Mobile_CommentaireExceptionnel_Commentaire_NonVide
        CHECK (LEN(LTRIM(RTRIM(Commentaire))) > 0)
);
GO

CREATE UNIQUE INDEX UX_Mobile_CommentaireExceptionnel_Actif
ON dbo.Mobile_CommentaireExceptionnel (DateTournee, NumClient, CodePDL)
WHERE Actif = 1;
GO

CREATE INDEX IX_Mobile_CommentaireExceptionnel_DateClient
ON dbo.Mobile_CommentaireExceptionnel (DateTournee, NumClient, CodePDL, Actif);
GO

/* ============================================================
   TABLE : Mobile_LogSynchronisation
   Role :
   Journalise les evenements techniques et metier importants :
   chargement, envoi, double envoi, validation, erreurs,
   pre-remplissages, commentaires exceptionnels, exports.
============================================================ */
CREATE TABLE dbo.Mobile_LogSynchronisation (
    IdLog BIGINT IDENTITY(1,1) NOT NULL,
    IdTourneeMobile BIGINT NULL,
    IdLivreur INT NULL,
    IdSynchronisation UNIQUEIDENTIFIER NULL,
    DateEvenement DATETIMEOFFSET(0) NOT NULL
        CONSTRAINT DF_Mobile_LogSynchronisation_DateEvenement DEFAULT SYSDATETIMEOFFSET(),
    TypeEvenement NVARCHAR(50) NOT NULL,
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
   Role :
   Historique des exports administratifs.
   Le contenu complet de l'export n'est pas obligatoirement stocke.
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
   Role :
   Vue administrative simple pour consulter le detail des
   quantites par tournee, ligne et article.
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
   Role :
   Vue administrative de synthese par tournee et article.
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
   VERIFICATION RAPIDE DES OBJETS CREES
============================================================ */
SELECT
    TABLE_SCHEMA,
    TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
  AND TABLE_NAME LIKE 'Mobile_%'
ORDER BY TABLE_NAME;
GO

SELECT
    TABLE_SCHEMA,
    TABLE_NAME AS VIEW_NAME
FROM INFORMATION_SCHEMA.VIEWS
WHERE TABLE_NAME LIKE 'v_Mobile_%'
ORDER BY TABLE_NAME;
GO

SELECT
    i.name AS IndexName,
    OBJECT_NAME(i.object_id) AS TableName,
    i.is_unique,
    i.has_filter,
    i.filter_definition
FROM sys.indexes i
WHERE OBJECT_NAME(i.object_id) LIKE 'Mobile_%'
  AND i.name IS NOT NULL
ORDER BY TableName, IndexName;
GO
