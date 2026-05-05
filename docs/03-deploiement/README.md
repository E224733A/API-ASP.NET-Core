# Documentation déploiement

Ce dossier contient les notes liées au déploiement de l’API.

## Architecture cible

L’application mobile ne doit pas accéder directement à SQL Server.

Le flux prévu est :

```text
Téléphone Android
→ API ASP.NET Core
→ SQL Server / vues ABSSolute
→ tables mobiles
```

## Déploiement cible

Cible recommandée :

- VM Windows ;
- ASP.NET Core ;
- hébergement sous IIS ;
- accès depuis le Wi-Fi du dépôt ;
- SQL Server accessible uniquement depuis la VM ou le réseau interne.

## Configuration réseau

Pour les tests en entreprise :

- le téléphone doit être connecté au Wi-Fi du dépôt ;
- l’API doit avoir une adresse stable ;
- l’application mobile doit utiliser l’URL de l’API ;
- SQL Server ne doit pas être exposé directement au mobile.

## Secrets et chaînes de connexion

Les chaînes de connexion ne doivent pas être versionnées dans Git.

En développement, utiliser :

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:AbssoluteConnection" "..."
dotnet user-secrets set "ConnectionStrings:MobileConnection" "..."
```

Ne pas mettre de mot de passe dans :

- `appsettings.json` ;
- `README.md` ;
- documentation publique ;
- captures d’écran partagées.

## HTTPS

En développement local, HTTP peut être utilisé.

En production, HTTPS doit être configuré proprement via IIS ou un reverse proxy.

## Tests de disponibilité

```powershell
curl.exe "http://adresse-api/api/synchronisations"
```
