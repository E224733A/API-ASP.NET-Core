# Documentation API Mobile SLI — contrat JSON v1.2

Date de mise à jour : 2026-05-12  
Projet : Application mobile MobileSLI — dématérialisation de la fiche de tournée  
API : ASP.NET Core  
Contrat technique courant : `schemaVersion = "1.2"`

Cette documentation décrit l’état actuel de l’API après l’adaptation au nouveau cahier des charges, à la nouvelle base mobile et au contrat JSON v1.2.

Le cahier des charges décrit le besoin fonctionnel.  
La configuration API v1.2 est la référence technique exacte du JSON.

---

## 1. Rôle de l’API

L’API ASP.NET Core est le seul point d’entrée technique entre :

- l’application mobile Android des livreurs ;
- les vues ABSSolute en lecture ;
- les tables SQL Server dédiées au projet mobile ;
- la future interface expédition / administration.

Le mobile ne doit jamais accéder directement à SQL Server.

L’API sert à :

```text
- vérifier que le service est disponible ;
- vérifier les connexions SQL ;
- récupérer les livreurs ;
- récupérer les tournées disponibles pour une date ;
- charger une tournée complète ;
- fournir les articles saisissables ;
- intégrer les commentaires exceptionnels ;
- intégrer les pré-remplissages expédition ;
- recevoir la synchronisation finale du mobile ;
- valider les règles métier ;
- bloquer les doubles envois ;
- enregistrer les données dans la base mobile ;
- consulter les synchronisations envoyées.
```

---

## 2. Contrat JSON courant

La version de référence est :

```json
{
  "schemaVersion": "1.2"
}
```

La version 1.2 introduit ou confirme :

```text
- le tableau saisie.quantites[] ;
- quantiteLivreePrevue nullable ;
- quantiteLivree ;
- quantiteRecuperee ;
- la distinction entre null et 0 ;
- commentaireExceptionnel ;
- les informations de tournée et retour conservées dans le snapshot ;
- les dates/heures avec offset ;
- les protections anti-doublons.
```

### Règle importante sur `quantiteLivreePrevue`

```text
quantiteLivreePrevue = null → l’expédition n’a rien renseigné
quantiteLivreePrevue = 0    → l’expédition a volontairement prévu zéro
quantiteLivreePrevue > 0    → l’expédition a prévu une quantité
```

Les champs `null` doivent rester visibles dans le JSON. Dans `Program.cs`, la configuration recommandée est donc :

```csharp
options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
```

---

## 3. Architecture du code

```text
Controllers/
├── HealthController.cs
├── LivreursController.cs
├── TourneesController.cs
├── SynchronisationsController.cs
└── DebugSqlController.cs

Constants/
├── ApiErrorCodes.cs
├── ArticlesSaisissables.cs
├── SchemaVersions.cs
├── StatutsPassage.cs
└── StatutsSynchronisation.cs

Data/
└── SqlConnectionFactory.cs

Mappers/
└── TourneeMobileMapper.cs

Models/
└── DTOs de requête et de réponse

Repositories/
├── LivreursRepository.cs
├── TourneesRepository.cs
└── SynchronisationsRepository.cs

Services/
└── TourneesService.cs

Validators/
└── SynchronisationTourneeValidator.cs
```

### Controllers

Les contrôleurs gèrent la couche HTTP : paramètres, appels aux services/repositories, validation de haut niveau et réponses HTTP.

### Services

`TourneesService` orchestre le chargement des tournées : vérification du livreur, lecture des données, lecture des articles, lecture des commentaires/pré-remplissages et appel du mapper.

### Mappers

`TourneeMobileMapper` construit le JSON de chargement mobile : en-tête, livreur, lignes, infos livreur, articles saisissables et quantités initiales.

### Repositories

Les repositories isolent les accès SQL :

```text
TourneesRepository         → lecture ABSSolute + lecture base mobile pour articles/pré-remplissages/commentaires
SynchronisationsRepository → écriture et consultation dans les tables Mobile_*
LivreursRepository         → lecture des chauffeurs/livreurs
```

### Validators

`SynchronisationTourneeValidator` centralise les règles métier du `POST /api/synchronisations`.

---

## 4. Routes principales

### 4.1 Santé API

```http
GET /api/health
GET /api/health/abssolute
GET /api/health/mobile
```

Commandes :

```powershell
$api = "http://localhost:5120"

curl.exe -i "$api/api/health"
curl.exe -i "$api/api/health/abssolute"
curl.exe -i "$api/api/health/mobile"
```

Résultat attendu : `200 OK`.

---

### 4.2 Liste des livreurs

```http
GET /api/livreurs
```

Commande :

```powershell
curl.exe -i "$api/api/livreurs"
```

Résultat attendu : `200 OK` avec la liste des chauffeurs/livreurs connus.

---

### 4.3 Tournées disponibles

```http
GET /api/tournees/disponibles?dateTournee=2026-05-07&codeLivreur=2
```

Commande :

```powershell
curl.exe -i "$api/api/tournees/disponibles?dateTournee=2026-05-07&codeLivreur=2"
```

Réponse attendue :

```json
{
  "schemaVersion": "1.2",
  "dateTournee": "2026-05-07",
  "dateModifiable": false,
  "livreur": {
    "codeLivreur": "2",
    "nomLivreur": "DAVID LEBAS"
  },
  "tournees": [
    {
      "codeTournee": "4006",
      "libelleTournee": "BOUAYE",
      "nombrePoints": 16
    }
  ]
}
```

---

### 4.4 Chargement complet d’une tournée

```http
GET /api/tournees/jour?dateTournee=2026-05-07&codeTournee=4006&codeLivreur=2
```

Commande :

```powershell
curl.exe -i "$api/api/tournees/jour?dateTournee=2026-05-07&codeTournee=4006&codeLivreur=2"
```

Réponse attendue :

```text
schemaVersion = 1.2
dateModifiable = false
jourTournee = 4
jourLibelle = Jeudi
codeTournee = 4006
libelleTournee = BOUAYE
statutSynchronisation = NON_ENVOYEE
livreur.codeLivreur = 2
livreur.nomLivreur = DAVID LEBAS
articlesSaisissables = ROLLS, TAPIS, SACS
lignes[] présent
saisie.quantites[] présent dans chaque ligne
quantiteLivreePrevue visible, même quand la valeur vaut null
```

---

### 4.5 Synchronisation finale

```http
POST /api/synchronisations
```

Commande :

```powershell
curl.exe -i -X POST "$api/api/synchronisations" `
  -H "Content-Type: application/json" `
  --data-binary "@sync-valide.json"
```

Réponse succès :

```json
{
  "statut": "SUCCESS",
  "message": "Synchronisation enregistrée avec succès."
}
```

---

### 4.6 Consultation des synchronisations

```http
GET /api/synchronisations
GET /api/synchronisations?dateTournee=2026-05-07&codeTournee=4006&codeLivreur=2
GET /api/synchronisations/{idTourneeMobile}
```

Commandes :

```powershell
curl.exe -i "$api/api/synchronisations"
curl.exe -i "$api/api/synchronisations?dateTournee=2026-05-07&codeTournee=4006&codeLivreur=2"
curl.exe -i "$api/api/synchronisations/1"
```

---

## 5. Règles métier validées côté API

L’API refuse :

```text
- une requête sans schemaVersion ;
- une schemaVersion non supportée ;
- une requête sans idSynchronisation ;
- un idSynchronisation invalide ;
- une requête sans dateTournee ;
- une dateTournee invalide ;
- une requête sans codeTournee ;
- une requête sans livreur.codeLivreur ;
- une requête sans mobile ;
- une requête sans lignes[] ;
- une ligne sans idLigneSource ;
- deux lignes avec le même idLigneSource dans la même requête ;
- une ligne sans client ;
- une ligne sans pointLivraison ;
- une ligne sans saisie ;
- une ligne sans statutPassage ;
- le statut A_FAIRE dans l’envoi final ;
- estValidee = false dans l’envoi final ;
- estValidee = true sans heureValidation ;
- NON_FAIT sans commentaireLivreur ;
- ANOMALIE sans commentaireLivreur ;
- un tableau quantites[] vide ;
- deux fois le même codeArticle dans une ligne ;
- quantiteLivree négative ;
- quantiteRecuperee négative ;
- quantiteLivreePrevue négative ;
- un double envoi avec le même idSynchronisation ;
- un double envoi métier pour la même dateTournee + codeTournee + livreur.
```

---

## 6. Codes de réponse principaux

### Succès

```http
HTTP/1.1 200 OK
```

```json
{
  "statut": "SUCCESS",
  "message": "Synchronisation enregistrée avec succès."
}
```

### Erreur de validation

```http
HTTP/1.1 400 Bad Request
```

```json
{
  "statut": "VALIDATION_ERROR",
  "errors": [
    "Ligne 1 : HeureValidation est obligatoire."
  ]
}
```

### Doublon technique

```http
HTTP/1.1 409 Conflict
```

```json
{
  "statut": "CONFLICT",
  "code": "SYNCHRONISATION_ALREADY_EXISTS",
  "message": "Cette synchronisation a déjà été reçue."
}
```

### Doublon métier

```http
HTTP/1.1 409 Conflict
```

```json
{
  "statut": "CONFLICT",
  "code": "TOURNEE_ALREADY_SENT",
  "message": "Cette tournée a déjà été envoyée pour ce livreur et cette date."
}
```

### Non trouvé

```http
HTTP/1.1 404 Not Found
```

### Erreur technique

```http
HTTP/1.1 500 Internal Server Error
```

```json
{
  "statut": "ERROR",
  "code": "TECHNICAL_ERROR",
  "message": "Erreur technique lors de la synchronisation."
}
```

---

## 7. Tables SQL concernées

### Lecture métier ABSSolute

```text
v_chauffeurs
v_tournee
v_pdl_jour
v_fermeture
v_clients
v_jour_client
v_route_number
v_liste_article
v_liste_produit_abssolute
```

### Tables mobiles

```text
Mobile_ArticleSaisissable
Mobile_ChargementTournee
Mobile_CommentaireExceptionnel
Mobile_ExportAdmin
Mobile_Livreur
Mobile_LogSynchronisation
Mobile_PreRemplissageHistorique
Mobile_PreRemplissageQuantite
Mobile_PreRemplissageTournee
Mobile_Tournee
Mobile_TourneeLigne
Mobile_TourneeLigneQuantite
Mobile_UtilisateurExpedition
```

---

## 8. Routes debug SQL

Ces routes sont utiles en développement mais doivent être désactivées ou protégées avant une mise en production réelle.

```http
GET /api/debug/sql/tables-mobile
GET /api/debug/sql/vues-abssolute
GET /api/debug/sql/chauffeurs
GET /api/debug/sql/tournees
GET /api/debug/sql/clients
GET /api/debug/sql/pdl-jour
GET /api/debug/sql/fermetures
GET /api/debug/sql/jour-client
GET /api/debug/sql/route-number
GET /api/debug/sql/articles
GET /api/debug/sql/produits-abssolute
```

Commandes :

```powershell
curl.exe -i "$api/api/debug/sql/tables-mobile"
curl.exe -i "$api/api/debug/sql/vues-abssolute"
curl.exe -i "$api/api/debug/sql/chauffeurs"
curl.exe -i "$api/api/debug/sql/tournees"
curl.exe -i "$api/api/debug/sql/clients"
curl.exe -i "$api/api/debug/sql/pdl-jour"
curl.exe -i "$api/api/debug/sql/fermetures"
curl.exe -i "$api/api/debug/sql/jour-client"
curl.exe -i "$api/api/debug/sql/route-number"
curl.exe -i "$api/api/debug/sql/articles"
curl.exe -i "$api/api/debug/sql/produits-abssolute"
```

---

## 9. État validé au 2026-05-12

Les tests suivants ont été validés :

```text
01 - Synchronisation valide                         → 200 OK / SUCCESS
02 - Doublon technique idSynchronisation           → 409 Conflict / SYNCHRONISATION_ALREADY_EXISTS
03 - Double envoi métier date + tournée + livreur  → 409 Conflict / TOURNEE_ALREADY_SENT
04 - Quantité négative                             → 400 Bad Request / VALIDATION_ERROR
05 - NON_FAIT sans commentaire                     → 400 Bad Request / VALIDATION_ERROR
06 - ANOMALIE sans commentaire                     → 400 Bad Request / VALIDATION_ERROR
07 - Ligne validée sans heureValidation            → 400 Bad Request / VALIDATION_ERROR
08 - estValidee false dans envoi final             → 400 Bad Request / VALIDATION_ERROR
09 - A_FAIRE dans envoi final                      → 400 Bad Request / VALIDATION_ERROR
10 - idLigneSource dupliqué                        → 400 Bad Request / VALIDATION_ERROR
11 - codeArticle dupliqué                          → 400 Bad Request / VALIDATION_ERROR
12 - schemaVersion non supportée                   → 400 Bad Request / VALIDATION_ERROR
13 - quantites vide                                → 400 Bad Request / VALIDATION_ERROR
14 - quantiteLivreePrevue négative                 → 400 Bad Request / VALIDATION_ERROR
```
