# Architecture

```
Nexus.App (WPF)  ──►  Nexus.Detection ─┐
   │                  Nexus.Launching  ├─►  Nexus.Core  (modèles, paramètres, journal, NavAction)
   │                  Nexus.Input      │
   │                  Nexus.Cloud      │
   │                  Nexus.Store      ┘
   └──────────────►  Nexus.Data (SQLite)  ──►  Nexus.Core
```
Dépendances dans un seul sens. Seul Nexus.App dépend de WPF ; tout le reste est testable sans interface.

| Projet | Rôle |
|---|---|
| Nexus.Core | Modèles (Game, Profile, DetectedGame), AppSettings (clé/valeur), journal fichier, NavAction |
| Nexus.Data | SQLite : migrations numérotées (4), sauvegarde avant migration, récupération d'une base corrompue ; dépôts jeux, profils, réglages |
| Nexus.Input | Manettes XInput : détection, 60 Hz seulement si utile, profils, zone morte, symboles |
| Nexus.Detection | Une source par plateforme (Steam, Epic, GOG, Ubisoft) + rapprochement avec la base |
| Nexus.Launching | Lancement (URI ou exécutable), suivi de session, premier plan Windows |
| Nexus.Cloud | Détection GeForce NOW, services web, entrées de lancement temporaires |
| Nexus.Store | Catalogue Steam public, cache disque, lecture tolérante du JSON |
| Nexus.App | Fenêtre, navigation, pages, sons synthétisés, thème, navigateur WebView2, assistant |

## Flux principaux
- **Entrées** : clavier et manette produisent des `NavAction` → `MainWindow.Perform` → page courante (`HandleAction`) ou déplacement de focus.
- **Détection** : `GameScanner` (arrière-plan) → sources → rapprochement (sans doublon, favoris conservés, « non installé » plutôt que suppression).
- **Lancement** : `LaunchAsync` → écran d'attente annulable → jeu détecté → fenêtre réduite, manettes suspendues → fermeture → restauration, session enregistrée.
- **Réglages** : `SettingsService` (par profil) → base → thème, sons, échelle, mémoire.

## Choix assumés
- WPF plutôt que WinUI 3 : maturité et risque minimal ; la décision définitive dépend des mesures sur la machine cible (page Performance).
- Pas de flou WPF (coût CPU) : aplats et dégradés.
- Sons synthétisés en mémoire : zéro fichier, zéro dépendance.
- Pas d'ORM, pas de framework MVVM : socle minimal.
