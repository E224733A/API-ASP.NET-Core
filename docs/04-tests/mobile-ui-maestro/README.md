# Priorité 3 - Tests UI mobile avec Maestro

Ce dossier contient un jeu de tests UI automatisés pour l'application mobile **Mobile SLI**.

Objectif : compléter les tests API et les tests de masse par quelques tests bout en bout sur téléphone Android, sans chercher à automatiser toute l'application.

## Emplacement conseillé

Copier ce dossier dans :

```text
API-ASP.NET-Core\docs\04-tests\mobile-ui-maestro
```

Ce dossier reste documentaire et outillage de recette. Il ne modifie pas le code de l'API.

## Périmètre couvert

Trois scénarios bonus sont fournis :

| ID | Scénario | But |
|---|---|---|
| MUI-001 | Reprise d'une tournée non synchronisée | Vérifier que le livreur comprend qu'une tournée locale existe et que le bouton de reprise est bien utilisable. |
| MUI-002 | Saisie `ROLLS_VIDES` | Vérifier que le champ **Livré** des chariots vides est saisissable et qu'il n'affiche plus de tiret trompeur. |
| MUI-003 | Validation d'un point | Vérifier qu'un point peut être ouvert, renseigné et validé. |

## Deux variantes de flows

Le dossier contient deux variantes :

```text
flows-texte
flows-stables-automationid
```

### `flows-texte`

Flows utilisables directement avec les textes visibles de l'interface.

Ils sont pratiques pour démarrer rapidement, mais ils sont moins robustes si les textes changent ou si plusieurs boutons ont le même libellé.

### `flows-stables-automationid`

Flows recommandés à long terme.

Ils supposent que des `AutomationId` sont ajoutés dans les pages MAUI. Les identifiants recommandés sont listés dans :

```text
docs/automationid-recommandes.md
```

Cette variante est celle à valoriser dans le rapport ou la soutenance, car elle montre une démarche de test UI fiable.

## Application testée

L'identifiant Android actuel de l'application est :

```text
fr.sli.mobiletournee
```

## Préparation minimale

1. Installer l'application Mobile SLI sur le téléphone ou l'émulateur.
2. Vérifier que le téléphone est visible avec ADB :

```powershell
adb devices
```

3. Vérifier que Maestro est installé :

```powershell
maestro --version
```

4. Préparer les données selon le scénario :
   - une tournée locale non synchronisée pour MUI-001 ;
   - une tournée contenant `ROLLS_VIDES` pour MUI-002 ;
   - au moins un point à faire pour MUI-003.

Les préconditions détaillées sont dans :

```text
docs/preconditions-tests.md
```

## Exécution rapide

Depuis ce dossier :

```powershell
.\scripts\run-maestro-mobile-ui-tests.ps1
```

Pour lancer la variante stable avec `AutomationId` :

```powershell
.\scripts\run-maestro-mobile-ui-tests.ps1 -FlowProfile automationid
```

Pour lancer uniquement un scénario :

```powershell
maestro test .\flows-texte\02-saisie-rolls-vides.yaml
```

## Résultats

Les sorties sont écrites dans :

```text
reports
```

Le modèle de rapport à compléter est disponible dans :

```text
docs/rapport-execution-mobile-ui-modele.md
```

## Remarque importante

Ces tests ne remplacent pas une recette manuelle sur vrai téléphone. Ils servent à automatiser les parcours critiques qui risquent de régresser après une correction d'interface.
