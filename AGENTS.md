@'
# Instructions projet pour Codex

## Contexte

Projet de stage : application mobile de dématérialisation des fiches de tournée livreur.

Architecture retenue :
- Application mobile Android, probablement .NET MAUI
- API ASP.NET Core
- SQL Server
- Lecture des données ABSSolute via vues SQL
- Écriture uniquement dans des tables dédiées Mobile_*
- Fonctionnement mobile hors connexion pendant la tournée
- Chargement le matin via Wi-Fi dépôt
- Synchronisation le soir via Wi-Fi dépôt
- API hébergée à terme dans une VM Windows sous IIS

## Règles importantes

Ne jamais connecter directement l’application mobile à SQL Server.

Ne jamais modifier les tables ABSSolute directement.

Ne pas inventer de colonnes SQL. Toujours vérifier les documents dans :
- docs/01-api/
- docs/02-base-donnees/

Ne pas mettre de mot de passe ou chaîne de connexion réelle dans le code source.

Les chaînes de connexion doivent passer par :
- dotnet user-secrets en local
- variables d’environnement en déploiement

## Routes principales

Route de lecture :
GET /api/tournees/jour

Objectif :
charger la tournée du jour depuis les vues ABSSolute et renvoyer un JSON complet au mobile.

Route de synchronisation :
POST /api/synchronisations

Objectif :
recevoir le JSON envoyé en fin de journée par le mobile, valider les données, bloquer les doublons et insérer dans les tables Mobile_*.

## Règles métier POST /api/synchronisations

Refuser :
- quantité négative
- statut NON_FAIT sans commentaire
- statut ANOMALIE sans commentaire
- ligne validée sans heureValidation
- statut A_FAIRE dans un envoi final
- doublon idLigneSource dans la même requête
- double envoi de la même tournée
- idSynchronisation déjà utilisé

Calculs côté API :
- QuantiteLivree = NbExpes + NbRolls + NbVetements + NbTapis + NbSacs
- QuantiteReprise = NbRecuperes

Le mobile ne doit pas calculer les quantités finales de compatibilité SQL.

## Base de données

Les tables mobiles sont documentées dans :
docs/02-base-donnees/schema-tables-mobiles.pdf

Tables principales :
- Mobile_Livreur
- Mobile_ChargementTournee
- Mobile_Tournee
- Mobile_TourneeLigne
- Mobile_LogSynchronisation
- Mobile_ExportAdmin

## Qualité attendue

Respecter :
- séparation Controller / Service / Repository
- Dapper pour les requêtes SQL
- transactions SQL pour la synchronisation
- validation claire avant insertion
- messages d’erreur compréhensibles
- logs dans Mobile_LogSynchronisation
- code lisible et maintenable

Ne pas faire de gros fichiers fourre-tout.
'@ | Set-Content AGENTS.md -Encoding UTF8