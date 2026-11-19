# Checklist de validation sur installation propre

À faire sur une machine ou une VM Windows 10/11 sans SDK .NET. Cocher seulement ce qui a été observé.

## Installation
- [ ] `NexusConsole-Setup-x.y.z.exe` s'installe sans demande de droits administrateur
- [ ] Raccourci du menu Démarrer créé ; l'icône personnalisée s'affiche
- [ ] L'application démarre sans installer quoi que ce soit d'autre (version autonome)
- [ ] Désinstallation depuis « Applications installées » : fichiers programme retirés, clé de démarrage automatique retirée
- [ ] Les données `%LOCALAPPDATA%\NexusConsole` sont conservées (comportement voulu) ; suppression manuelle possible

## Premier démarrage
- [ ] L'assistant (6 écrans) s'affiche ; il se relance depuis Paramètres → Système et performances
- [ ] Manette Xbox détectée ; assistant de boutons : chaque action se configure, les doublons sont refusés
- [ ] Détection : Steam, Epic, GOG, Ubisoft listent les jeux réellement installés (noter les écarts)

## Jeux
- [ ] Lancer un jeu Steam, un Epic ou GOG, un ajout manuel : NEXUS se réduit, revient à la fermeture, durée enregistrée
- [ ] Annuler un lancement (Échap / bouton Retour) pendant « Lancement… »
- [ ] Jeu qui ne démarre pas : message après 120 s, pas de blocage
- [ ] Jeu qui plante : retour à NEXUS
- [ ] Jeu en plein écran exclusif : NEXUS revient au premier plan

## Cloud, boutique, navigateur
- [ ] GeForce NOW détecté ; lancement puis retour à NEXUS à la fermeture (tester les deux modes de suivi si besoin)
- [ ] Xbox Cloud Gaming / Luna : fenêtre Edge dédiée, connexion conservée, retour à la fermeture
- [ ] Boutique : catégories chargées, fiche d'un jeu, captures ; sans Internet : message clair, pas de plantage
- [ ] « Acheter ou télécharger » : page officielle dans le navigateur intégré ; connexion possible ; repli vers le navigateur Windows si échec
- [ ] Navigation à la manette dans le navigateur (défilement, éléments, clic, retour)

## Réglages
- [ ] Profils : création, changement (thème et sons propres à chaque profil), suppression, dernier profil protégé
- [ ] Démarrer avec Windows, mode économie de mémoire (redémarrer pour les 30 images/s), vider le cache, sauvegarde
- [ ] Mode TV et tailles d'écran 1080p / 1440p / 4K

## Mesures (à joindre au retour)
- [ ] `tools\measure.ps1` : mesures.csv
- [ ] Onglet Performance : benchmark.csv (48 et 192 cartes)
