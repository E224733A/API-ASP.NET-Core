# Lancement local de l'API

## Prérequis

```text
.NET SDK installé
SQL Server accessible
chaîne de connexion configurée
tables Mobile_* présentes
vues ABSSolute accessibles
```

## Lancer l'API en développement

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\API\API-ASP.NET-Core"

$env:ASPNETCORE_ENVIRONMENT="Development"

dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

Le développement local reste en HTTP.

La production validée utilise l'URL HTTPS :

```text
https://srvapi1.sli.local
```

## Tester l'API locale

Dans un deuxième terminal :

```powershell
Invoke-RestMethod "http://127.0.0.1:5000/api/health"
```

## Tester l'API production HTTPS

Depuis une machine qui résout `srvapi1.sli.local` :

```powershell
Invoke-WebRequest "https://srvapi1.sli.local/api/health" -UseBasicParsing
```

Résultat attendu : HTTP 200.

## Tester Swagger en local

```text
http://127.0.0.1:5000/swagger
```

Swagger est activé uniquement en environnement `Development`.

## Vérifier le port local

```powershell
netstat -ano | findstr ":5000"
```

Si le port est déjà utilisé :

```powershell
taskkill /PID 12345 /F
```

Remplacer `12345` par le PID réel.

## Commandes utiles

```powershell
dotnet restore
dotnet clean
dotnet build
dotnet build -c Release
dotnet publish -c Release -o ".\publish"
```

## Tests API Mobile stricts 1.3

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force

.\docs\04-tests\Mobile\scripts\run-api-mobile-tests.ps1 `
  -ApiBaseUrl "https://srvapi1.sli.local" `
  -DateTournee "2026-06-09"
```

Adapter `DateTournee` à la date métier autorisée au moment du test.