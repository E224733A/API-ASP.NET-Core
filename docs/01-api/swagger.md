# Documentation Swagger de l'API Mobile SLI

## Rôle de Swagger

Swagger est l'interface de documentation interactive de l'API ASP.NET Core.

Dans ce projet, Swagger sert à :

- consulter les routes disponibles ;
- voir les paramètres attendus ;
- comprendre les modèles JSON envoyés et reçus ;
- tester les routes sans écrire directement de commande `curl` ;
- vérifier les codes de réponse HTTP possibles ;
- documenter le contrat entre l'API et l'application mobile.

Swagger ne remplace pas le cahier des charges fonctionnel.  
Il documente surtout le contrat technique de l'API.

---

## Accès à Swagger

En développement local, lancer l'API :

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"
dotnet run --urls "http://127.0.0.1:5000"

Swagger disponible à cette adresse : http://127.0.0.1:5000/swagger/index.html
