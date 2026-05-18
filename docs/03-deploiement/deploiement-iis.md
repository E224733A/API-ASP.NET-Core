# Déploiement IIS

## Objectif

Déployer l'API ASP.NET Core sur un serveur Windows avec IIS.

## Prérequis

```text
IIS installé
ASP.NET Core Hosting Bundle installé
accès réseau entre IIS et SQL Server
chaîne de connexion configurée hors Git
certificat HTTPS en production
```

## Publier l'API

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"

dotnet publish -c Release -o ".\publish"
```

## Dossier publié

```text
backend\API-ASP.NET-Core\publish
```

## Points à vérifier dans IIS

```text
site IIS créé
pool d'application démarré
dossier publié sélectionné
Hosting Bundle ASP.NET Core installé
droits du compte IIS suffisants
connexion SQL Server fonctionnelle
HTTPS configuré en production
```

## Test après déploiement

```text
https://adresse-serveur/api/health
```

## Sécurité

Ne jamais stocker dans Git :

```text
chaîne de connexion réelle
mot de passe SQL Server
token
secret
certificat privé
```

Les routes `/api/debug/sql/*` doivent être désactivées, protégées ou réservées à un rôle administrateur avant production réelle.
