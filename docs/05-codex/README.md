# Consignes pour Codex et les assistants de code

Ce dossier contient les règles à respecter pour modifier le projet sans casser l’architecture actuelle.

## Objectif

Le projet utilise maintenant une structure plus propre.

Il faut conserver cette organisation :

```text
Controllers  -> couche HTTP
Validators   -> validation métier
Services     -> orchestration métier
Mappers      -> construction des DTOs de réponse
Repositories -> accès SQL
Constants    -> valeurs métier centralisées
Models       -> DTOs de requête et de réponse
```

## Règles obligatoires

### Ne pas réintroduire l’ancien contrat JSON

Ne jamais réintroduire l’ancien format de saisie mobile.

Le format officiel est :

```text
saisie.quantites[]
quantiteLivree
quantiteRecuperee
```

Les colonnes SQL de compatibilité peuvent encore exister, mais elles doivent être calculées par l’API.

### Ne pas remettre la validation dans le contrôleur

La validation de `POST /api/synchronisations` doit rester dans :

```text
Validators/SynchronisationTourneeValidator.cs
```

### Ne pas utiliser dynamic pour la consultation admin

La consultation admin doit utiliser :

```text
SynchronisationResumeDto
SynchronisationDetailDto
SynchronisationLigneDetailDto
SynchronisationQuantiteDetailDto
SynchronisationLogDto
```

Ne pas revenir à `QueryAsync<dynamic>` pour ces routes.

### Ne pas remettre le mapping dans TourneesService

Le mapping du GET tournée doit rester dans :

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
1.1
```

Utiliser les classes dans `Constants/`.

## Règles métier à préserver

### Chargement du matin

- le livreur s’identifie par code livreur ;
- la date est non modifiable ;
- le livreur choisit sa tournée ;
- le GET retourne `schemaVersion = "1.1"` ;
- chaque ligne contient `saisie.quantites[]`.

### Envoi du soir

- `A_FAIRE` est interdit ;
- chaque ligne doit être validée ;
- `heureValidation` est obligatoire ;
- `NON_FAIT` et `ANOMALIE` exigent un commentaire ;
- les quantités ne peuvent pas être négatives ;
- l’envoi réussi verrouille la tournée.

### Anti-doublons

Deux anti-doublons existent :

```text
IdSynchronisation
DateTournee + CodeTournee + IdLivreur
```

Le premier empêche de recevoir deux fois le même paquet mobile.

Le second empêche d’envoyer deux fois la même tournée du même jour par le même livreur.

Une même tournée peut exister une autre semaine si `DateTournee` change.

## Règle SQL importante

La vue `v_pdl_jour` peut contenir plusieurs lignes pour le même client / PDL / jour.

Le repository doit garder le dédoublonnage avec `ROW_NUMBER()`.

Ne pas supprimer cette logique.
