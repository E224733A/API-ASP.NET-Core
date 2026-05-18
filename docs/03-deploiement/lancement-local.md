# Lancement local de l'API

## Prérequis

```text
.NET SDK installé
SQL Server accessible
chaîne de connexion configurée
tables Mobile_* présentes
vues ABSSolute accessibles
```

## Lancer l'API

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"

$env:ASPNETCORE_ENVIRONMENT="Development"

dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

## Tester l'API

Dans un deuxième terminal :

```powershell
Invoke-RestMethod "http://127.0.0.1:5000/api/health"
```

## Tester Swagger

```text
http://127.0.0.1:5000/swagger
```

## Vérifier le port

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
