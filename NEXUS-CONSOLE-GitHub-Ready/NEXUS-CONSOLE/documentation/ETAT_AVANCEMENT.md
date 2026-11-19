# NEXUS CONSOLE — État d'avancement final (Phases 1 à 12)

## Ce qu'il faut savoir avant tout
- **Rien n'a été compilé ni exécuté.** L'environnement de production n'avait ni Windows ni SDK .NET. Vérifié seulement : XML des .xaml / .csproj bien formé,
  accolades C# équilibrées. Avec environ 70 fichiers C#, des erreurs de compilation sont probables.
- **Aucune mesure de performance n'existe.** Aucun chiffre de RAM, CPU ou démarrage n'est annoncé : seuls les outils pour les mesurer sont livrés.
- **Aucun test n'a été exécuté.** 30 tests sont écrits (tests/Nexus.Data.Tests et tests/Nexus.Logic.Tests).
- **Phase 11 (installateur) : scripts écrits, installateur jamais construit ni testé sur installation propre. Phase 12 (vérification globale) : impossible sans exécution** ;
  elle est remplacée par la liste de contrôle `CHECKLIST_INSTALLATION.md` à dérouler par toi.

Légende : **Écrit** = code présent, non vérifié · **Partiel** = écrit mais une partie du cahier des charges manque · **Absent**.

## Phase par phase

| Phase | Contenu | État |
|---|---|---|
| 1 Conception | Architecture, choix techniques, plan | Fait (voir ARCHITECTURE.md) |
| 2 Base | Fenêtre, navigation, paramètres, SQLite, journaux, erreurs | Écrit |
| 3 Interface | Accueil, bibliothèque, cartes, transitions, sons, ambiance, échelle auto | Écrit, partiel (voir limites) |
| 4 Manettes | XInput, assistant, profils, glyphes, test | Écrit, **partiel** (pas de DualSense/DS4/DirectInput natifs) |
| 5 Détection | Steam, Epic, GOG, Ubisoft | Écrit, **partiel** (4 plateformes sur 8) |
| 6 Lancement | Suivi de session, retour automatique | Écrit |
| 7 Cloud | GeForce NOW, Xbox Cloud Gaming, Luna, GFN web | Écrit |
| 8 Boutique | Catalogue Steam, fiches, navigateur intégré | Écrit, **partiel** (une seule boutique) |
| 9 Avancé | Profils, assistant de premier démarrage, TV, économie, démarrage Windows | Écrit, partiel |
| 10 Optimisation | Mode économie, cache borné, lectures conditionnelles, outils de mesure | Écrit ; **mesures non faites** |
| 11 Packaging | Icône, script de publication, script Inno Setup | Écrit ; **non construit, non testé** |
| 12 Finalisation | Documentation, checklist | Écrit ; **vérification globale non faite** |

## Détail des fonctionnalités par phase

### Phases 2–3 (base et interface)
Barre supérieure (logo, profil, heure, date, réseau, manette), 8 sections, navigation par actions abstraites avec bruitages, fondu et glissement entre pages,
paramètres persistants par profil (thème, accent, animations, mode TV, taille de l'interface, ambiance), bibliothèque (grille / horizontale, filtres, tris, favoris, masquage,
retrait sans désinstallation, ajout manuel avec icône Windows), fiche d'un jeu, accueil (jeu à reprendre + récents), huit sons synthétisés réglables un par un, aucune musique,
échelle automatique 0,8× à 2×, page Performance (FPS, RAM, CPU, export CSV).
**Limites :** vue détaillée absente ; parallaxe absente ; renommer / changer l'image ou le chemin d'un jeu absents (pas de saisie de texte à la manette) ;
listes non virtualisées ; pas de verre dépoli réel ; arrière-plans personnalisables absents ; les erreurs inattendues s'affichent dans une boîte Windows classique.

### Phase 4 (manettes)
XInput : détection, connexion / déconnexion, 4 manettes, manette principale = la dernière utilisée, navigation complète, assistant de boutons (détection du bouton pressé, refus des doublons),
zone morte radiale réglable, inversion d'axe vertical, test en direct, indications à l'écran, profil enregistré par profil utilisateur.
**Absent :** DualSense, DualShock 4, DirectInput natifs (prévu via SDL3, non fait faute de pouvoir tester une bibliothèque native ; ces manettes fonctionnent via Steam Input ou DS4Windows),
profil séparé par manette, sensibilité des sticks, saisie de texte. Le bouton Guide dépend d'une fonction XInput non documentée.

### Phase 5 (détection)
Steam, Epic Games, GOG, Ubisoft Connect ; analyse en arrière-plan (démarrage, intervalle réglable, manuelle, après session) ; pas de doublons ; favoris conservés ; « non installé » au lieu de suppression ; une plateforme en échec n'efface rien.
**Absent :** EA App, Battle.net, Riot, Xbox / Microsoft Store ; téléchargement de pochettes (seul le cache local de Steam est utilisé) ; surveillance en temps réel des dossiers ;
doublons entre plateformes. Formats non officiels (localconfig Steam, noms Ubisoft) : « au mieux ».

### Phase 6 (lancement)
URI de plateforme ou exécutable ; suivi par dossier d'installation et RunningAppID Steam ; délais de grâce ; délai maximal ; un seul lancement à la fois ; annulation ; fenêtre réduite sans toucher au jeu ;
manettes suspendues ; restauration avec premier plan forcé ; durée enregistrée ; messages clairs.
**Limites :** exécutable hors dossier d'installation (hors Steam) non reconnu ; plantage non distinguable d'une fermeture (avertissement si session très courte) ; focus refusé par Windows dans certains cas.

### Phase 7 (cloud)
GeForce NOW : détection (chemins connus et raccourcis du menu Démarrer), lancement, retour à NEXUS, deux modes de suivi réglables (processus lancé / dossier) car le comportement réel du client n'a pas pu être observé.
Xbox Cloud Gaming, Amazon Luna, GeForce NOW web : fenêtre Edge « application » avec profil dédié (connexion conservée, processus à part donc suivable) ; repli sur le navigateur par défaut (non suivi).
Applications : applications natives détectées (GeForce NOW).
**Limites :** aucune information interne des services ; Luna dépend de la disponibilité dans le pays ; « Applications » ne liste que GeForce NOW ; pas d'ajout d'applications personnalisées.

### Phase 8 (boutique)
Catalogue public Steam (promotions, meilleures ventes, nouveautés, prochainement), cache disque 30 min avec repli hors ligne, cartes avec visuel, prix, réduction ; fiche (visuel, description, genres, développeur, éditeur, captures, prix) ;
« Acheter ou télécharger » ouvre la page officielle dans le navigateur intégré (WebView2, sessions conservées, navigation à la manette, repli vers le navigateur Windows). Aucun paiement propriétaire, aucune authentification contournée.
**Absent :** Epic Games Store, GOG, Microsoft Store (pas d'API publique stable utilisable), jeux gratuits en catégorie dédiée, recherche textuelle. Les points d'accès Steam ne sont pas une API officielle documentée.
**Limite du navigateur :** pendant que la page web a le focus, le clavier est capté par le moteur (Échap ne ferme pas) ; la manette et la barre d'outils à la souris restent utilisables. Certains sites de connexion peuvent refuser un navigateur intégré : utiliser le navigateur Windows.

### Phase 9 (paramètres avancés)
Profils locaux (nom automatique, réglages séparés, changement, suppression, dernier profil protégé), assistant de premier démarrage en 6 écrans (relançable), mode TV (×1,25), taille d'interface, démarrage avec Windows (clé utilisateur, sans admin),
actualisation automatique réglable, sauvegarde de la base, thème clair.
**Limites :** jeux, favoris et récents communs à tous les profils (non séparés) ; pas de renommage ni d'avatar personnalisable (initiales et couleur automatiques) ; pas de choix de résolution autre que l'échelle ; pas d'arrière-plans personnalisés.

### Phase 10 (optimisation)
Écrit : mode économie (cache d'images 32 au lieu de 96, animations à 30 images/s au prochain lancement), images décodées à la taille utile et chargées à la demande, navigateur créé et détruit à la demande,
lecture des manettes à 60 Hz seulement si nécessaire et arrêtée pendant un jeu, une seule minuterie d'horloge par minute, détection hors du thread d'interface, désabonnement des événements à la fermeture des pages, aucun processus permanent ni service.
**Non fait :** toute mesure (RAM au repos, en navigation, CPU, démarrage, chargement de bibliothèque), analyse mémoire, recherche fine fuites, tests de stabilité. `tools\measure.ps1` et la page Performance servent à les produire.
Aucun objectif chiffré n'est revendiqué.

### Phase 11 (packaging)
Icône (assets\nexus.ico), version 0.12.0, `installer\build.ps1` (publication autonome ReadyToRun ou dépendante du runtime, puis Inno Setup), `installer\NexusConsole.iss` : installation par utilisateur sans droits administrateur,
raccourcis, désinstallation propre (les données restent dans %LOCALAPPDATA%\NexusConsole), retrait du démarrage automatique. Mises à jour : non implémentées (la version est exposée ; une solution de type Velopack pourra s'y brancher).

## Dernière chose : ordre conseillé pour toi
1. `setup.ps1` : me renvoyer toutes les erreurs de compilation et d'échec de test, telles quelles.
2. Dérouler la checklist ; me dire ce qui est faux (surtout détection Epic / GOG / Ubisoft, suivi GeForce NOW, retour au premier plan).
3. `tools\measure.ps1` et la page Performance ; me renvoyer les CSV : l'optimisation et la décision WPF / WinUI 3 se feront sur ces chiffres.
4. Ensuite seulement : EA App, Battle.net, Riot, Xbox ; backend DualSense ; mises à jour automatiques.
