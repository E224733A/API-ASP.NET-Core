# API-ASP.NET-Core

## Préconditions

Avant de lancer l’API, vérifier que :

le SDK .NET est installé ;
SQL Server est accessible ;
les vues ABSSolute nécessaires sont accessibles ;
les tables Mobile_* existent ;
la chaîne de connexion est configurée ;
aucun secret n’est stocké dans Git.

Vérifier .NET :

```powershell
dotnet --info
```

Vérifier les workloads installés :
```powershell
dotnet workload list
```

## Lancer l’API en développement

```powershell
cd "C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\backend\API-ASP.NET-Core"
dotnet build
dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

La commande recommandée est :

```powershell
dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

--no-launch-profile permet d’éviter que launchSettings.json force une autre adresse ou un autre port.

Résultat attendu :
```powershell
Now listening on: http://127.0.0.1:5000
Application started.
```

## Arrêter l’API

Dans le terminal où l’API tourne :

Ctrl + C

Attendre l’arrêt propre de l’application.

Résultat attendu :

Application is shutting down...

## Vérifier que l’API répond

Dans un deuxième terminal :

```powershell
curl.exe "http://127.0.0.1:5000/api/health"
```

Résultat attendu : HTTP 200 OK

Selon la version, le JSON exact peut varier.

Tester aussi :

```powershell
curl.exe "http://127.0.0.1:5000/api/health/abssolute"
curl.exe "http://127.0.0.1:5000/api/health/mobile"
```

## Ouvrir Swagger

Une fois l’API lancée :

http://127.0.0.1:5000/swagger

Swagger permet de consulter et tester les routes API.

## Commandes de maintenance courantes

Restaurer les dépendances : 

```powershell
dotnet restore
```

Nettoyer la compilation : 

```powershell
dotnet clean
```

Compiler : 

```powershell
dotnet build
```

Lancer l’API : 

```powershell
dotnet run --no-launch-profile --urls "http://127.0.0.1:5000"
```

Compiler en Release : 

```powershell
dotnet build -c Release
```

Publier l’API :

```powershell
dotnet publish -c Release -o ".\publish"
```

Le dossier généré est :
backend\API-ASP.NET-Core\publish

## Vérifier le port 5000

Si l’API ne démarre pas parce que le port est déjà utilisé :

```powershell
netstat -ano | findstr :5000
```

Exemple de résultat :

TCP    127.0.0.1:5000    0.0.0.0:0    LISTENING    12345

Le dernier nombre est le PID du processus.

Pour arrêter ce processus :

```powershell
taskkill /PID 12345 /F
```

Remplacer 12345 par le vrai PID.

## Tester depuis un téléphone Android physique

Si le mobile utilise :

http://127.0.0.1:5000

il faut rediriger le port du téléphone vers le PC avec ADB.

```powershell
cd "C:\Program Files (x86)\Android\android-sdk\platform-tools"
.\adb.exe devices -l
.\adb.exe reverse --remove-all
.\adb.exe reverse tcp:5000 tcp:5000
.\adb.exe reverse --list
```

Résultat attendu :

tcp:5000 tcp:5000

Dans ce mode, 127.0.0.1:5000 côté téléphone pointe vers l’API lancée sur le PC.