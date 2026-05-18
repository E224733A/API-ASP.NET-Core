# Test mobile avec ADB reverse

ADB reverse permet à un téléphone Android connecté en USB d'accéder à l'API locale du PC via `127.0.0.1`.

## Lancer l'API

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"

$env:ASPNETCORE_ENVIRONMENT="Development"

dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

## Se placer dans le dossier ADB

Exemple :

```powershell
cd "C:\Program Files (x86)\Android\android-sdk\platform-tools"
```

## Vérifier le téléphone

```powershell
.\adb.exe devices -l
```

## Configurer le reverse

```powershell
.\adb.exe reverse --remove-all
.\adb.exe reverse tcp:5000 tcp:5000
.\adb.exe reverse --list
```

Résultat attendu :

```text
tcp:5000 tcp:5000
```

## Configuration MobileSLI

Dans l'application mobile :

```text
http://127.0.0.1:5000
```

Puis :

```text
Enregistrer l'adresse
Tester la connexion
Charger la tournée
```

## Limite

Cette méthode fonctionne uniquement quand le téléphone est connecté en USB avec ADB actif.
