# Test POST /api/synchronisations validé

Date du test : 2026-04-30

Exemple de commande utilisée :

```powershell
curl.exe -X POST "http://localhost:5120/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync.json"

  {"statut":"SUCCESS","message":"Synchronisation enregistrée avec succès."}
```

Si on essaye d'envoyer 2 fois ça ne fonctionne pas :
 
```powershell
{"statut":"CONFLICT","message":"Une synchronisation avec l'ID 4e17a871-5fc5-4f49-8d01-4d791a6d9941 existe déjà."}
```

Copier/coller ce bloc pour effectuer les tests : 

```powershell

$ApiUrl = "http://localhost:5120/api/synchronisations"

function Test-Synchronisation {
    param(
        [string]$NomTest,
        [string]$Fichier,
        [string]$ResultatAttendu
    )

    Write-Host ""
    Write-Host "============================================================"
    Write-Host "TEST : $NomTest"
    Write-Host "FICHIER : $Fichier"
    Write-Host "ATTENDU : $ResultatAttendu"
    Write-Host "============================================================"

    curl.exe -i -X POST $ApiUrl `
      -H "Content-Type: application/json" `
      --data-binary "@$Fichier"
}

Test-Synchronisation `
    -NomTest "1 - Synchronisation valide" `
    -Fichier "sync-valide.json" `
    -ResultatAttendu "SUCCESS"

Test-Synchronisation `
    -NomTest "2 - Doublon avec le même idSynchronisation" `
    -Fichier "sync-doublon.json" `
    -ResultatAttendu "CONFLICT"

Test-Synchronisation `
    -NomTest "3 - Quantité négative" `
    -Fichier "sync-quantite-negative.json" `
    -ResultatAttendu "ERROR ou BAD REQUEST"

Test-Synchronisation `
    -NomTest "4 - NON_FAIT sans commentaire" `
    -Fichier "sync-non-fait-sans-commentaire.json" `
    -ResultatAttendu "ERROR ou BAD REQUEST"

Test-Synchronisation `
    -NomTest "5 - ANOMALIE sans commentaire" `
    -Fichier "sync-anomalie-sans-commentaire.json" `
    -ResultatAttendu "ERROR ou BAD REQUEST"

Test-Synchronisation `
    -NomTest "6 - Ligne validée sans heureValidation" `
    -Fichier "sync-validee-sans-heure.json" `
    -ResultatAttendu "ERROR ou BAD REQUEST"

Test-Synchronisation `
    -NomTest "7 - estValidee false dans l’envoi final" `
    -Fichier "sync-est-validee-false.json" `
    -ResultatAttendu "ERROR ou BAD REQUEST"

```