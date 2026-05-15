$api = "http://127.0.0.1:5000"
$ApiUrl = "$api/api/synchronisations"

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

Test-Synchronisation -NomTest "01 - Synchronisation valide" -Fichier "sync-valide.json" -ResultatAttendu "200 OK / SUCCESS"
Test-Synchronisation -NomTest "02 - Doublon technique idSynchronisation" -Fichier "sync-doublon.json" -ResultatAttendu "409 Conflict / SYNCHRONISATION_ALREADY_EXISTS"
Test-Synchronisation -NomTest "03 - Double envoi métier même date + tournée + livreur" -Fichier "sync-double-envoi-tournee.json" -ResultatAttendu "409 Conflict / TOURNEE_ALREADY_SENT"
Test-Synchronisation -NomTest "04 - Quantité négative" -Fichier "sync-quantite-negative.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "05 - NON_FAIT sans commentaire" -Fichier "sync-non-fait-sans-commentaire.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "06 - ANOMALIE sans commentaire" -Fichier "sync-anomalie-sans-commentaire.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "07 - Ligne validée sans heureValidation" -Fichier "sync-validee-sans-heure.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "08 - estValidee false dans envoi final" -Fichier "sync-est-validee-false.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "09 - A_FAIRE dans envoi final" -Fichier "sync-a-faire.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "10 - idLigneSource dupliqué" -Fichier "sync-idligne-duplique.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "11 - codeArticle dupliqué" -Fichier "sync-code-article-duplique.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "12 - schemaVersion non supportée" -Fichier "sync-schema-version-invalide.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "13 - quantites vide" -Fichier "sync-quantites-vide.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
Test-Synchronisation -NomTest "14 - quantiteLivreePrevue négative" -Fichier "sync-quantite-prevue-negative.json" -ResultatAttendu "400 Bad Request / VALIDATION_ERROR"
