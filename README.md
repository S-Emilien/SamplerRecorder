# SamplerRecorder

SamplerRecorder est un enregistreur audio pour Windows conçu pour capturer le son du système, le réécouter avec une visualisation de la forme d’onde et extraire rapidement les passages souhaités au format MP3.

> Ce projet est mon premier essai de **full vibe coding**. Je l'ai réalisé comme une expérience personnelle afin de découvrir cette manière de développer, de me remettre à jour techniquement et d'évaluer concrètement les capacités actuelles des modèles d'IA de pointe.

## Aperçu

<img width="1132" height="707" alt="image2" src="https://github.com/user-attachments/assets/7b847f41-c80d-4e51-9d10-d2384a4f9a3b" />

## Fonctionnalités

- Enregistrement de la sortie audio de Windows avec sélection du périphérique
- Encodage MP3 en temps réel
- Démarrage automatique à la détection d'un son
- Suppression des périodes de silence après un délai configurable
- Mise en pause et reprise d'un enregistrement
- Ajout de marqueurs pendant l'enregistrement
- Sauvegarde et consultation des sessions précédentes
- Affichage interactif de la forme d'onde
- Navigation, zoom, déplacement et sélection précise d'une région
- Création, renommage, préécoute et suppression de clips
- Export individuel ou groupé des clips au format MP3
- Ajout de notes aux sessions
- Raccourcis clavier et souris globaux configurables
- Réglage indépendant du volume de lecture
- Interface sombre inspirée de la palette Dark+ de Visual Studio Code

## Aperçu du fonctionnement

1. Sélectionnez la sortie audio à enregistrer.
2. Activez éventuellement le démarrage au premier son ou le saut des silences.
3. Lancez l'enregistrement et ajoutez des marqueurs aux moments importants.
4. Ouvrez la session sauvegardée dans l'éditeur.
5. Sélectionnez une portion de la forme d'onde et créez un clip.
6. Exportez un clip ou l'ensemble des clips en MP3.

## Technologies utilisées

- [.NET 8](https://dotnet.microsoft.com/)
- WPF
- C#
- [NAudio](https://github.com/naudio/NAudio) pour la capture et la lecture audio
- [NAudio.Lame](https://www.nuget.org/packages/NAudio.Lame/) pour l'encodage MP3
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) pour l'architecture MVVM
- APIs Win32 pour les raccourcis globaux

## Prérequis

- Windows 10 ou Windows 11
- Le SDK [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- Un périphérique de sortie audio actif

SamplerRecorder repose sur la capture loopback WASAPI et n'est donc pas destiné à macOS ou Linux dans son état actuel.

## Installation et lancement

Clonez le dépôt puis placez-vous dans son dossier :

```powershell
git clone https://github.com/S-Emilien/SamplerRecorder.git
cd SamplerRecorder
```

Restaurez les dépendances et lancez l'application :

```powershell
dotnet restore
dotnet run
```

Pour compiler le projet :

```powershell
# Version de développement
dotnet build -c Debug

# Version optimisée
dotnet build -c Release
```

Les scripts `build_debug.bat` et `build_release.bat` permettent également de lancer ces deux compilations sous Windows.

## Données locales

Par défaut, les données de l'application sont stockées dans les emplacements suivants :

- Paramètres et journaux : `%APPDATA%\SamplerRecorder`
- Sessions : `%APPDATA%\SamplerRecorder\sessions`
- Clips exportés : `%USERPROFILE%\Documents\SamplerRecorder\Exports`

Chaque session contient le fichier audio `recording.mp3` ainsi qu'un fichier `session.json` regroupant ses métadonnées, ses marqueurs et ses clips. Le dossier des sessions et celui des exports peuvent être modifiés dans les paramètres de l'application.

## Structure du projet

```text
SamplerRecorder/
|-- Controls/       # Contrôles WPF personnalisés
|-- Converters/     # Convertisseurs de données pour l'interface
|-- Models/         # Sessions, marqueurs, clips et paramètres
|-- Resources/      # Ressources graphiques
|-- Services/       # Capture, export, stockage et raccourcis
|-- Themes/         # Thème visuel de l'application
|-- ViewModels/     # Logique de présentation MVVM
|-- MainWindow.xaml # Interface principale
`-- SamplerRecorder.csproj
```

## À propos de l'approche « full vibe coding »

L'objectif de ce dépôt n'était pas seulement de produire un enregistreur audio, mais aussi d'explorer un nouveau mode de collaboration avec l'IA : décrire une intention, itérer rapidement, tester le résultat puis guider les corrections et les améliorations.

Le code a ainsi été développé avec une forte assistance de modèles d'IA de pointe. Le projet constitue à la fois une application fonctionnelle et un retour d'expérience pratique sur ce que ces outils permettent aujourd'hui : prototypage rapide, génération d'interface, résolution de bugs, refactorisation et découverte d'une pile technique.

Cette démarche reste expérimentale. Certaines parties du code peuvent manquer de recul, de tests automatisés ou de validation sur une grande variété de configurations matérielles. Les retours, audits et propositions d'amélioration sont donc les bienvenus.

## État du projet

Version actuelle : **1.0.5**

SamplerRecorder est un projet personnel et expérimental. Avant un usage critique, il est recommandé de tester l'application avec votre propre configuration audio et de conserver une copie des enregistrements importants.

## Auteur

Projet personnel créé sous le pseudonyme **Net4King** dans le cadre d'une exploration personnelle du vibe coding et des capacités actuelles de l'intelligence artificielle appliquée au développement logiciel.
