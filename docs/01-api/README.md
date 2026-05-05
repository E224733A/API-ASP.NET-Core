# Documentation API

Cette documentation décrit l’état actuel de l’API après refactorisation qualité.

## Architecture du code

L’API est organisée par responsabilités.

```text
Controllers/
├── TourneesController.cs
└── SynchronisationsController.cs

Validators/
└── SynchronisationTourneeValidator.cs

Services/
└── TourneesService.cs

Mappers/
└── TourneeMobileMapper.cs

Repositories/
├── TourneesRepository.cs
└── SynchronisationsRepository.cs

Constants/
├── ApiErrorCodes.cs
├── ArticlesSaisissables.cs
├── SchemaVersions.cs
├── StatutsPassage.cs
└── StatutsSynchronisation.cs

Models/
└── DTOs de requête et de réponse
```

## Rôle des couches

### Controllers

Les contrôleurs gèrent uniquement la couche HTTP :

- lecture des paramètres ;
- appel du validateur ;
- appel du repository ou du service ;
- retour de la réponse HTTP.

Ils ne doivent pas contenir de logique métier lourde.

### Validators

Le validateur `SynchronisationTourneeValidator` centralise les règles métier du `POST /api/synchronisations`.

Il vérifie notamment :

- `schemaVersion = "1.1"` ;
- `idSynchronisation` obligatoire et valide ;
- `dateTournee` obligatoire ;
- `codeTournee` obligatoire ;
- `livreur.codeLivreur` obligatoire ;
- `mobile` obligatoire ;
- `lignes[]` non vide ;
- `idLigneSource` obligatoire et unique dans la requête ;
- `saisie.quantites[]` non vide ;
- quantités non négatives ;
- `A_FAIRE` interdit dans l’envoi final ;
- `NON_FAIT` et `ANOMALIE` avec commentaire obligatoire ;
- `heureValidation` obligatoire si `estValidee = true` ;
- `estValidee = true` obligatoire dans l’envoi final.

### Services

Le service `TourneesService` orchestre le chargement d’une tournée.

Il :

1. vérifie le code livreur ;
2. récupère le livreur depuis `v_chauffeurs` ;
3. récupère les lignes depuis `v_tournee` ;
4. appelle `TourneeMobileMapper`.

### Mappers

`TourneeMobileMapper` construit le JSON mobile du `GET /api/tournees/jour`.

Il est responsable de :

- `schemaVersion = "1.1"` ;
- `dateModifiable = false` ;
- génération de `idLigneSource` ;
- initialisation de `saisie.quantites[]` ;
- création de la structure mobile complète.

### Repositories

Les repositories gèrent uniquement l’accès SQL.

`TourneesRepository` lit les vues ABSSolute.

`SynchronisationsRepository` écrit dans les tables mobiles et consulte les synchronisations envoyées.

## Route GET tournée du jour

```http
GET /api/tournees/jour?dateTournee=2026-04-27&codeTournee=1001&codeLivreur=3
```

Cette route charge une tournée réelle depuis ABSSolute.

Réponse attendue :

```text
schemaVersion = 1.1
dateModifiable = false
livreur.codeLivreur = 3
livreur.nomLivreur = DAVID VARIN
codeTournee = 1001
libelleTournee = CHATAIGNERAIE LES HERBIERS
chargement.nombrePointsEnvoyes = 7
saisie.quantites[] présent dans chaque ligne
```

## Route POST synchronisation

```http
POST /api/synchronisations
```

Cette route reçoit l’envoi final du mobile.

Le format officiel est `schemaVersion = "1.1"` avec `saisie.quantites[]`.

## Réponses principales

Succès :

```json
{
  "statut": "SUCCESS",
  "message": "Synchronisation enregistrée avec succès."
}
```

Erreur de validation :

```json
{
  "statut": "VALIDATION_ERROR",
  "errors": []
}
```

Doublon technique :

```json
{
  "statut": "CONFLICT",
  "code": "SYNCHRONISATION_ALREADY_EXISTS",
  "message": "Cette synchronisation a déjà été reçue."
}
```

Doublon métier :

```json
{
  "statut": "CONFLICT",
  "code": "TOURNEE_ALREADY_SENT",
  "message": "Cette tournée a déjà été envoyée pour ce livreur et cette date."
}
```

Erreur technique :

```json
{
  "statut": "ERROR",
  "code": "TECHNICAL_ERROR",
  "message": "Erreur technique lors de la synchronisation."
}
```

## Consultation admin

### Liste

```http
GET /api/synchronisations
```

Filtres disponibles :

```http
GET /api/synchronisations?dateTournee=2026-04-27
GET /api/synchronisations?dateTournee=2026-04-27&codeTournee=1001
GET /api/synchronisations?dateTournee=2026-04-27&codeTournee=1001&codeLivreur=3
```

### Détail

```http
GET /api/synchronisations/{idTourneeMobile}
```

La consultation utilise maintenant des DTOs typés :

- `SynchronisationResumeDto` ;
- `SynchronisationDetailDto` ;
- `SynchronisationLigneDetailDto` ;
- `SynchronisationQuantiteDetailDto` ;
- `SynchronisationLogDto`.

Il ne faut plus utiliser `dynamic` pour ces réponses.
