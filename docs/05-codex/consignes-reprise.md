# Consignes de reprise du projet

## Règles non négociables

```text
Ne jamais connecter le mobile directement à SQL Server.
Ne jamais modifier les tables internes ABSSolute.
Ne jamais stocker de secrets dans Git.
Respecter schemaVersion = 1.2.
Ne pas réintroduire les anciennes routes Expédition.
Ne pas ajouter de filtres sur le GET Expédition.
Ne pas utiliser ROLLS_VIDES côté Expédition.
Conserver l'anti-doublon mobile DateTournee + CodeTournee.
```

## Routes Expédition définitives

```http
GET  /api/expedition/preparations/a-preparer
POST /api/expedition/preparations/verrouiller
```

## Routes mobile principales

```http
GET  /api/tournees/disponibles
GET  /api/tournees/jour
POST /api/synchronisations
```

## Articles Expédition

Autorisés :

```text
ROLLS
TAPIS
SACS
```

Interdit :

```text
ROLLS_VIDES
```

## Fonctionnement Expédition

```text
Le Web Expédition charge tout via le GET global.
Le choix de la tournée se fait côté Web.
Les brouillons restent dans SQLite local côté Web Expédition.
Le verrouillage officiel passe par l'API.
SQL Server conserve les données verrouillées.
Le mobile lit seulement les données verrouillées.
```

## Avant de modifier le code

Lire dans cet ordre :

```text
docs/00-cadrage/decisions-techniques.md
docs/01-api/routes-expedition.md
docs/01-api/routes-mobile.md
docs/01-api/contrats-json-expedition.md
docs/01-api/contrats-json-mobile.md
docs/02-base-donnees/README.md
docs/04-tests/Expedition/README.md
```

## Avant de livrer une modification

Exécuter :

```powershell
dotnet build
```

Puis tester :

```powershell
.\docs\04-tests\Expedition\tests\Run-ExpeditionTests.ps1 -BaseUrl "http://127.0.0.1:5000"
```

Si la modification touche les écritures Expédition, tester aussi :

```powershell
.\docs\04-tests\Expedition\tests\Run-ExpeditionTests.ps1 -BaseUrl "http://127.0.0.1:5000" -RunWriteTests
```
