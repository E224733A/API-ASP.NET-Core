# Test POST /api/synchronisations validé

Date du test : 2026-04-30

Commande utilisée :

```powershell
curl.exe -X POST "http://localhost:5120/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync.json"

  {"statut":"SUCCESS","message":"Synchronisation enregistrée avec succès."}

  