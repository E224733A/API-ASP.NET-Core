# Tests API Mobile - Priorite 1

Ce dossier contient les tests API simples du contrat mobile.

Emplacement recommande :

```text
API-ASP.NET-Core\docs\04-tests\Mobile
```

## Objectif

Ces tests verifient rapidement les regles principales de synchronisation mobile :

- synchronisation valide ;
- doublon technique par `idSynchronisation` ;
- double envoi metier par `dateTournee + codeTournee` ;
- refus des quantites negatives ;
- refus des statuts `NON_FAIT` et `ANOMALIE` sans commentaire ;
- refus du statut `A_FAIRE` dans l'envoi final ;
- refus d'une ligne validee sans heure ;
- refus d'une ligne non validee ;
- refus des `idLigneSource` dupliques ;
- refus des `codeArticle` dupliques ;
- refus d'une version de schema non supportee ;
- refus d'un tableau `quantites[]` vide ;
- acceptation de `ROLLS_VIDES` en quantite livree ;
- acceptation de `ROLLS_VIDES` avec quantite prevue.

## Structure

```text
Mobile
├── README.md
├── resultats-tests.md
├── matrice-tests-mobile.md
├── payloads
│   ├── valides
│   ├── invalides
│   └── conflits
├── scripts
│   ├── run-api-mobile-tests.ps1
│   └── run-verification-sql-mobile.ps1
├── sql
│   └── verification_synchronisation_mobile_v12.sql
└── rapports
```

## Commande correcte pour lancer les tests API

Depuis la racine du depot API :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\docs\04-tests\Mobile\scripts\run-api-mobile-tests.ps1 -ApiBaseUrl "http://192.168.1.233:5000"
```

Depuis le dossier `docs\04-tests\Mobile\scripts` :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\run-api-mobile-tests.ps1 -ApiBaseUrl "http://192.168.1.233:5000"
```

Ou en deux commandes :

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\run-api-mobile-tests.ps1 -ApiBaseUrl "http://192.168.1.233:5000"
```

## Ce qu'il ne faut pas faire

Cette commande est incorrecte :

```powershell
.\docs\04-tests\Mobile
```

`Mobile` est un dossier, pas un script.

Cette commande est egalement incorrecte si elle est lancee depuis `Mobile\scripts` :

```powershell
.\docs\04-tests\Mobile
```

Depuis `Mobile\scripts`, il faut lancer :

```powershell
.\run-api-mobile-tests.ps1 -ApiBaseUrl "http://192.168.1.233:5000"
```

## Gestion automatique des dates

L'API mobile accepte uniquement la date metier mobile autorisee, donc la date du jour cote API.

Le script evite que les JSON de test deviennent obsoletes. Avant chaque envoi, il adapte automatiquement :

- `dateTournee` ;
- `mobile.dateChargementMobile` ;
- `mobile.dateEnvoiMobile` ;
- `saisie.heureValidation` quand elle existe ;
- `idLigneSource` ;
- `codeTournee` ;
- `idSynchronisation`.

Les fichiers JSON d'origine ne sont pas modifies. Les payloads reellement envoyes sont copies dans :

```text
Mobile\rapports\payloads-envoyes
```

## Resultats generes

Apres execution, le script cree :

```text
Mobile\rapports\rapport-tests-api-mobile.md
Mobile\rapports\resultats-tests-api-mobile.csv
Mobile\rapports\resultats-tests-api-mobile.json
Mobile\rapports\payloads-envoyes
Mobile\rapports\responses
```

## Verification SQL

Depuis la racine du depot API :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\docs\04-tests\Mobile\scripts\run-verification-sql-mobile.ps1 -ServerInstance "192.168.1.233" -Database "NOM_BASE_SQL"
```

Depuis le dossier `Mobile\scripts` :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\run-verification-sql-mobile.ps1 -ServerInstance "192.168.1.233" -Database "NOM_BASE_SQL"
```

## Pre-requis

Pour les tests API :

```text
PowerShell
API centrale demarree
Fichiers JSON presents dans payloads
```

Pour les verifications SQL :

```text
sqlcmd installe
Acces SQL Server autorise
Nom de base SQL connu
```

## En cas d'erreur

Si PowerShell refuse l'execution :

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
```

Si une erreur indique qu'un payload est introuvable, verifier que les fichiers JSON sont dans :

```text
Mobile\payloads
```

Le script cherche les payloads de maniere recursive.
