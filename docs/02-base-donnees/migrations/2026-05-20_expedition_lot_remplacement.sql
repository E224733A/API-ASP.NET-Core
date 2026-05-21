USE [bd_eric];
GO

/* ============================================================
   Migration Expédition - Remplacement d'un lot global verrouillé

   Objectif :
   - Autoriser un nouveau verrouillage Expédition standard à remplacer
     l'ancien lot GLOBAL actif pour une même DateTournee.
   - Conserver l'historique des anciens lots en les passant à REMPLACE.
   - Ne pas modifier le contrat JSON API / SERVWEB.

   À exécuter une seule fois sur la base mobile utilisée par l'API.
============================================================ */

SET XACT_ABORT ON;
GO

/* 1. Autoriser le statut REMPLACE sur le journal des lots. */
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_Mobile_ExpeditionLotVerrouillage_StatutLot'
      AND parent_object_id = OBJECT_ID(N'dbo.Mobile_ExpeditionLotVerrouillage')
)
BEGIN
    ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
    DROP CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_StatutLot;
END;
GO

ALTER TABLE dbo.Mobile_ExpeditionLotVerrouillage
ADD CONSTRAINT CK_Mobile_ExpeditionLotVerrouillage_StatutLot
CHECK (StatutLot IN (N'VERROUILLE', N'REJOUE_IDENTIQUE', N'REFUSE', N'REMPLACE'));
GO

/* 2. Remplacer l'index unique DateTournee + CodeTournee + EmpreintePayload
      par un index non unique.

      Pourquoi :
      si un nouveau lot standard contient le même payload qu'un ancien lot
      remplacé, il doit pouvoir être journalisé avec un nouvel IdLotVerrouillage.
*/
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Mobile_ExpeditionLotVerrouillage_DateCodeEmpreinte'
      AND object_id = OBJECT_ID(N'dbo.Mobile_ExpeditionLotVerrouillage')
)
BEGIN
    DROP INDEX UX_Mobile_ExpeditionLotVerrouillage_DateCodeEmpreinte
    ON dbo.Mobile_ExpeditionLotVerrouillage;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Mobile_ExpeditionLotVerrouillage_DateCodeEmpreinte'
      AND object_id = OBJECT_ID(N'dbo.Mobile_ExpeditionLotVerrouillage')
)
BEGIN
    CREATE INDEX IX_Mobile_ExpeditionLotVerrouillage_DateCodeEmpreinte
    ON dbo.Mobile_ExpeditionLotVerrouillage (DateTournee, CodeTournee, EmpreintePayload);
END;
GO

/* 3. Garder l'index filtré qui garantit un seul lot actif VERROUILLE
      par DateTournee + CodeTournee.

      Le repository passe l'ancien lot GLOBAL / VERROUILLE en REMPLACE
      avant d'insérer le nouveau lot GLOBAL / VERROUILLE.
*/
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Mobile_ExpeditionLotVerrouillage_DateCodeVerrouille'
      AND object_id = OBJECT_ID(N'dbo.Mobile_ExpeditionLotVerrouillage')
)
BEGIN
    CREATE UNIQUE INDEX UX_Mobile_ExpeditionLotVerrouillage_DateCodeVerrouille
    ON dbo.Mobile_ExpeditionLotVerrouillage (DateTournee, CodeTournee)
    WHERE StatutLot = N'VERROUILLE';
END;
GO
