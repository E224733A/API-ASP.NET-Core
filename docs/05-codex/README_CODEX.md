# Consignes pour Codex et les assistants de code

Ce dossier contient les règles à respecter pour modifier le projet sans casser l’architecture actuelle.

## Objectif

Le projet utilise une architecture par responsabilités.

À conserver :

```text
Controllers   -> couche HTTP
Validators    -> validation métier
Services      -> orchestration métier
Mappers       -> construction des DTOs de réponse
Repositories  -> accès SQL
Constants     -> valeurs métier centralisées
Models        -> DTOs de requête et de réponse
```

## Règles obligatoires

### Ne pas réintroduire l’ancien contrat JSON

Le contrat officiel est :

```text
schemaVersion = "1.2"
saisie.quantites[]
quantiteLivreePrevue
quantiteLivree
quantiteRecuperee
```

Ne pas revenir à l’ancien format :

```text
nbRolls
nbTapis
nbSacs
nbRecuperes
```

Ces anciennes colonnes SQL peuvent exister comme colonnes de compatibilité, mais elles doivent rester calculées par l’API.

### Respecter le sens de `quantiteLivreePrevue`

```text
null -> l’expédition n’a rien renseigné
0    -> l’expédition a volontairement prévu zéro
> 0  -> l’expédition a prévu une quantité
```

Ne jamais remplacer automatiquement `null` par `0` pour `quantiteLivreePrevue`.

### Ne pas remettre la validation dans les contrôleurs

La validation du `POST /api/synchronisations` doit rester dans :

```text
Validators/SynchronisationTourneeValidator.cs
```

Le contrôleur doit seulement :

```text
recevoir la requête
appeler le validateur
appeler le repository
retourner une réponse HTTP
```

### Ne pas utiliser `dynamic` pour la consultation admin

La consultation admin doit utiliser les DTOs typés :

```text
SynchronisationResumeDto
SynchronisationDetailDto
SynchronisationLigneDetailDto
SynchronisationQuantiteDetailDto
SynchronisationLogDto
```

Ne pas revenir à `QueryAsync<dynamic>` pour ces routes.

### Ne pas remettre le mapping dans `TourneesService`

Le mapping du `GET /api/tournees/jour` doit rester dans :

```text
Mappers/TourneeMobileMapper.cs
```

`TourneesService.cs` doit rester court.

### Utiliser les constantes métier

Ne pas écrire directement dans le code :

```text
A_FAIRE
FAIT
NON_FAIT
ANOMALIE
ENVOYEE
ROLLS
TAPIS
SACS
1.2
```

Utiliser les classes dans `Constants/`.

## Règles métier à préserver

### Chargement du matin

- le livreur s’identifie par code livreur ;
- la date est non modifiable ;
- le livreur choisit une tournée disponible ;
- le GET retourne `schemaVersion = "1.2"` ;
- chaque ligne contient `saisie.quantites[]` ;
- chaque quantité contient `quantiteLivreePrevue`, `quantiteLivree` et `quantiteRecuperee` ;
- les articles saisissables viennent de `Mobile_ArticleSaisissable` ;
- les pré-remplissages viennent des tables `Mobile_PreRemplissage*`.

### Envoi du soir

- `A_FAIRE` est interdit ;
- chaque ligne doit être validée ;
- `heureValidation` est obligatoire ;
- `NON_FAIT` et `ANOMALIE` exigent un commentaire ;
- les quantités ne peuvent pas être négatives ;
- `quantiteLivreePrevue` ne peut pas être négative si elle est renseignée ;
- l’envoi réussi verrouille la tournée.

### Anti-doublons

Deux protections existent :

```text
IdSynchronisation
DateTournee + CodeTournee + IdLivreur
```

La première empêche de recevoir deux fois le même paquet mobile.

La seconde empêche d’envoyer deux fois la même tournée du même jour par le même livreur.

Une même tournée peut être envoyée une autre semaine si `DateTournee` change.

## Règles SQL importantes

### ABSSolute

Les vues ABSSolute sont en lecture seule.

Ne jamais écrire dans les tables internes ABSSolute depuis l’API mobile.

### Tables mobiles

La source principale des quantités est :

```text
Mobile_TourneeLigneQuantite
```

Les tables de pré-remplissage doivent rester séparées des valeurs réellement envoyées par le mobile.

### Dédoublonnage

Certaines vues ABSSolute peuvent contenir plusieurs lignes pour le même client, point de livraison et jour.

Le repository doit garder le dédoublonnage avec `ROW_NUMBER()`.

Ne pas supprimer cette logique.

## Routes de debug

Les routes `/api/debug/sql/*` sont utiles en développement.

Elles ne doivent pas rester accessibles librement en production.

## Avant de modifier

Avant toute modification importante :

```powershell
dotnet build
```

Après modification d’un contrat JSON ou d’une règle métier :

```powershell
cd .\docs\04-tests
.\run-tests-post.ps1
```

## À retenir

Ne pas optimiser en cassant la séparation des responsabilités.

Ne pas simplifier le JSON en supprimant `quantiteLivreePrevue`.

Ne pas remettre SQL, validation et mapping dans les contrôleurs.
