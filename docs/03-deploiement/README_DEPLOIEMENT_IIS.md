# Documentation déploiement IIS

Ce dossier documente le déploiement de l'API ASP.NET Core Mobile SLI sous IIS.

## Objectif

L'API doit être accessible aux téléphones Android connectés au Wi-Fi du dépôt.

Le mobile ne doit jamais accéder directement à SQL Server.

```text
Téléphone Android
-> Wi-Fi dépôt
-> IIS
-> API ASP.NET Core
-> SQL Server
   -> vues ABSSolute en lecture
   -> tables Mobile_* en écriture
```

## Architecture cible

Déploiement recommandé :

```text
VM Windows ou serveur Windows
├── IIS
├── .NET Hosting Bundle
├── API ASP.NET Core publiée
└── accès réseau interne vers SQL Server
```

SQL Server doit rester accessible uniquement depuis le serveur, la VM ou le réseau interne autorisé.

## Prérequis serveur

À installer ou vérifier sur la machine qui héberge IIS :

- IIS activé ;
- module ASP.NET Core Hosting Bundle installé ;
- accès réseau vers SQL Server ;
- compte SQL autorisé pour `AbssoluteConnection` ;
- compte SQL autorisé pour `MobileConnection` ;
- règle pare-feu entrante pour le port HTTP ou HTTPS ;
- adresse IP fixe ou réservation DHCP ;
- nom DNS interne recommandé si possible.

## Publication de l'API

Depuis le dossier du projet :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"

dotnet clean
dotnet restore
dotnet build
dotnet publish -c Release -o .\publish
```

Le dossier `publish` contient les fichiers à déployer dans IIS.

## Création du site IIS

Dans IIS Manager :

1. créer un nouveau site ou une nouvelle application ;
2. choisir le dossier physique `publish` ;
3. configurer le pool d'applications en `No Managed Code` ;
4. utiliser un compte avec les droits nécessaires sur le dossier ;
5. configurer le binding HTTP ou HTTPS ;
6. redémarrer le site.

Exemple de binding interne :

```text
Type : http
IP   : adresse IP de la VM ou du serveur
Port : 5120 ou 80 selon choix entreprise
Host : optionnel, par exemple api-mobile-sli.local
```

## Chaînes de connexion

Les chaînes de connexion ne doivent pas être stockées dans Git.

En développement local, utiliser `user-secrets` :

```powershell
dotnet user-secrets init

dotnet user-secrets set "ConnectionStrings:AbssoluteConnection" "Server=<SERVEUR_SQL>;Database=<BDD_ABSSOLUTE>;User Id=<USER>;Password=<PASSWORD>;Encrypt=False;TrustServerCertificate=True;"

dotnet user-secrets set "ConnectionStrings:MobileConnection" "Server=<SERVEUR_SQL>;Database=<BDD_MOBILE>;User Id=<USER>;Password=<PASSWORD>;Encrypt=False;TrustServerCertificate=True;"

dotnet user-secrets list
```

En déploiement IIS, utiliser plutôt :

- variables d'environnement Windows ;
- configuration serveur non versionnée ;
- coffre de secrets si l'entreprise en dispose.

Ne jamais mettre de mot de passe dans :

```text
appsettings.json
README.md
documentation publique
captures d'écran
GitHub
```

## Variables d'environnement IIS

Les variables attendues sont :

```text
ConnectionStrings__AbssoluteConnection
ConnectionStrings__MobileConnection
ASPNETCORE_ENVIRONMENT
```

Exemple de valeur pour l'environnement :

```text
ASPNETCORE_ENVIRONMENT=Production
```

## HTTPS

En développement local, HTTP peut être utilisé.

En pré-production ou production, HTTPS est recommandé.

Configuration possible :

```text
IIS
-> certificat interne ou certificat entreprise
-> binding HTTPS
-> API ASP.NET Core
```

Si HTTPS n'est pas encore disponible, limiter l'accès de l'API au réseau interne du dépôt.

## Tests après déploiement

Remplacer `<adresse-api>` par l'adresse réelle du site IIS.

```powershell
curl.exe "http://<adresse-api>/api/health"
curl.exe "http://<adresse-api>/api/health/abssolute"
curl.exe "http://<adresse-api>/api/health/mobile"
```

Résultat attendu :

```text
API disponible
AbssoluteConnection OK
MobileConnection OK
```

Tester ensuite les routes principales :

```powershell
curl.exe "http://<adresse-api>/api/livreurs"

curl.exe "http://<adresse-api>/api/tournees/disponibles?dateTournee=2026-05-07&codeLivreur=2"

curl.exe "http://<adresse-api>/api/tournees/jour?dateTournee=2026-05-07&codeTournee=4006&codeLivreur=2"

curl.exe "http://<adresse-api>/api/synchronisations"
```

## Test depuis un téléphone Android

Le téléphone doit être connecté au Wi-Fi du dépôt.

À vérifier :

```text
Wi-Fi dépôt connecté
adresse API accessible depuis le téléphone
pare-feu Windows ouvert pour le port IIS
SQL Server non exposé directement au téléphone
```

L'application mobile doit utiliser l'URL de base de l'API :

```text
http://<adresse-api>/api
```

## Points de sécurité minimum

Avant usage réel :

- ne pas exposer SQL Server au mobile ;
- ne pas stocker de secrets dans Git ;
- limiter l'API au réseau interne ;
- protéger ou désactiver les routes de debug SQL ;
- utiliser HTTPS dès que possible ;
- conserver les logs de synchronisation ;
- sauvegarder régulièrement la base mobile.

## Routes de debug

Les routes suivantes sont utiles en développement :

```text
/api/debug/sql/tables-mobile
/api/debug/sql/vues-abssolute
/api/debug/sql/chauffeurs
/api/debug/sql/tournees
/api/debug/sql/clients
/api/debug/sql/pdl-jour
```

Elles ne doivent pas rester accessibles librement en production.

## À retenir

Le déploiement cible reste simple :

```text
Mobile Android
-> API IIS interne
-> SQL Server
```

L'API centralise les règles métier, sécurise l'accès aux données et évite toute connexion directe entre le téléphone et SQL Server.
