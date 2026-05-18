# Test mobile avec ngrok

ngrok permet d'exposer temporairement l'API locale pour tester depuis un téléphone Android.

## Lancer l'API

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"

$env:ASPNETCORE_ENVIRONMENT="Development"

dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

## Lancer ngrok

Dans un autre terminal :

```powershell
ngrok http 5000
```

ngrok fournit une URL du type :

```text
https://feline-ninja-sandstone.ngrok-free.dev
```

## Tester l'URL ngrok depuis le PC

```powershell
Invoke-RestMethod "https://feline-ninja-sandstone.ngrok-free.dev/api/health"
```

## Configuration MobileSLI

Dans l'application mobile :

```text
Adresse API :
https://feline-ninja-sandstone.ngrok-free.dev
```

Puis :

```text
Enregistrer l'adresse
Tester la connexion
Charger la tournée
```

## Sécurité

ngrok expose temporairement l'API sur Internet.

Il doit être utilisé uniquement en développement.

Fermer ngrok après les tests.
