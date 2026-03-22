# LumiContact

LumiContact est une application mobile de gestion de contacts construite en trois parties:

- un client mobile Xamarin.Forms
- un backend .NET ASP.NET Core
- un canal temps reel par sockets pour propager les changements entre clients

Le projet fonctionne comme un carnet de contacts partage. Chaque client garde une base SQLite locale pour l'affichage immediat et l'usage hors ligne, puis synchronise avec le serveur. Quand un client cree, modifie ou supprime un contact, le backend diffuse un evenement socket pour que les autres clients se mettent a jour.

## Vue d'ensemble

### 1. Client mobile Xamarin

Le client est dans `/Users/maximevery/Projects/LumiContactApp/LumiContact`.

Stack principale:

- `Xamarin.Forms` pour l'interface multiplateforme
- `Xamarin.Essentials` pour les permissions, preferences, media picker, contacts natifs et lancement telephone/email
- `SQLite-net-pcl` pour la base locale
- `DependencyService` pour brancher les services natifs Android et iOS

Structure:

- `/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact`
  - projet partage
- `/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact.Android`
  - tete Android
- `/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact.iOS`
  - tete iOS

### 2. Backend .NET

Le backend est dans `/Users/maximevery/Projects/LumiContactApp/backend/LumiContact.Backend`.

Stack principale:

- `ASP.NET Core` pour l'API
- `Entity Framework Core + SQLite` pour la persistence serveur
- `SignalR` cote serveur
- `WebSocket` brut en plus pour le client Xamarin
- stockage disque pour les photos importees

Le backend expose:

- une API REST pour lire et modifier les contacts
- un point de sante
- un endpoint de configuration
- un hub SignalR
- un endpoint WebSocket pour le temps reel mobile

### 3. Sockets / temps reel

Le temps reel suit ce schema:

1. un client modifie un contact
2. le client pousse la modification au backend en HTTP
3. le backend enregistre la modification en base
4. le backend broadcast un message de changement
5. les autres clients recoivent l'evenement socket
6. chaque client relance un pull HTTP pour recharger la liste proprement

Le client mobile n'utilise pas `SignalR` en direct. Il utilise `ClientWebSocket` vers `/ws/contacts`, car cette approche est plus compatible avec ce projet Xamarin legacy.

## Comment Xamarin fonctionne dans ce projet

Le projet suit une architecture MVVM simple.

### Vue

La vue principale est [MainPage.xaml](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/Views/MainPage.xaml).

Elle contient plusieurs couches:

- la vue liste des contacts
- l'overlay de details
- l'overlay d'ajout/modification
- l'overlay de parametres
- le toast de notification

Le code-behind [MainPage.xaml.cs](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/Views/MainPage.xaml.cs) ne porte que la logique visuelle:

- animations d'ouverture/fermeture des overlays
- application du theme
- animation des cartes/boutons dans les parametres

### ViewModel

Le coeur applicatif est dans [ContactViewModel.cs](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/ViewModels/ContactViewModel.cs).

Ce fichier contient:

- le modele `Contact`
- le groupement des contacts
- `MainViewModel`

`MainViewModel` gere:

- chargement des contacts
- recherche
- ouverture et fermeture des vues
- ajout, edition, suppression
- favoris
- import de contacts natifs
- configuration de l'URL serveur
- synchronisation manuelle
- reaction aux evenements socket distants

### Services

Services principaux:

- [DatabaseService.cs](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/Services/DatabaseService.cs)
  - base SQLite locale
- [ContactSyncService.cs](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/Services/ContactSyncService.cs)
  - API HTTP + WebSocket client
- `IImageResizer`
  - redimensionnement de photos
- `IContactPhotoService`
  - lecture des photos natives du carnet du telephone

Les implementations natives sont dans:

- `/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact.Android/Services`
- `/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact.iOS/Services`

## Comment l'application fonctionne

### Ecran principal

L'utilisateur arrive sur une liste de contacts groupes:

- favoris en haut
- puis tri alphabetique
- recherche en direct
- swipe gauche pour appeler
- swipe droite pour supprimer

### Vue details

Quand on touche un contact:

- l'overlay de details s'ouvre
- on voit nom, telephone, email, notes, photo
- on peut appeler, envoyer un email, modifier ou supprimer

### Vue ajout / edition

Le formulaire permet:

- prenom
- nom
- telephone
- email
- commentaire
- photo depuis la galerie

En enregistrement:

1. le contact est ecrit en SQLite
2. il est marque `NeedsSync`
3. le client tente un push HTTP vers le backend
4. si le backend repond, le contact local recupere `RemoteId`, `RemoteVersion` et `LastSyncedAtUtc`

### Vue parametres

L'ecran de parametres contient:

- choix du theme clair/sombre
- URL du serveur
- bouton `Synchroniser maintenant`
- import depuis le telephone

Important:

- si l'URL serveur est vide, elle reste vide
- l'app ne reinjecte pas d'URL par defaut
- la sync manuelle affiche un message si aucune URL n'est renseignee

### Import de contacts

Lors d'un import:

1. l'app demande la permission Contacts
2. elle lit les contacts natifs via `Xamarin.Essentials`
3. elle tente de recuperer les photos natives via les services Android/iOS
4. elle ignore les doublons nom/prenom
5. elle enregistre les nouveaux contacts en local
6. elle lance ensuite une synchronisation serveur

### Synchronisation

La synchronisation combine deux mecanismes:

- HTTP pour les donnees
- WebSocket pour les notifications temps reel

Flux:

1. au demarrage, si une URL serveur existe, le client se configure
2. il ouvre une connexion WebSocket vers le backend
3. un sync manuel ou un changement local pousse les contacts en attente
4. le client recupere ensuite la liste distante
5. quand un autre client change quelque chose, le socket declenche un refresh

## Base locale

La base locale est creee par [DatabaseService.cs](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/Services/DatabaseService.cs).

Champs importants en plus des donnees contact:

- `RemoteId`
- `RemoteVersion`
- `NeedsSync`
- `LastSyncedAtUtc`

Ces champs servent a faire le lien entre le contact local SQLite et le contact du serveur.

## Backend: fonctionnement

Le backend principal est dans [Program.cs](/Users/maximevery/Projects/LumiContactApp/backend/LumiContact.Backend/Program.cs).

Il gere:

- verification simple par `X-Lumi-App-Key`
- API REST contacts
- photos
- diffusion temps reel
- generation des URLs publiques

Endpoints utiles:

- `GET /api/health`
- `GET /api/settings`
- `GET /api/contacts`
- `POST /api/contacts`
- `PUT /api/contacts/{id}`
- `DELETE /api/contacts/{id}`
- `GET /hubs/contacts`
- `GET /ws/contacts`

## Lancer le projet

### Client mobile

Build du projet partage:

```bash
dotnet build /Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/LumiContact.csproj
```

Build Android:

```bash
msbuild /Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact.Android/LumiContact.Android.csproj /t:Build /p:Configuration=Debug /p:Platform=AnyCPU
```

### Backend local

```bash
cd /Users/maximevery/Projects/LumiContactApp/backend/LumiContact.Backend
dotnet run
```

### Backend Docker

```bash
cd /Users/maximevery/Projects/LumiContactApp/backend
cp .env.example .env
docker compose up -d --build
```

## Deploiement

Le backend est pense pour etre expose derriere un reverse proxy avec une URL publique du type:

- `https://contact.ryvexam.fr`

Le client mobile stocke cette URL dans les parametres. C'est cette URL qui est utilisee pour:

- l'API HTTP
- la connexion WebSocket temps reel

## Fichiers importants

- [MainPage.xaml](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/Views/MainPage.xaml)
- [MainPage.xaml.cs](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/Views/MainPage.xaml.cs)
- [ContactViewModel.cs](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/ViewModels/ContactViewModel.cs)
- [DatabaseService.cs](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/Services/DatabaseService.cs)
- [ContactSyncService.cs](/Users/maximevery/Projects/LumiContactApp/LumiContact/LumiContact/Services/ContactSyncService.cs)
- [Program.cs](/Users/maximevery/Projects/LumiContactApp/backend/LumiContact.Backend/Program.cs)
- [backend/README.md](/Users/maximevery/Projects/LumiContactApp/backend/README.md)
