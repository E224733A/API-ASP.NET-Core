# Tests Expédition

## Routes testées

```http
GET  /api/expedition/preparations/a-preparer
POST /api/expedition/preparations/verrouiller
```

## Lancer l'API

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"

$env:ASPNETCORE_ENVIRONMENT="Development"

dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

## Autoriser temporairement les scripts

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core\docs\04-tests\Expedition"

Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

Unblock-File ".\tests\Run-ExpeditionTests.ps1"
```

## Lancer les tests GET

```powershell
.\tests\Run-ExpeditionTests.ps1 -BaseUrl "http://127.0.0.1:5000"
```

## Lancer les tests GET + POST

Attention : les tests POST écrivent dans la base de développement.

```powershell
.\tests\Run-ExpeditionTests.ps1 -BaseUrl "http://127.0.0.1:5000" -RunWriteTests
```

## Tests attendus

```text
GET global sans filtre                         -> 200
GET avec dateTournee                           -> 400
GET avec codeTournee                           -> 400
GET avec codeLivreur                           -> 400
POST valide                                    -> 200
POST rejoué identique                          -> 200 / ALREADY_PROCESSED
POST rejoué différent                          -> 409
POST avec ROLLS_VIDES                          -> 400
POST avec quantité négative                    -> 400
POST sans idLotVerrouillage                    -> 400
```

## Fichiers

| Dossier | Rôle |
|---|---|
| `json` | Exemples JSON pour tests manuels |
| `tests` | Script PowerShell et fichier `.http` |
