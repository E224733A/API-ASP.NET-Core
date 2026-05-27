# Tests de masse API Mobile SLI

Ce dossier correspond à la priorité 2 des tests du projet MobileSLI.

Objectif : simuler 20 ou 50 synchronisations mobiles vers l'API centrale, produire un rapport avec les temps de réponse et les erreurs, puis contrôler la cohérence des données enregistrées en SQL Server.

## Emplacement attendu

Copier le dossier `masse` dans le dépôt API :

```text
API-ASP.NET-Core\docs\04-tests\masse
```

## Ce que contient le dossier

```text
masse/
  README.md
  COMMENT_APPLIQUER.md
  plan-tests-masse.md
  k6/
    post-synchronisations-masse.js
    post-synchronisations-mixte.js
  scripts/
    run-k6-masse.ps1
    run-k6-masse-20.ps1
    run-k6-masse-50.ps1
    build-rapport-k6.ps1
    generer-payloads-masse.ps1
    run-verification-sql-masse.ps1
  sql/
    verification-apres-k6.sql
    nettoyage-tests-masse.sql
  rapports/
    modele-rapport-tests-masse.md
  resultats/
    .gitkeep
  payloads/generated/
    .gitkeep
  config/
    masse.env.example
```

## Prérequis

Installer k6 sur le poste qui lance les tests :

```powershell
winget install k6.k6
```

Vérifier l'installation :

```powershell
k6 version
```

L'API centrale doit être accessible depuis le poste de test. Exemple :

```powershell
curl.exe http://localhost:5120/api/health
```

ou sur le réseau interne :

```powershell
curl.exe http://192.168.1.233:5000/api/health
```

## Test rapide avec 20 synchronisations

Depuis le dossier `API-ASP.NET-Core\docs\04-tests\masse` :

```powershell
.\scripts\run-k6-masse-20.ps1 -ApiBaseUrl "http://localhost:5120"
```

## Test plus large avec 50 synchronisations

```powershell
.\scripts\run-k6-masse-50.ps1 -ApiBaseUrl "http://localhost:5120"
```

## Test paramétrable

```powershell
.\scripts\run-k6-masse.ps1 `
  -ApiBaseUrl "http://localhost:5120" `
  -Count 50 `
  -Vus 10
```

Le script calcule automatiquement :

- la date de tournée autorisée selon l'heure Europe/Paris ;
- un identifiant de campagne `RunId` ;
- un préfixe unique de tournée de type `K6xxxxxx` pour éviter les conflits avec les exécutions précédentes ;
- un rapport Markdown dans `rapports/` ;
- un résumé JSON k6 dans `resultats/`.

## Vérification SQL après exécution

Après le test k6, lancer :

```powershell
.\scripts\run-verification-sql-masse.ps1 `
  -ServerInstance "NOM_SERVEUR_SQL" `
  -Database "NOM_BASE_SQL"
```

Par défaut, ce script lit `resultats\metadata-latest.json`, donc il réutilise automatiquement la date, le préfixe de tournée et le nombre de synchronisations attendues.

Exemple :

```powershell
.\scripts\run-verification-sql-masse.ps1 `
  -ServerInstance "SRVSQL\SQLEXPRESS" `
  -Database "LAVINPROSLI"
```

## Résultat attendu

Pour le test de masse valide :

```text
20 ou 50 réponses HTTP 200
0 erreur serveur 500
0 erreur fonctionnelle inattendue
nombre de tournées SQL = nombre de synchronisations attendues
nombre de lignes SQL = nombre de synchronisations x 5 lignes
nombre de quantités SQL = nombre de lignes x 4 articles
```

## Interprétation pédagogique

Ces tests valorisent le projet car ils montrent que l'application n'a pas seulement été essayée manuellement. Le comportement critique est testé avec une approche reproductible :

```text
scénario de masse -> mesure k6 -> rapport -> vérification SQL -> conclusion
```
