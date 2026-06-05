# Tests Expédition

Ce dossier contient des exemples JSON et des tests PowerShell pour les routes Expédition.

## Routes couvertes

- `GET /api/expedition/preparations/a-preparer`
- `POST /api/expedition/preparations/verrouiller`

## Règles testées

- Le GET Expédition est global.
- Le GET Expédition ne prend pas `dateTournee`, `codeTournee` ou `codeLivreur`.
- Le JSON retourné contient des tournées et des lignes exploitables par le Web Expédition.
- Le contrat Expédition reste en `schemaVersion = "1.2"`.
- Les articles autorisés sont `ROLLS`, `ROLLS_VIDES`, `SACS` et `TAPIS`.
- `ROLLS_VIDES` est autorisé côté Expédition.
- Les quantités prévues peuvent être nulles avant saisie.
- Le verrouillage passe par un `idLotVerrouillage`.
- Les quantités négatives doivent être refusées.

## Exécution rapide

Depuis PowerShell :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\API\API-ASP.NET-Core"
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

Dans un second terminal :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\API\API-ASP.NET-Core"
.\docs\04-tests\Expedition\tests\Run-ExpeditionTests.ps1 -BaseUrl "http://127.0.0.1:5000"
```

Pour tester l'API serveur actuellement utilisée :

```powershell
.\docs\04-tests\Expedition\tests\Run-ExpeditionTests.ps1 -BaseUrl "http://srvapi1.sli.local:5000"
```

Pour lancer aussi les tests POST qui écrivent en base :

```powershell
.\docs\04-tests\Expedition\tests\Run-ExpeditionTests.ps1 -BaseUrl "http://srvapi1.sli.local:5000" -RunWriteTests
```

## Attention

Les tests GET sont non destructifs.

Les tests POST avec `-RunWriteTests` écrivent dans les tables de préparation Expédition. Il faut les lancer sur une base de développement ou de test.

## Nettoyage SQL après `-RunWriteTests`

Un script de nettoyage ciblé est fourni ici :

```text
sql/nettoyage-tests-expedition-run-write.sql
```

Il cible les lots créés par les tests dont le libellé commence par :

```text
Lot global Expédition TEST-EXPEDITION-
```

Le script est sécurisé par défaut :

```sql
DECLARE @ExecuteDelete bit = 0;
```

Pour supprimer réellement après vérification de l'aperçu :

```sql
DECLARE @ExecuteDelete bit = 1;
```

Tables couvertes par le nettoyage :

```text
dbo.Mobile_ExpeditionPreparationLigne
dbo.Mobile_ExpeditionPreparationHistorique
dbo.Mobile_ExpeditionPreparation
dbo.Mobile_ExpeditionLotVerrouillage
dbo.Mobile_LogSynchronisation
```
