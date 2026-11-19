# NEXUS CONSOLE

Application Windows (WPF, .NET 8) qui transforme un PC en console de salon : bibliothèque de jeux détectée automatiquement,
navigation à la manette, lancement avec retour automatique à l'interface, cloud gaming, boutique avec achat sur les sites officiels.

> **État honnête : tout le code est écrit mais n'a jamais été compilé ni exécuté** (il a été produit dans un environnement sans Windows
> ni SDK .NET). Voir `documentation/ETAT_AVANCEMENT.md` pour ce qui est fait, partiel ou absent, et `documentation/CHECKLIST_INSTALLATION.md`
> pour valider.

## Démarrage rapide (développeur)
1. Installer le SDK .NET 8 ou plus récent.
2. `powershell -ExecutionPolicy Bypass -File setup.ps1` : crée la solution, compile, lance les tests.
3. `dotnet run --project src/Nexus.App -c Release`

## Mesurer (aucun chiffre n'est annoncé sans mesure)
- `tools\measure.ps1` : temps de démarrage, RAM et CPU au repos de l'application compilée.
- Onglet **Performance** dans NEXUS : FPS, RAM, CPU sous animation ; export `benchmark.csv`.

## Construire l'installateur
`powershell -ExecutionPolicy Bypass -File installer\build.ps1` (nécessite Inno Setup 6.3+). Installation par utilisateur, sans droits administrateur.

## Données et journaux
`%LOCALAPPDATA%\NexusConsole` : `nexus.db` (jeux, profils, réglages), `logs\`, `cache\`, `webview2\`, `edge-profile\`.
