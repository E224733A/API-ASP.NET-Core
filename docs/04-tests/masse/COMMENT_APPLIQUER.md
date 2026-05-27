# Comment appliquer le dossier de tests de masse

## 1. Copier le dossier

Décompresser le zip, puis copier le dossier `masse` ici :

```text
API-ASP.NET-Core\docs\04-tests\masse
```

## 2. Vérifier la structure

Depuis PowerShell :

```powershell
cd C:\Users\Logistique\Downloads\Stage\ProjetMobileTournee\API\API-ASP.NET-Core\docs\04-tests\masse
Get-ChildItem -Recurse
```

## 3. Vérifier k6

```powershell
k6 version
```

Si k6 n'est pas installé :

```powershell
winget install k6.k6
```

## 4. Lancer un test court

```powershell
.\scripts\run-k6-masse-20.ps1 -ApiBaseUrl "http://localhost:5120"
```

Pour l'API sur SERVAPI ou sur la VM :

```powershell
.\scripts\run-k6-masse-20.ps1 -ApiBaseUrl "http://192.168.1.233:5000"
```

## 5. Lancer un test plus valorisant

```powershell
.\scripts\run-k6-masse-50.ps1 -ApiBaseUrl "http://192.168.1.233:5000"
```

## 6. Lire le rapport k6

Les fichiers sont générés ici :

```text
rapports/
resultats/
```

Le rapport Markdown contient :

- le nombre de requêtes ;
- le nombre de synchronisations réussies ;
- le nombre d'erreurs ;
- le temps moyen ;
- le temps p95 ;
- une conclusion exploitable pour le dossier de test.

## 7. Vérifier la base SQL

```powershell
.\scripts\run-verification-sql-masse.ps1 `
  -ServerInstance "NOM_SERVEUR_SQL" `
  -Database "NOM_BASE_SQL"
```

Le script lit automatiquement le dernier test lancé grâce à :

```text
resultats\metadata-latest.json
```

## 8. Ce qu'il faut conserver comme preuve

Pour la soutenance et le rapport de tests, conserver :

```text
rapports\rapport-k6-masse-*.md
rapports\verification-sql-masse-*.txt
resultats\k6-summary-*.json
resultats\k6-console-*.log
resultats\metadata-*.json
```

## 9. Attention aux doublons

Chaque campagne utilise un préfixe de tournée unique. Cela évite que deux exécutions du même jour se bloquent sur la règle métier :

```text
une seule tournée ENVOYEE par DateTournee + CodeTournee
```

Ne forcez pas manuellement le même `RunId` plusieurs fois sur la même date, sauf pour tester volontairement les conflits.
