# SnakeGame

**Langues :** [English](README.md) | [简体中文](README.zh-CN.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Русский](README.ru.md)

**SnakeGame** est une collection moderne de jeux de serpent avec cinq modes de serpent et un mode tower defense 3D complet inspiré d'Arknights, développé avec **Unity / Tuanjie 2022.3**. Le même code source est publié pour **Windows (x64 / x86)**, **Android (arm64-v8a)** et le web (WebGL).

[![Version](https://img.shields.io/badge/version-9.1.0--beta-00b7c3?style=flat-square)](https://github.com/MEIMAOA/SnakeGame/releases)
[![Plateformes](https://img.shields.io/badge/platforms-Windows%20%7C%20Android%20%7C%20WebGL-0078D4?style=flat-square)](#plateformes-et-téléchargements)
[![Moteur](https://img.shields.io/badge/engine-Unity%20%2F%20Tuanjie%202022.3-000000?style=flat-square)](https://unity.com/)
[![Licence](https://img.shields.io/badge/license-MIT-00cc6a?style=flat-square)](LICENSE)

---

## Présentation

- Cinq modes de serpent indépendants et un mode tower defense 3D complet dans une seule application.
- Interface entièrement dessinée en IMGUI, sans framework UI externe, dans un style inspiré de Windows 11.
- Localisation complète : chinois simplifié, anglais et allemand, modifiables à l'exécution.
- Entrées bureau et mobile : souris et clavier sous Windows, joystick virtuel et boutons tactiles sous Android.
- Réglages d'affichage et de performance : résolution, mode d'affichage, limite de fps, anticrénelage, synchronisation verticale.
- Succès, statistiques et classements enregistrés localement.
- La même arborescence produit les clients Windows, Android et WebGL.

## Modes de jeu

| Mode | Description |
|---|---|
| Classique | Manger pour grandir et atteindre 3000 points sans toucher un mur ni soi-même |
| Battle Royale (Timed) | 49 serpents IA, F défie un adversaire proche en duel |
| Équipe 4v4 | Coopérer avec les serpents IA bleus pour capturer la zone centrale |
| Extrême | 20 ennemis, survivre trois minutes pendant que le multiplicateur augmente |
| Extraction | Fouiller des conteneurs, récupérer du butin et atteindre le point d'extraction vert |
| Tower Defense (3D) | Déploiement, blocage, compétences, coût et dix niveaux inspirés d'Arknights |

## Plateformes et téléchargements

| Plateforme | Paquet | Remarque |
|---|---|---|
| Windows x64 | `SnakeGame-9.1.0-beta-Windows64.zip` | Décompresser puis lancer `SnakeGame.exe` |
| Windows x86 | `SnakeGame-9.1.0-beta-Windows32.zip` | Pour Windows 32 bits |
| Android | `SnakeGame-9.1.0-beta-Android.apk` | arm64-v8a, Android 8.0 (API 26) ou supérieur |
| WebGL | `SnakeGame-9.1.0-beta-WebGL.zip` | Paquet statique de la version navigateur |

Téléchargement : <https://github.com/MEIMAOA/SnakeGame/releases/tag/v9.1.0-beta>

## Jouer en ligne (WebGL)

Le vrai jeu (même code que la version bureau) s'exécute dans le navigateur :

- Démo en ligne : <https://meimaoa.github.io/snakegame-web/play/index.html>
- Site du projet : <https://meimaoa.github.io/snakegame-web/>

Le premier chargement prend environ 15 à 20 secondes, le fichier WebGL faisant environ 18 Mo. Un navigateur de bureau avec souris et clavier est recommandé.

## Commandes

| Plateforme | Direction | Accélération | Pause | Autres |
|---|---|---|---|---|
| Windows | Visée souris ou ZQSD / flèches | Ctrl ou Maj | P ou Échap | Touche F pour les actions, Espace pour le rythme, R pour recommencer |
| Android | Glisser n'importe où (joystick virtuel) | Maintenir le bouton d'accélération en bas à droite | Bouton pause en haut à droite | Bouton d'action au-dessus du bouton d'accélération |
| WebGL | Visée souris ou ZQSD / flèches | Ctrl ou Maj | P ou Échap | Identique à Windows |

## Technologies

- C# avec Unity / Tuanjie 2022.3 LTS.
- Tous les écrans, panneaux et dialogues sont dessinés via IMGUI.
- Builds Windows Standalone x64 et x86, Android ARM64 avec IL2CPP, build WebGL.
- Police Noto Sans SC intégrée (SIL Open Font License) pour un rendu correct du chinois sur le web.
- GitHub Actions pour l'automatisation des versions.

## Compiler depuis les sources

Prérequis : Tuanjie 2022.3.62t16 avec les modules de support Windows, Android et WebGL.

```powershell
$editor = "C:\Path\To\Tuanjie\Editor\Tuanjie.exe"
$project = "C:\Path\To\SnakeGame"

# Windows x64 / x86
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildWindows
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildWindows32

# Android (APK)
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildAndroid

# WebGL
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildWebGL

# Vérification de la chaîne de démarrage (181 contrôles)
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.VerifyLaunch.Run
```

Les sorties sont écrites dans `Build/Windows64`, `Build/Windows32`, `Build/Android` et `Build/WebGL`.

## Structure du dépôt

| Chemin | Description |
|---|---|
| `Assets/Scripts/Core` | Réglages, localisation, progression et données partagées |
| `Assets/Scripts/View` | Interface, menus, HUD, réglages d'affichage et support des plateformes |
| `Assets/Scripts/Snake` | Simulation du serpent pour les cinq modes |
| `Assets/Scripts/Battle` | Simulation du tower defense |
| `Assets/Scripts/Editor` | Outils de build, de vérification et de prévisualisation |
| `Assets/StreamingAssets/Saukiya-Arknights` | Prototype open source intégré, non compilé |
| `External/Saukiya-Arknights` | Copie source du même projet pour référence |
| `SnakeGame-v9.0.0` | Artefacts archivés de la version 9.0.0 |
| `SnakeGameWpf - 7.0.0` | Artefacts archivés de l'ancienne version WPF 7.0.0 |

## Localisation

L'interface prend en charge le chinois simplifié (`zh-CN`), l'anglais (`en-US`) et l'allemand (`de-DE`). La langue se change dans `Paramètres - Affichage - Langue` et est enregistrée immédiatement. Toutes les chaînes passent par une couche de traduction unique basée sur un dictionnaire.

## Tiers et remerciements

- **Saukiya/Arknights** (<https://github.com/Saukiya/Arknights>) par **Saukiya** : prototype tower defense inspiré d'Arknights référencé par l'option expérimentale « mode tower defense amélioré ». Ce dépôt est un prototype de scripts Unity sans scènes ni ressources graphiques et ne déclare aucune licence open source ; tous les droits restent à son auteur. Le source est présent uniquement à titre de référence dans `External/Saukiya-Arknights` et `Assets/StreamingAssets/Saukiya-Arknights` et n'est pas compilé dans le jeu. Voir `THIRD-PARTY-NOTICES.md`.
- **Noto Sans SC** de Google : police intégrée pour l'affichage du chinois dans le build WebGL, sous licence SIL Open Font License 1.1. Voir `Assets/Resources/Fonts/OFL.txt`.

## Licence

Ce projet est publié sous licence MIT. Le texte complet se trouve dans [LICENSE](LICENSE).

## Liens

- Dépôt : <https://github.com/MEIMAOA/SnakeGame>
- Versions : <https://github.com/MEIMAOA/SnakeGame/releases>
- Site du projet et démo en ligne : <https://meimaoa.github.io/snakegame-web/>
- Auteur : <https://github.com/MEIMAOA>
