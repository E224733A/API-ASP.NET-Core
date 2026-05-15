# Documentation base de données

Ce dossier documente la base SQL Server dédiée au projet mobile SLI.

## Principe général

Les vues ABSSolute restent la source de lecture métier.

L'API lit les données ABSSolute, mais ne modifie jamais les tables internes ABSSolute.

Les données saisies par les livreurs, les synchronisations, les pré-remplissages, les verrouillages Expédition et les logs sont stockés dans les tables `Mobile_*`.

```text
ABSSolute / vues SQL -> API ASP.NET Core -> Application mobile
Application mobile -> API ASP.NET Core -> Tables Mobile_*
Application web Expédition -> API ASP.NET Core -> Tables Mobile_*
```

## Organisation des scripts SQL

Le dossier base de données peut contenir deux types de scripts. Ils ne doivent pas avoir le même rôle.

### Script complet

Exemple :

```text
BDD_sli_v12_corrigee.sql
```

Rôle : créer ou recréer toute la base dédiée au projet mobile.

Ce script est utile en développement ou en test lorsqu'il faut repartir d'une base propre.

Point de vigilance : ce script est destructif s'il contient des instructions `DROP TABLE` ou `DROP VIEW`. Il ne doit pas être exécuté sur une base contenant des données à conserver sans sauvegarde préalable.

### Scripts de migration

Exemple :

```text
expedition_v1_ajouts_verrouillage.sql
```

Rôle : faire évoluer une base existante sans tout supprimer.

Ce type de script sert à ajouter une table, une colonne, une contrainte ou un index lorsqu'une nouvelle fonctionnalité est ajoutée.

Pour la partie Expédition, le script de migration ajoute notamment la gestion du lot de verrouillage et les champs nécessaires au verrouillage idempotent.

### Règle recommandée

En développement ou en test, pour repartir de zéro :

```text
1. Exécuter BDD_sli_v12_corrigee.sql
2. Exécuter les migrations nécessaires, dans l'ordre chronologique
```

Sur une base déjà utilisée, ne pas relancer le script complet destructif. Exécuter uniquement les migrations nécessaires.

### Organisation conseillée

Organisation simple acceptable :

```text
docs/02-base-donnees/
├── README_BDD.md
├── BDD_sli_v12_corrigee.sql
└── expedition_v1_ajouts_verrouillage.sql
```

Organisation recommandée à terme :

```text
docs/02-base-donnees/
├── README_BDD.md
├── complete/
│   └── BDD_sli_v13_complete.sql
└── migrations/
    └── 2026-05-15_expedition_verrouillage_lot.sql
```

Quand la partie Expédition sera stabilisée, il sera préférable de créer une nouvelle version complète, par exemple `BDD_sli_v13_complete.sql`, qui intègre directement les ajouts de la migration Expédition. La migration doit quand même rester conservée pour documenter le passage de la version précédente vers la nouvelle.

## Tables principales

### Mobile_Livreur

Référence les livreurs connus côté mobile.

Alimentation principale :

```text
v_chauffeurs.DRIVERNUMERO -> CodeLivreur
v_chauffeurs.DRIVERNAME   -> NomLivreur
```

### Mobile_ChargementTournee

Historise les chargements de tournée du matin.

Cette table permet de savoir quelle tournée a été envoyée au mobile, quand, sur quel appareil et avec combien de points.

### Mobile_Tournee

Stocke l'en-tête d'une tournée synchronisée en fin de journée.

Une ligne correspond à une tournée envoyée par un livreur pour une date donnée.

Champs importants :

```text
IdSynchronisation
DateTournee
CodeTournee
IdLivreur
StatutSynchronisation
DateEnvoi
EstVerrouillee
NomAppareil
VersionApplication
```

### Mobile_TourneeLigne

Stocke les arrêts de tournée : client, point de livraison, adresse, informations livreur et saisie terrain.

Une ligne correspond à un arrêt de tournée.

Les colonnes `QuantiteLivree` et `QuantiteReprise` sont des totaux de compatibilité calculés depuis `Mobile_TourneeLigneQuantite`.

### Mobile_TourneeLigneQuantite

Table principale des quantités saisies ou confirmées par le mobile.

Une ligne correspond à un article saisi pour un arrêt.

Champs importants :

```text
CodeArticle
LibelleArticle
QuantiteLivreePrevue
QuantiteLivree
QuantiteRecuperee
```

Règle métier :

```text
QuantiteLivreePrevue = NULL -> l'expédition n'a rien renseigné
QuantiteLivreePrevue = 0    -> l'expédition a volontairement prévu zéro
QuantiteLivreePrevue > 0    -> l'expédition a prévu une quantité
```

Ce modèle permet d'ajouter de nouveaux articles sans modifier la structure principale.

### Mobile_ArticleSaisissable

Liste les articles affichés dans l'application mobile.

Première version :

```text
ROLLS
TAPIS
SACS
```

Le code `ROLLS_VIDES` peut exister pour la saisie mobile de récupération, mais il ne doit pas être utilisé comme quantité livrée prévue côté Expédition.

## Tables liées à l'Expédition

### Mobile_PreRemplissageTournee

Stocke l'en-tête des pré-remplissages Expédition pour une date et une tournée.

Dans le fonctionnement retenu, les brouillons avant verrouillage restent côté application web Expédition, dans SQLite local. SQL Server reçoit uniquement les données envoyées par l'API au moment du verrouillage.

Champs importants :

```text
DateTournee
CodeTournee
LibelleTournee
EstVerrouille
DateVerrouillage
IdLotVerrouillage
DateReceptionApi
```

`IdLotVerrouillage` permet de rattacher la tournée au lot POST envoyé par l'application web Expédition à 00:05.

### Mobile_PreRemplissageQuantite

Stocke les quantités prévues par article après verrouillage Expédition.

Le pré-remplissage concerne uniquement la partie `Livré prévu`.

Champs importants :

```text
IdPreRemplissageTournee
IdLigneSource
CodeArticle
QuantiteLivreePrevue
Actif
DateCreation
DateModification
```

Règles métier :

```text
QuantiteLivreePrevue = NULL -> non renseigné
QuantiteLivreePrevue = 0    -> zéro prévu explicitement
QuantiteLivreePrevue > 0    -> quantité prévue
QuantiteLivreePrevue < 0    -> interdit
```

Les quantités récupérées ne sont pas préparées par l'Expédition.

### Mobile_PreRemplissageHistorique

Trace les actions importantes liées aux pré-remplissages.

Exemples :

```text
CREATION
MODIFICATION
SUPPRESSION
VERROUILLAGE
TENTATIVE_MODIFICATION_APRES_BLOCAGE
```

En première version, il n'est pas obligatoire de tracer le poste Expédition, car l'utilisation prévue se fait depuis un seul poste. La dernière modification valide est conservée avec son horaire de validation lorsque l'information est disponible.

### Mobile_ExpeditionLotVerrouillage

Table ajoutée pour gérer le verrouillage global de l'Expédition.

Une ligne correspond à un lot de verrouillage envoyé par l'application web Expédition vers l'API.

Rôle principal : rendre le `POST /api/expedition/preparations/verrouiller` idempotent.

Champs importants :

```text
IdLotVerrouillage
DateTournee
DateReceptionApi
Statut
Message
NombreTournees
NombreLignes
NombreQuantites
AdresseIP
DateCreation
```

Règle d'idempotence :

```text
Même IdLotVerrouillage envoyé plusieurs fois avec le même contenu
-> l'API ne doit pas créer de doublon.

Même tournée déjà verrouillée
-> l'API retourne un statut de type ALREADY_LOCKED ou CONFLICT selon le cas.
```

Statuts attendus :

```text
SUCCESS
VALIDATION_ERROR
LOCK_WINDOW_ERROR
ALREADY_PROCESSED
ALREADY_LOCKED
CONFLICT
TECHNICAL_ERROR
```

### Mobile_CommentaireExceptionnel

Stocke les commentaires exceptionnels propres au projet mobile.

Ces commentaires ne viennent pas d'ABSSolute.

Ils sont liés à une date, un client et éventuellement un point de livraison.

Le commentaire exceptionnel doit rester séparé des instructions ABSSolute.

### Mobile_LogSynchronisation

Stocke les événements importants liés aux synchronisations, aux chargements, aux erreurs et aux traitements Expédition.

Exemples :

```text
CHARGEMENT_TOURNEE
ENVOI_REUSSI
ERREUR_VALIDATION
DOUBLE_ENVOI
CHARGEMENT_PRE_REMPLISSAGE
CREATION_PRE_REMPLISSAGE
MODIFICATION_PRE_REMPLISSAGE
BLOCAGE_PRE_REMPLISSAGE
ERREUR_PRE_REMPLISSAGE
COMMENTAIRE_EXCEPTIONNEL
```

### Mobile_ExportAdmin

Prévu pour tracer les exports ou consultations administratives futures.

## Anti-doublons

### Anti-doublon technique mobile

Empêche le même paquet mobile d'être enregistré deux fois.

```text
IdSynchronisation unique
```

### Anti-doublon métier mobile

Empêche la même tournée d'être envoyée plusieurs fois pour la même date selon la règle métier retenue.

```text
DateTournee + CodeTournee
```

Le choix actuel ne met pas `IdLivreur` dans la règle métier principale, car une tournée verrouillée ou synchronisée pour une date et un code tournée ne doit pas être doublonnée.

Une même tournée peut être envoyée une semaine plus tard, car `DateTournee` change.

### Anti-doublon Expédition

Le verrouillage Expédition utilise un identifiant de lot.

```text
IdLotVerrouillage unique
```

Objectif : permettre à l'application web Expédition de réessayer un `POST` de verrouillage sans créer de doublon si la réponse API a été perdue ou si la tâche automatique s'est relancée.

## Fonctionnement Expédition

Le module Expédition fonctionne en deux temps.

### Avant verrouillage

L'application web Expédition charge les données préparables via l'API.

Les modifications utilisateur sont conservées côté application web Expédition dans SQLite local.

SQLite local sert uniquement de stockage brouillon durable. Il ne remplace pas SQL Server et ne constitue pas la sauvegarde métier définitive.

### Au verrouillage

À 00:05, l'application web Expédition déclenche :

```http
POST /api/expedition/preparations/verrouiller
```

La règle d'architecture est :

```text
Le backend web déclenche.
L'API vérifie.
SQL Server trace.
```

L'API ne doit pas faire confiance aveuglément au serveur web. Elle doit recontrôler au minimum :

```text
dateTournee
heure de verrouillage
statut de préparation
idLotVerrouillage
idLigneSource
codeTournee
codeArticle
quantités
commentaires
```

Si le lot est valide, l'API écrit les données dans SQL Server et marque les préparations comme verrouillées.

### Après verrouillage

Le mobile lit uniquement les données verrouillées présentes en SQL Server.

Une préparation encore en brouillon SQLite côté application web Expédition n'est jamais visible par le mobile.

## Format JSON associé

La base est alignée avec le contrat JSON API `1.2` pour le mobile.

Le point central est le tableau `quantites[]` :

```json
{
  "quantites": [
    {
      "codeArticle": "ROLLS",
      "libelle": "Rolls",
      "quantiteLivreePrevue": null,
      "quantiteLivree": 1,
      "quantiteRecuperee": 2
    }
  ]
}
```

La source principale des quantités synchronisées est donc :

```text
Mobile_TourneeLigneQuantite
```

La source des quantités préparées par l'Expédition après verrouillage est :

```text
Mobile_PreRemplissageQuantite
```

Les anciennes colonnes de type `NbRolls`, `NbTapis`, `NbSacs`, `NbRecuperes` sont uniquement des colonnes de compatibilité lorsqu'elles existent.

## Dédoublonnage du GET

Certaines vues ABSSolute peuvent contenir plusieurs lignes pour un même client, point de livraison et jour.

Le repository utilise `ROW_NUMBER()` pour ne garder qu'une seule ligne par arrêt.

Objectif : éviter d'envoyer deux fois le même arrêt au mobile ou au module Expédition.

## Vérifications après synchronisation mobile

Après un `POST /api/synchronisations` réussi, vérifier :

```sql
SELECT TOP 10 *
FROM dbo.Mobile_Tournee
ORDER BY IdTourneeMobile DESC;

SELECT TOP 50 *
FROM dbo.Mobile_TourneeLigne
ORDER BY IdTourneeMobile DESC, OrdreArret;

SELECT TOP 100 *
FROM dbo.Mobile_TourneeLigneQuantite
ORDER BY IdQuantite DESC;

SELECT TOP 20 *
FROM dbo.Mobile_LogSynchronisation
ORDER BY IdLog DESC;
```

Résultat attendu :

```text
Mobile_Tournee                  -> 1 en-tête de tournée
Mobile_TourneeLigne             -> 1 ligne par arrêt
Mobile_TourneeLigneQuantite     -> 1 ligne par article et par arrêt
Mobile_LogSynchronisation       -> 1 log ENVOI_REUSSI ou équivalent
```

## Vérifications après verrouillage Expédition

Après un `POST /api/expedition/preparations/verrouiller` réussi, vérifier :

```sql
SELECT TOP 10 *
FROM dbo.Mobile_ExpeditionLotVerrouillage
ORDER BY DateReceptionApi DESC;

SELECT TOP 20 *
FROM dbo.Mobile_PreRemplissageTournee
ORDER BY DateVerrouillage DESC;

SELECT TOP 100 *
FROM dbo.Mobile_PreRemplissageQuantite
ORDER BY DateCreation DESC;

SELECT TOP 20 *
FROM dbo.Mobile_PreRemplissageHistorique
ORDER BY DateEvenement DESC;
```

Résultat attendu :

```text
Mobile_ExpeditionLotVerrouillage -> 1 lot de verrouillage SUCCESS
Mobile_PreRemplissageTournee     -> tournées verrouillées avec IdLotVerrouillage
Mobile_PreRemplissageQuantite    -> quantités prévues par idLigneSource et codeArticle
Mobile_PreRemplissageHistorique  -> trace du verrouillage
```

## À retenir

La base mobile ne remplace pas ABSSolute.

Elle sert à conserver les données terrain saisies par les livreurs, les quantités prévues et réelles, les commentaires exceptionnels, les lots de verrouillage Expédition, les logs et les futures exploitations administratives.

Le script complet sert à recréer la base en développement ou en test.

Les scripts de migration servent à faire évoluer une base existante sans supprimer les données.

Pour le module Expédition, SQLite local côté application web sert au brouillon avant 00:05. SQL Server conserve uniquement les données verrouillées et officielles.
