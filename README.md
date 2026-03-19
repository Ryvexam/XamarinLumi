# 📱 LumiContact - Gestionnaire de Contacts Intelligent

![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20iOS-lightgrey?style=for-the-badge&logo=android)
![Framework](https://img.shields.io/badge/Framework-Xamarin.Forms-3498db?style=for-the-badge&logo=xamarin)
![Language](https://img.shields.io/badge/Language-C%23-239120?style=for-the-badge&logo=c-sharp)
![Architecture](https://img.shields.io/badge/Architecture-MVVM-ff69b4?style=for-the-badge)
![Database](https://img.shields.io/badge/Database-SQLite-003B57?style=for-the-badge&logo=sqlite)
![Status](https://img.shields.io/badge/Status-Completed-success?style=for-the-badge)

LumiContact est une application mobile multiplateforme (Android & iOS) développée avec **Xamarin.Forms**. Elle propose une gestion de contacts moderne, fluide et élégante, intégrant des fonctionnalités avancées comme le mode sombre, l'importation de contacts natifs et des interactions par balayage (Swipe).

---

## ✨ Fonctionnalités Principales

*   **Gestion Complète (CRUD) :** Créer, lire, modifier et supprimer des contacts.
*   **Importation Native :** Importation des contacts depuis le répertoire du téléphone (Google Contacts, iCloud, etc.) avec récupération automatique des photos de profil.
*   **Favoris & Groupement :** Mise en favoris d'un clic (icônes dynamiques). La liste est automatiquement triée par ordre alphabétique, avec les favoris épinglés en haut (★ Favoris).
*   **Interactions Modernes :** 
    *   *Swipe-to-Call* (Balayage à gauche pour appeler).
    *   *Swipe-to-Delete* (Balayage à droite pour supprimer).
*   **Personnalisation UI :** Génération de couleurs aléatoires pour les avatars des contacts sans photo.
*   **Thèmes :** Support complet du Mode Clair et Mode Sombre avec persistance des préférences.
*   **Protection Mémoire :** Redimensionnement natif des images (Compression) pour éviter les crashs de type *Out Of Memory* lors de l'ajout de photos haute résolution.

---

## 🏗 Architecture du Projet

L'application est architecturée selon le motif de conception **MVVM (Model-View-ViewModel)**. Ce pattern permet de séparer proprement l'interface graphique de la logique métier et des données.

### 1. Le projet partagé (`LumiContact`)
C'est ici que se trouve 90% du code de l'application, commun à Android et iOS.

*   **`/Models`** *(Intégré dans ContactViewModel.cs pour ce projet)*
    *   `Contact` : Représente la structure d'un contact (Id, Prénom, Nom, Téléphone, Photo, IsFavorite, etc.) et configure la table SQLite.
*   **`/Views`** (L'interface visuelle)
    *   `MainPage.xaml` : L'interface utilisateur écrite en XAML (Liste, Formulaires modaux, Boutons).
    *   `MainPage.xaml.cs` : Le "Code-Behind", utilisé **uniquement** pour gérer ce qui est purement visuel (Animations d'apparition des popups, application dynamique des couleurs du thème).
*   **`/ViewModels`** (Le cerveau)
    *   `ContactViewModel.cs` (`MainViewModel`) : Contient toute la logique métier. Il charge les données de la base, filtre la recherche, gère l'état de l'interface (modales ouvertes/fermées) et réagit aux clics de l'utilisateur via des `Commands` (`SaveContactCommand`, `ImportContactsCommand`, etc.).
*   **`/Services`** (Les outils)
    *   `DatabaseService.cs` : Gère la connexion à la base de données locale **SQLite** et les requêtes (Get, Save, Delete).
    *   `IImageResizer.cs` & `IContactPhotoService.cs` : Interfaces de *DependencyService* pour faire appel à des fonctionnalités natives spécifiques au système d'exploitation.

### 2. Les projets natifs (`LumiContact.Android` & `LumiContact.iOS`)
Ils contiennent le code spécifique à chaque plateforme.

*   **`MainActivity.cs` / `AppDelegate.cs`** : Les points d'entrée respectifs de l'application sur Android et iOS.
*   **`Properties/AndroidManifest.xml`** : Déclare les permissions requises sur Android (`READ_CONTACTS`, `READ_EXTERNAL_STORAGE`, etc.).
*   **`/Services`** (Implémentations natives) :
    *   `ImageResizer.cs` : Utilise les librairies natives (Android Bitmap / iOS UIImage) pour compresser les photos.
    *   `ContactPhotoService.cs` : Interroge les API systèmes (`ContactsContract` sur Android, `CNContactStore` sur iOS) pour extraire les vraies photos de profil du téléphone.

---

## 🛠 Composants Techniques Clés

1.  **Xamarin.Forms & XAML :** Création de l'interface utilisateur multiplateforme.
2.  **Xamarin.Essentials :** Utilisé pour accéder aux fonctionnalités de base du téléphone :
    *   `Preferences` : Sauvegarde du thème et des paramètres.
    *   `Contacts.GetAllAsync()` : Lecture du répertoire téléphonique.
    *   `Permissions` : Demande d'autorisation à l'utilisateur.
    *   `Launcher` : Lancement de l'application d'appel (`tel:`) et d'email (`mailto:`).
    *   `MediaPicker` : Sélection de photos depuis la galerie.
3.  **SQLite-net-pcl :** ORM léger pour la sauvegarde persistante des contacts en local sur l'appareil.
4.  **DependencyService :** Mécanisme d'injection de dépendances de Xamarin permettant au code partagé d'appeler du code C# spécifique à Android ou iOS (utilisé pour les photos et la compression).

---

## 🚀 Installation & Exécution

### Prérequis
*   Visual Studio 2022 (Windows/Mac) ou Visual Studio Code.
*   Charge de travail (Workload) "Développement mobile en .NET" (Xamarin) installée.
*   Un émulateur Android/iOS ou un appareil physique configuré pour le débogage.

### Étapes
1. Clonez ou téléchargez le dépôt.
2. Ouvrez la solution `LumiContact.sln` dans Visual Studio.
3. Restaurez les packages NuGet (clic droit sur la solution > *Restaurer les packages NuGet*).
4. Définissez `LumiContact.Android` ou `LumiContact.iOS` comme projet de démarrage.
5. Cliquez sur le bouton "Exécuter" (Play) pour compiler et lancer l'application sur votre émulateur ou appareil.

*Note pour iOS : L'exécution sur un appareil physique nécessite un compte développeur Apple valide et un Mac (ou une connexion à un Mac depuis Windows).*