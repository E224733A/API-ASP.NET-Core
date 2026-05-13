$ApiBaseUrl = "http://127.0.0.1:5000"

function Invoke-TestPost {
    param(
        [string]$Title,
        [string]$File,
        [string]$Expected
    )

    Write-Host "============================================================"
    Write-Host "TEST : $Title"
    Write-Host "FICHIER : $File"
    Write-Host "ATTENDU : $Expected"
    Write-Host "============================================================"

    curl.exe -i -X POST "$ApiBaseUrl/api/synchronisations" `
        -H "Content-Type: application/json" `
        --data-binary "@$File"
}

Invoke-TestPost "15 - Synchronisation valide avec ROLLS_VIDES" "sync-valide-rolls-vides.json" "200 OK / SUCCESS"
Invoke-TestPost "16 - ROLLS_VIDES avec quantiteLivree > 0" "sync-rolls-vides-livree-invalide.json" "400 Bad Request / VALIDATION_ERROR"
Invoke-TestPost "17 - ROLLS_VIDES avec quantiteLivreePrevue > 0" "sync-rolls-vides-prevue-invalide.json" "400 Bad Request / VALIDATION_ERROR"
