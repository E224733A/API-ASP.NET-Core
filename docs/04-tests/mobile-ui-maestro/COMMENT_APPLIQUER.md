# Comment appliquer le dossier de tests UI Maestro

## 1. Copier le dossier

Décompresser le zip, puis copier le dossier :

```text
mobile-ui-maestro
```

dans :

```text
API-ASP.NET-Core\docs\04-tests\mobile-ui-maestro
```

## 2. Vérifier les outils

Depuis PowerShell :

```powershell
adb devices
maestro --version
```

Si `adb devices` ne voit aucun téléphone, vérifier :

```text
- mode développeur Android activé ;
- débogage USB activé ;
- autorisation RSA acceptée sur le téléphone ;
- téléphone ou émulateur démarré.
```

## 3. Préparer les données

Les tests UI ne créent pas eux-mêmes les données API ou SQLite.

Avant de lancer les tests, préparer l'application dans l'état attendu :

```text
MUI-001 : une tournée locale non synchronisée existe déjà.
MUI-002 : la tournée chargée contient un article ROLLS_VIDES.
MUI-003 : un point non validé est visible dans la liste.
```

## 4. Lancer les tests texte

```powershell
cd API-ASP.NET-Core\docs\04-tests\mobile-ui-maestro
.\scripts\run-maestro-mobile-ui-tests.ps1 -FlowProfile texte
```

## 5. Lancer les tests stables avec AutomationId

Après ajout des `AutomationId` recommandés côté application mobile :

```powershell
.\scripts\run-maestro-mobile-ui-tests.ps1 -FlowProfile automationid
```

## 6. Compléter le rapport

Après exécution, compléter :

```text
docs/rapport-execution-mobile-ui-modele.md
```

et ajouter les traces générées dans :

```text
reports
```

## 7. Critère d'acceptation

Les tests sont considérés exploitables si :

```text
- chaque flow démarre depuis un état clairement défini ;
- les textes ou AutomationId ciblés sont stables ;
- une trace d'exécution est conservée ;
- les échecs sont analysés comme erreur de test, erreur d'environnement ou régression applicative.
```
