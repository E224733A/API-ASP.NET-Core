# Déploiement API - Projet Mobile SLI

Ce fichier documente le déploiement de l'API ASP.NET Core sur `SRVAPI1`.

## 1. Environnement validé

| Élément | Valeur |
|---|---|
| VM API | `SRVAPI1` |
| IP API | `192.168.1.233` |
| OS | Windows 11 Professionnel 64 bits |
| Compte local utilisé | `SRVAPI1\nbracqadmin` |
| SDK requis | `.NET 10` |
| Hosting Bundle | Microsoft ASP.NET Core Hosting Bundle 10 |
| IIS | Activé |
| Base SQL cible | `bdd_eric` |
| SQL Server | `SRVSQL\SQL2017` |
| Port SQL | `61272` |
| URL API de test | `http://192.168.1.233:5000` |

La route suivante répond en Production depuis le poste de développement :

```powershell
curl.exe http://192.168.1.233:5000/api/health
```

Réponse validée :

```json
{"service":"API-ASP.NET-Core","status":"ok","environment":"Production"}
```

## 2. Installer .NET 10 SDK

Le projet cible `net10.0`. Le SDK .NET 8 ne suffit pas.

Sur `SRVAPI1`, ouvrir PowerShell en administrateur :

```powershell
winget install Microsoft.DotNet.SDK.10 --accept-package-agreements --accept-source-agreements
```

Vérifier :

```powershell
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

## 3. Installer Git

```powershell
winget install --id Git.Git -e --source winget --accept-package-agreements --accept-source-agreements
git --version
```

## 4. Cloner le dépôt API

```powershell
New-Item -ItemType Directory -Force -Path "C:\Sources" | Out-Null
cd C:\Sources
git clone https://github.com/E224733A/API-ASP.NET-Core.git
```

Pour les mises à jour futures :

```powershell
cd C:\Sources\API-ASP.NET-Core
git pull
```

## 5. Configurer les variables d'environnement

L'API utilise deux chaînes de connexion :

- `ConnectionStrings__AbssoluteConnection` : lecture des vues ABSSolute.
- `ConnectionStrings__MobileConnection` : lecture/écriture des tables `Mobile_*`.

Dans l'environnement actuel, les deux connexions pointent vers :

```text
Server=tcp:192.168.1.7,61272
Database=bdd_eric
```

Ne jamais écrire les mots de passe dans Git ou dans ce README.

### 5.1 Définir l'environnement Production

```powershell
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
```

### 5.2 Si deux accès SQL différents existent

Exécuter les lignes `Read-Host` une par une.

```powershell
$abssoluteUser = Read-Host "Utilisateur SQL Abssolute"
$abssolutePasswordSecure = Read-Host "Mot de passe SQL Abssolute" -AsSecureString

$mobileUser = Read-Host "Utilisateur SQL Mobile"
$mobilePasswordSecure = Read-Host "Mot de passe SQL Mobile" -AsSecureString
```

Puis exécuter :

```powershell
$bstrAbs = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($abssolutePasswordSecure)
$abssolutePassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstrAbs)

$bstrMob = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($mobilePasswordSecure)
$mobilePassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstrMob)

$abssoluteConnection = "Server=tcp:192.168.1.7,61272;Database=bdd_eric;User Id=$abssoluteUser;Password=$abssolutePassword;Encrypt=True;TrustServerCertificate=True;"
$mobileConnection = "Server=tcp:192.168.1.7,61272;Database=bdd_eric;User Id=$mobileUser;Password=$mobilePassword;Encrypt=True;TrustServerCertificate=True;"

[Environment]::SetEnvironmentVariable("ConnectionStrings__AbssoluteConnection", $abssoluteConnection, "Machine")
[Environment]::SetEnvironmentVariable("ConnectionStrings__MobileConnection", $mobileConnection, "Machine")

[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstrAbs)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstrMob)
```

Vérifier sans afficher les secrets :

```powershell
[Environment]::GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Machine")
[Environment]::GetEnvironmentVariable("ConnectionStrings__AbssoluteConnection", "Machine") -ne $null
[Environment]::GetEnvironmentVariable("ConnectionStrings__MobileConnection", "Machine") -ne $null
```

Résultat attendu :

```text
Production
True
True
```

## 6. Test simple avec dotnet run

```powershell
cd C:\Sources\API-ASP.NET-Core

dotnet restore
dotnet build
dotnet run --urls "http://0.0.0.0:5000"
```

Dans un deuxième PowerShell :

```powershell
curl.exe http://127.0.0.1:5000/api/health
curl.exe http://192.168.1.233:5000/api/health
```

## 7. Activer IIS

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName `
IIS-WebServerRole,`
IIS-WebServer,`
IIS-CommonHttpFeatures,`
IIS-DefaultDocument,`
IIS-StaticContent,`
IIS-HttpErrors,`
IIS-HealthAndDiagnostics,`
IIS-HttpLogging,`
IIS-RequestMonitor,`
IIS-Security,`
IIS-RequestFiltering,`
IIS-Performance,`
IIS-HttpCompressionStatic,`
IIS-ManagementConsole,`
IIS-WebSockets `
-All

iisreset
Get-Service W3SVC,WAS
Test-Path "C:\Windows\System32\inetsrv\inetmgr.exe"
```

## 8. Installer le Hosting Bundle .NET 10

```powershell
winget install --id Microsoft.DotNet.HostingBundle.10 -e --source winget --accept-package-agreements --accept-source-agreements
iisreset
dotnet --list-runtimes
```

## 9. Publier l'API en Release

Publier le `.csproj` directement pour éviter l'avertissement `NETSDK1194` lié à la publication d'une solution avec `-o`.

```powershell
cd C:\Sources\API-ASP.NET-Core

git pull

dotnet restore .\API-ASP.NET-Core.csproj
dotnet build .\API-ASP.NET-Core.csproj -c Release

New-Item -ItemType Directory -Force -Path "C:\Deploy\MobileSLI.Api" | Out-Null

dotnet publish .\API-ASP.NET-Core.csproj -c Release -o "C:\Deploy\MobileSLI.Api"
```

Vérifier :

```powershell
Test-Path "C:\Deploy\MobileSLI.Api\web.config"
Test-Path "C:\Deploy\MobileSLI.Api\API-ASP.NET-Core.dll"
```

## 10. Créer l'Application Pool IIS

```powershell
Import-Module WebAdministration

if (-not (Test-Path "IIS:\AppPools\MobileSLI.Api")) {
    New-WebAppPool -Name "MobileSLI.Api"
}

Set-ItemProperty "IIS:\AppPools\MobileSLI.Api" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\MobileSLI.Api" -Name processModel.loadUserProfile -Value $true
```

## 11. Donner les droits au dossier publié

```powershell
icacls "C:\Deploy\MobileSLI.Api" /grant "IIS AppPool\MobileSLI.Api:(OI)(CI)RX" /T
```

## 12. Créer le site IIS sur le port 5000

```powershell
Import-Module WebAdministration

if (Test-Path "IIS:\Sites\MobileSLI.Api") {
    Stop-Website -Name "MobileSLI.Api"
    Remove-Website -Name "MobileSLI.Api"
}

New-Website `
  -Name "MobileSLI.Api" `
  -PhysicalPath "C:\Deploy\MobileSLI.Api" `
  -Port 5000 `
  -IPAddress "*" `
  -ApplicationPool "MobileSLI.Api"
```

## 13. Ouvrir le pare-feu Windows

```powershell
if (-not (Get-NetFirewallRule -DisplayName "MobileSLI API HTTP 5000" -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule `
      -DisplayName "MobileSLI API HTTP 5000" `
      -Direction Inbound `
      -Protocol TCP `
      -LocalPort 5000 `
      -Action Allow
}
```

## 14. Démarrer et tester IIS

```powershell
iisreset
Start-WebAppPool "MobileSLI.Api"
Start-Website "MobileSLI.Api"

curl.exe http://localhost:5000/api/health
curl.exe http://127.0.0.1:5000/api/health
curl.exe http://192.168.1.233:5000/api/health
```

Depuis le poste de développement :

```powershell
Test-NetConnection 192.168.1.233 -Port 5000
curl.exe http://192.168.1.233:5000/api/health
```

## 15. Tester les routes métiers

Utiliser `-i` pour afficher le code HTTP.

```powershell
curl.exe -i "http://192.168.1.233:5000/api/tournees/disponibles?dateTournee=2026-05-07&codeLivreur=2"
curl.exe -i "http://192.168.1.233:5000/api/tournees/jour?dateTournee=2026-05-07&codeTournee=4006&codeLivreur=2"
```

Si `/api/health` fonctionne mais que les routes métiers échouent, vérifier :

1. les noms exacts des routes ;
2. les paramètres attendus ;
3. les chaînes de connexion IIS ;
4. les droits SQL ;
5. les données disponibles dans `bdd_eric` ;
6. les logs IIS et l'Observateur d'événements Windows.

## 16. Mise à jour future de l'API

```powershell
cd C:\Sources\API-ASP.NET-Core

git pull

Stop-Website "MobileSLI.Api"

dotnet restore .\API-ASP.NET-Core.csproj
dotnet build .\API-ASP.NET-Core.csproj -c Release
dotnet publish .\API-ASP.NET-Core.csproj -c Release -o "C:\Deploy\MobileSLI.Api"

Start-Website "MobileSLI.Api"

curl.exe http://localhost:5000/api/health
curl.exe http://192.168.1.233:5000/api/health
```

## 17. Étapes finales à faire plus tard

- Passer l'API de HTTP `5000` vers HTTPS `443`.
- Choisir le nom DNS final : `SRVAPI1.SLI.local` ou `api-mobile-sli.sli.local`.
- Générer et installer le certificat IIS.
- Valider que les téléphones Android font confiance au certificat.
- Créer un compte SQL dédié à l'API avec droits limités.
- Restreindre SQL Server pour autoriser uniquement `SRVAPI1` si possible.
- Fermer le port de test `5000` quand le port `443` est validé.
- Déployer ensuite `servewebEXPE` sur `SRVINTRAWEB1`.

## 18. Sécurité

Ne jamais stocker dans Git :

- mots de passe Windows ;
- mots de passe SQL ;
- chaînes de connexion contenant des secrets ;
- certificats privés ;
- tokens ou clés API.
