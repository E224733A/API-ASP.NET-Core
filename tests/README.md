# Tests SERVWEB

Ce dossier contient ou doit contenir les tests automatisés du projet `servewebEXPE`.

## Objectif court terme

Ajouter des tests unitaires simples sur les validators déjà isolés :

```text
Application/Expedition/ExpeditionPreparationValidator.cs
Application/Administration/AdministrationCommentaireValidator.cs
```

## Commandes conseillées

Depuis la racine du dépôt :

```powershell
dotnet test .\tests\MobileSLI.Expedition.Web.Tests\MobileSLI.Expedition.Web.Tests.csproj -c Release
```

Si le projet de tests n’existe pas encore localement :

```powershell
mkdir .\tests\MobileSLI.Expedition.Web.Tests
dotnet new xunit -o .\tests\MobileSLI.Expedition.Web.Tests -f net8.0
dotnet add .\tests\MobileSLI.Expedition.Web.Tests\MobileSLI.Expedition.Web.Tests.csproj reference .\src\MobileSLI.Expedition.Web\MobileSLI.Expedition.Web.csproj
```
