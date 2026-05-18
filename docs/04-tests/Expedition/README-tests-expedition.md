# Tests Expédition

Ce dossier contient des exemples JSON et des tests PowerShell pour les routes Expédition.

## Routes couvertes

- `GET /api/expedition/preparations/a-preparer`
- `POST /api/expedition/preparations/verrouiller`

## Règles testées

- Le GET Expédition est global.
- Le GET Expédition ne prend pas `dateTournee`, `codeTournee` ou `codeLivreur`.
- Le JSON retourné contient des tournées et des lignes exploitables par le Web Expédition.
- Les articles autorisés sont `ROLLS`, `SACS` et `TAPIS`.
- `ROLLS_VIDES` est interdit côté Expédition.
- Les quantités prévues peuvent être nulles avant saisie.
- Le verrouillage passe par un `idLotVerrouillage`.
- Les quantités négatives doivent être refusées.

## Exécution rapide

Depuis PowerShell :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --no-launch-profile --urls "http://127.0.0.1:5120"
```

Dans un second terminal :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core\docs\04-tests\Expedition"
.\tests\Run-ExpeditionTests.ps1 -BaseUrl "http://127.0.0.1:5120"
```

Pour lancer aussi les tests POST qui écrivent en base :

```powershell
.\tests\Run-ExpeditionTests.ps1 -BaseUrl "http://127.0.0.1:5120" -RunWriteTests
```

## Attention

Les tests GET sont non destructifs.

Les tests POST peuvent écrire dans les tables de préparation Expédition. Il faut les lancer sur une base de développement ou de test.
