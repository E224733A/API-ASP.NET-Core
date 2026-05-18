# Décisions techniques

## API comme point d'entrée unique

L'API ASP.NET Core est le seul point d'entrée technique vers SQL Server pour les applications du projet.

```text
MobileSLI -> API ASP.NET Core -> SQL Server
Web Expédition -> API ASP.NET Core -> SQL Server
```

Le mobile ne se connecte jamais directement à SQL Server.

Le Web Expédition ne se connecte jamais directement à SQL Server pour écrire les données officielles.

## ABSSolute

ABSSolute reste la source métier historique.

Les vues ABSSolute sont utilisées en lecture seule.

L'API ne doit jamais modifier les tables internes ABSSolute.

## Tables dédiées MobileSLI

Les données produites par le projet sont stockées dans les tables dédiées `Mobile_*`.

Ces tables servent à conserver :

- les synchronisations mobiles ;
- les lignes de tournée ;
- les quantités par article ;
- les commentaires exceptionnels ;
- les pré-remplissages Expédition verrouillés ;
- les lots de verrouillage ;
- les logs.

## Mobile

Le mobile charge sa tournée au dépôt.

Il travaille ensuite hors connexion pendant la journée.

Au retour dépôt, il synchronise les données saisies vers l'API.

Routes principales :

```http
GET  /api/tournees/disponibles
GET  /api/tournees/jour
POST /api/synchronisations
```

## Expédition

Le Web Expédition prépare les quantités avant le départ.

Les brouillons restent côté Web Expédition dans SQLite local.

SQLite sert uniquement au brouillon local durable avant verrouillage.

La sauvegarde officielle se fait uniquement quand le Web Expédition appelle :

```http
POST /api/expedition/preparations/verrouiller
```

## GET Expédition

La route de chargement Expédition est globale :

```http
GET /api/expedition/preparations/a-preparer
```

Aucun paramètre n'est accepté.

Paramètres interdits :

```text
dateTournee
codeTournee
codeLivreur
```

Le choix de la tournée se fait côté Web Expédition après chargement complet.

## POST Expédition

La route de verrouillage Expédition reçoit un lot global :

```http
POST /api/expedition/preparations/verrouiller
```

Le lot est identifié par :

```text
idLotVerrouillage
```

Cette valeur permet l'idempotence.

Si le même lot est renvoyé avec le même contenu, l'API ne crée pas de doublon.

Si le même identifiant de lot est renvoyé avec un contenu différent, l'API retourne un conflit.

## Verrouillage métier

Le verrouillage métier Expédition se fait autour de :

```text
00:05
```

Le fuseau horaire métier est :

```text
Europe/Paris
```

## Articles Expédition

Articles autorisés côté Expédition :

```text
ROLLS
TAPIS
SACS
```

Article interdit côté Expédition :

```text
ROLLS_VIDES
```

`ROLLS_VIDES` peut être utile côté mobile pour représenter une récupération de rolls vides, mais il ne doit pas être utilisé comme quantité livrée prévue côté Expédition.

## Anti-doublon mobile

Anti-doublon technique :

```text
idSynchronisation
```

Anti-doublon métier :

```text
DateTournee + CodeTournee
```

Le code livreur est conservé pour la trace, mais il ne permet pas de renvoyer une deuxième fois la même tournée le même jour.
