# Monsuta Potaru

Banque Pokémon privée pour un cercle d'amis, basée sur [PKHeX.Core](https://github.com/kwsch/PKHeX). Anciennement
« PokeBank » (renommé pour s'éloigner de Pokémon Bank, le service de Nintendo) : le homebrew 3DS garde pour l'instant
son nom, **PokeBank Sync**, et les clés de connexion leur nom technique « PokeBank » (`SetApplicationName` dans
`Program.cs` : le changer déconnecterait tout le monde).

**Pour les joueurs** : le mode d'emploi du site est dans le [guide du joueur](https://docs.google.com/document/d/1cCz7BaDp5PWYgcoyKmB7m63tj9R6L9jGBg1o5HDLijM/edit)
(Google Doc). Ce README est pour les développeurs.

- **Stack** : ASP.NET Core 10 + Blazor Server (tout en C#, pas de JavaScript à écrire), SQLite, ASP.NET Identity.
- **Jeux testés** (vraies sauvegardes, `RealSavesTests` : voir plus bas) : Rouge, Bleu, Argent, Rouge Feu, Vert Feuille,
  SoulSilver, Noire, Noire 2, Rubis Oméga, Ultra-Soleil, Épée ; Platine par la sauvegarde d'exemple. Pas encore de vraie
  sauvegarde de la 9e génération (Écarlate / Violet), ni de Let's Go, Diamant Étincelant ou Légendes Arceus : PKHeX sait
  les lire, à valider au cas par cas.

## Lancer en local

Les images (Pokémon, objets, sprites Donjon Mystère, ~90 Mo) ne sont pas dans le dépôt Git : elles sont stockées sur le
serveur. Après avoir cloné, les récupérer par SSH (une archive, quelques secondes) :

```bash
outils\recuperer-images.cmd debian@IP_DU_VPS
```

Sans accès au serveur, `python outils/images.py` les reconstruit depuis leurs projets d'origine (~10 minutes). Sans images,
le site marche mais affiche des points d'interrogation, et les tests d'images échouent avec un message qui l'explique.

Les combats font tourner le simulateur de Pokémon Showdown avec Node.js (22 ou plus) : l'installer une fois après avoir
cloné (sans lui, le site marche mais les combats et leurs tests échouent avec un message qui l'explique) :

```bash
npm ci --ignore-scripts --prefix showdown
```

Sous PowerShell, si l'exécution des scripts est bloquée (« npm.ps1 ne peut pas être chargé »), appeler `npm.cmd` à la
place (`npm.cmd ci --ignore-scripts --prefix showdown`) : inutile de changer la politique d'exécution de Windows.

```bash
dotnet run --project MonsutaPotaru
```

Puis ouvrir http://localhost:5265. En développement, le code d'invitation de départ est `pokeamis`
(voir `MonsutaPotaru/appsettings.Development.json` ; ensuite, page « Administration »). La base SQLite est créée automatiquement dans `MonsutaPotaru/Data/app.db`.

Tests :

```bash
dotnet test
```

Trois sortes de tests (xUnit) : les **services** sur une base SQLite en mémoire (`TestDatabase`) ; les **pages et composants**
Blazor rendus par [bUnit](https://bunit.dev) avec les vrais services (`PageTestContext` : compte connecté, JavaScript du navigateur
simulé, deux joueurs sur la même page pour les échanges et les combats) ; le **site entier** démarré par `Program.cs`
(`SiteFactory`, WebApplicationFactory : base, copies et clés dans un dossier temporaire, jamais `Data/app.db`) pour
l'inscription, la connexion, les pages du compte (formulaires envoyés au serveur), le rendu serveur de chaque page et
l'API de la 3DS. Couverture mesurée par SonarCloud (lignes et conditions) : environ 88 %, objectif 70 % (voir « CI/CD »).

`exemple-blanc2.sav` est une sauvegarde de test (Pikachu + Évoli) pour essayer le site sans vraie partie.
Pour la régénérer : `MONSUTA_POTARU_SAMPLE_OUT=chemin/fichier.sav dotnet test`.

`exemple-platine.sav` est une sauvegarde Platine (dresseur Lucas, 50 000 ₽) avec 50 Pokémon légaux d'espèces différentes,
tirés des vraies rencontres du jeu, et dans le sac 62 Pokéballs (Poké, Super, Hyper, Master, Luxe et Honor Ball),
26 médicaments (Potion, Super Potion, Total Soin, Rappel, Huile, Super Bonbon), 23 baies (Ceriz, Oran, Prine,
Sitrus, Enigma), 11 objets (Pierre Lune, Pierre Feu, Pépite, Restes, Attaque + dans la poche Objets de combat),
7 CT (2 CT01 Mitra-Poing, 3 CT06 Toxik, CT24 Tonnerre, CT26 Séisme), et ce que le site ne doit ni montrer ni toucher :
deux CS (Coupe, Surf), le Vélo (objet rare) et une Lettre Herbe.

Pour simuler « j'ai rejoué un peu » et passer la règle anti-copie de « Mes sauvegardes » (voir « Sauvegardes stockées » plus bas),
`MONSUTA_POTARU_ADD_PLAYTIME=chemin/fichier.sav dotnet test` ajoute une heure de jeu au fichier, modifié sur place (n'importe quel jeu). Pour la régénérer : `MONSUTA_POTARU_SAMPLE_PT_OUT=chemin/fichier.sav dotnet test`.

**Vraies sauvegardes de test** : les fichiers déposés dans `MonsutaPotaru.Tests/Sauvegardes/` (versionnés, malgré la règle `*.sav`) passent tout le parcours du site dans `RealSavesTests` : lecture et réécriture à l'identique,
dépôt d'un Pokémon puis retour au même emplacement, stockage (tous ses Pokémon dans la collection), retrait d'argent,
retrait de tous les objets du sac et du PC puis dépôt de retour (objets rares, CS et Cristaux Z intacts). Il suffit d'y ajouter un
fichier pour qu'il soit testé ; sans le dossier, ces tests ne vérifient rien. Actuellement : Rouge, Bleu, Argent (partie
randomisée : ses Pokémon sont signalés illégaux), Rouge Feu, Vert Feuille (2 parties de 3e génération, pour les combats ; PKHeX les reconnaît toutes deux comme Rouge Feu, `SaveVersion` reconnaît Vert Feuille d'après ses Pokémon), SoulSilver, Noire (`noir.sav`), Noire 2, Rubis Oméga, Ultra-Soleil, Épée. SoulSilver : les blocs annexes
jamais écrits par le jeu (Tour de Combat, vidéos...) restent à zéro, sans somme de contrôle valide, et PKHeX marque à
l'écriture toutes les boîtes comme modifiées : le fichier change une fois, puis plus. En 1re et 2e génération, le fichier réécrit n'est pas identique octet pour octet (le contenu l'est) :
le jeu ne prépare ses boîtes qu'au premier changement de boîte et PKHeX les écrit vides en règle ; et une boîte y est une
liste sans trou, tassée à l'écriture comme le fait le jeu (un Pokémon déposé dans la banque laisse sa place au suivant).

## Fonctionnement

Le mode d'emploi du site, pour les joueurs, est dans le **[guide du joueur](https://docs.google.com/document/d/1cCz7BaDp5PWYgcoyKmB7m63tj9R6L9jGBg1o5HDLijM/edit)**
(Google Doc, un chapitre par menu). Ici, seulement les règles que le code doit respecter et où elles sont ; le détail de
chaque sujet est dans sa fiche `docs/claude/` (voir `CLAUDE.md`).

1. L'utilisateur charge sa sauvegarde (`.sav`, `.dsv`…) dans « Transfert ».
2. Il dépose / retire des Pokémon : les changements restent **en attente** (en mémoire, `SaveSession`) et sont annulables.
3. « Valider et télécharger » : la base est mise à jour dans une transaction, une copie *avant/après* de la
   sauvegarde est écrite dans `Data/backups/<id utilisateur>/`, et la sauvegarde modifiée est téléchargée.
4. L'utilisateur **remplace** sa sauvegarde par ce fichier.

Un retrait convertit le Pokémon au format du jeu si PKHeX le peut (PK4 → PK5) et refuse ce qui est incompatible (espèce,
attaques ou objets absents du jeu, génération antérieure).

### Comptes

`Services/AccountService.cs`, pages dans `Components/Account` (ASP.NET Identity, messages en français :
`FrenchIdentityErrorDescriber`). Détails : `docs/claude/comptes.md`.

- **Inscription** : pseudo, adresse, mot de passe, **code d'invitation** (en base, voir « Administration » ; majuscules,
  tirets et espaces ignorés). Rôle Standard. Mot de passe de 12 caractères au moins (`AccountService.PasswordMinLength`),
  majuscule, minuscule, chiffre, caractère spécial (un ancien compte garde le sien jusqu'au changement).
- **Pseudo** : 3 à 20 caractères (lettres, accents, chiffres, espaces, tirets, points, tirets bas), unique sans la casse,
  **définitif** ; c'est le `UserName` d'Identity. L'adresse n'est montrée à personne. Compte d'avant le pseudo (`UserName`
  = adresse, « @ » interdit dans un pseudo : `AccountService.HasPseudo`) : `Shared/PseudoGuard.razor` le renvoie sur
  `/Account/Pseudo` ; la 3DS continue de marcher.
- **Connexion** par l'adresse (`RequireUniqueEmail`), pas par le pseudo, sans double authentification. Contre les essais :
  5 échecs d'affilée bloquent le compte 5 minutes (`AccountService.FindForLoginAsync`) ; 10 envois par minute et par IP
  pour la connexion, 10 par heure pour l'inscription (`AccountLimiter`, IP transmise par Caddy) ; les mauvais mots de
  passe de « Mon compte » (`CheckPasswordAsync`) comptent avec ceux de la connexion ; un seul message pour tout échec, et
  le mot de passe est vérifié même pour une adresse inconnue (durée de réponse identique).
- **Mon compte** : Profil, Adresse mail (changée directement, aucun mail envoyé), Mot de passe, Visibilité, Thèmes,
  Données personnelles (JSON ; suppression du compte et de tout ce qu'il contient).
- **Retirés** : téléphone, double authentification, connexions externes, mot de passe oublié, confirmation par mail
  (migration `RemovePhoneAndTwoFactor`). Un mot de passe oublié passe par l'administrateur.

### Multijoueurs

Détails et pièges (même composant pour deux adresses, valeurs trafiquées) : `docs/claude/multijoueurs.md`.

- **Rechercher un joueur** (`/joueurs`, `Pages/PlayerSearch.razor`) : 2 caractères du pseudo au moins, casse et accents
  ignorés, parmi les profils trouvables. Profil : `/joueurs/{pseudo}` (`Pages/PlayerProfilePage.razor`,
  `Shared/PlayerHeader.razor`). Collection, Parc, Inventaire et Living dex ont une seconde adresse
  (`/joueurs/{pseudo}/collection`...) en **lecture seule** : aucun bouton qui modifie, copie ou télécharge (et les actions
  refusent un visiteur), ni journal, ni noms de fichiers, ni ID de dresseur (`PokemonCard.HideTrainerId`).
- **Visibilité** (colonnes `Audience` et `PublicPages`) : Tout le monde (défaut), Amis seulement (trouvable, pages pour
  les amis), Personne (introuvable, rien de visible, même des amis ; ami par code seulement). Un profil introuvable ou une
  page non visible répond comme un joueur inexistant, administrateurs compris (`Services/PlayerService.cs`).
- **Amis** (`/amis`, `Pages/Friends.razor`, `Services/FriendService.cs`) : code ami `ABCD-EFGH` permanent et
  régénérable (`FriendCode`), demandes reçues et envoyées, compteur rouge dans le menu. Deux demandes croisées sont
  acceptées d'emblée. Tables `FriendRequests` (une par sens et par paire) et `Friendships` (deux lignes par amitié),
  supprimées avec le compte.

### Combats

`/combats`, `Pages/Battles.razor` ; détails : `docs/claude/combats.md`.

- 3e génération pour l'instant (Rubis, Saphir, Émeraude, Rouge Feu, Vert Feuille) : combat simple, 6 Pokémon, niveaux
  réels, sans clause ; illégaux acceptés mais signalés ; égalité = match nul. Les sauvegardes ne sont jamais modifiées.
- **Moteur** : [Pokémon Showdown](https://github.com/smogon/pokemon-showdown) (`@pkmn/sim`, dossier `showdown/`), un
  processus Node.js par combat (`showdown/combat.js`, lancé par `Services/Battle/ShowdownBattle.cs`). Le pont ne
  transmet que les choix (« p1 move 2 ») et la fin forcée, **jamais `eval`** ; la graine est tirée par le serveur. PP Plus
  réels transmis. Deoxys a la forme du jeu de sa sauvegarde.
- **Déroulé** : avec un ami, en direct ; paire de sauvegardes de même génération (`BattleService.SavePairsAsync`, amitié
  revérifiée), demande valable 10 minutes, un combat à la fois. Équipe de 6 (équipe et boîtes, sans œufs), place et
  empreinte revérifiées au départ (`BattleService.TeamAsync`).
- **Combat** (`Services/Battle/BattleHub.cs`, singleton en mémoire) : chaque choix est vérifié par le serveur sur la
  dernière requête de Showdown ; 60 secondes par choix (`BattleHub.Tick`), sinon défaite ; fuite, départ de la page ou
  onglet fermé : défaite ; simulateur arrêté par erreur : combat annulé.
- **Affichage** façon Platine (`app.css`, « Combats » ; `BattleTiming`), sprites du jeu de chaque joueur
  (`Services/Battle/BattleSprites.cs`), traduction française du protocole (`Services/Battle/BattleView.cs`,
  `BattleNames` ; test `BattleTests.RandomBattles_EndAndAreFullyTranslated`).
- **Résultats** : table `BattleRecords` (`Services/Battle/BattleRecorder.cs`, événement `BattleHub.Finished`), sans clé
  étrangère.

### Échanges

`/echanges`, `Pages/Trades.razor` ; mécanique `Services/TradeService.cs`, déroulé en direct `Services/TradeHub.cs`
(singleton : un redémarrage annule les échanges en cours).

- Entre deux sauvegardes stockées du **même contexte de jeu** de PKHeX (Noire ↔ Noire 2, Platine ↔ HeartGold ; pas Let's
  Go ↔ Soleil, ni Épée ↔ Diamant Étincelant), avec un ami, en direct. Le serveur revérifie l'amitié avant de montrer les
  sauvegardes de l'autre (`TradeService.SavePairsAsync`). Demande valable 10 minutes, un échange à la fois.
- Chacun choisit le Pokémon qu'il **reçoit** (équipe et boîtes, hors cases verrouillées, œufs compris), parmi ceux que sa
  sauvegarde accepte (`SaveFile.IsCompatiblePKM` après conversion) ; validation par les deux.
- **Échange** : chaque Pokémon prend la place de l'autre, au format de l'autre jeu, avec le joueur comme dresseur actuel
  (`EntityImportSettings.All`) ; évolution par échange selon PKHeX (simple, objet tenu consommé, Carabing ↔ Escargaume ;
  ni œuf, ni Pierre Stase). Refusé si un Pokémon a changé depuis le choix (empreinte) ou si une équipe ne garderait que
  des œufs. Les deux sauvegardes sont écrites dans une même transaction (copies `echange-avant` / `echange-apres`) et
  marquées modifiées sur le serveur (`SaveModification.PokemonTraded`).
- Quitter la page pendant un échange demande confirmation (`NavigationLock`) puis l'annule.
- **Statistiques** (`/statistiques`, `Pages/Statistics.razor`) : tables `TradeRecords` et `BattleRecords`, sans clé
  étrangère (elles survivent à la suppression du compte du partenaire).

### Thèmes

`Account/Pages/Manage/Themes.razor`, liste et défaut dans `Services/SiteThemes.cs` ; détails : `docs/claude/comptes.md`.

- Choix gardé dans le compte (colonne `Theme`, null = défaut, jamais enregistré : un compte qui n'a rien choisi suit un
  changement de défaut). Le serveur pose la clé sur la page dès le premier rendu (`<html data-theme="...">`, `App.razor`).
- **Ardoise** (défaut, `wwwroot/themes/ardoise.css`) : le seul thème à cadre propre (`Components/Layout/ArdoiseLayout.razor`,
  choisi par `MainLayout` d'après la valeur en cascade `SiteTheme`) : menu repliable (cookie `menu=replie`, relu par
  `App.razor`), fil d'Ariane (`Components/Layout/Breadcrumbs.cs`), menu du compte en `details` ; tout marche sans
  interactivité (clics gérés par `wwwroot/js/app.js`). **Pokédex** (`wwwroot/themes/pokedex.css`, tout sous
  `[data-theme="pokedex"]`) ; **Classique** (aucun style à lui).
- Les styles du menu sont isolés par composant (`NavMenu.razor.css`) : les sélecteurs d'un thème commencent par `html`
  pour passer devant. `app.css` fixe certaines couleurs en dur (`.btn-primary`, `.btn-link`, anneau du focus) : un thème
  doit les écraser explicitement.
- Compteurs du menu (porte-monnaie, demandes) : `Components/Layout/SiteMenuBase.cs`, communs aux deux menus.
- **Une nouvelle page** : un `Shared/PageHeader.razor` en tête (titre h1, nombre, description, actions, icône), des
  `Shared/Block.razor` pour ses parties (titre h2 ; `Flush` : liste collée aux bords).

### Sécurité

- **Connexion et inscription** : voir « Comptes » (blocage après 5 échecs, limites par adresse IP).
- **En-têtes de toutes les réponses** (`SecurityHeaders.cs`, branché au début de `Program.cs`, fichiers statiques et API
  compris) : `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin`, et une **politique de contenu (CSP)
  stricte** qui n'accepte que le site : scripts, images (plus les icônes `data:` du CSS), connexions (dont le WebSocket de
  Blazor), formulaires ; ni plugin, ni page du site dans le cadre d'un autre site. Les styles écrits dans la page restent
  permis (Blazor en écrit). La CSP que Blazor ajoute de lui-même est retirée (`ContentSecurityFrameAncestorsPolicy`) : la
  nôtre la contient. En `Development` seulement, les scripts de rechargement de Visual Studio (`localhost:*`) sont permis.
- **Aucun script écrit dans la page** : la CSP ne l'exécuterait pas. Les scripts du site sont dans `wwwroot/js` :
  `app.js` (chargé dans le `<head>` d'`App.razor` : images de remplacement, téléchargement, largeur d'écran, grille de
  l'inventaire, glisser-déposer, fermeture du menu sur téléphone) et `parc.js`. Pas de `onerror` / `onclick` dans le
  HTML : une image absente prend `data-remplacement="images/…/inconnu.png"` (ou `data-cacher-si-absente`), un seul
  écouteur d'`app.js` s'en charge. Pas d'`<ImportMap />` (script écrit dans la page) : `parc.js` est chargé par son
  chemin, et charge Phaser par le sien (`wwwroot/lib/phaser`, servi par le site). Test
  `SecurityHeadersTests.Pages_HaveNoInlineScript`.
- **Fichiers envoyés** : 8 Mo au plus depuis le navigateur (« Mes sauvegardes », « Transfert » :
  `StoredSaveService.ReadUploadAsync`), 4 Mo depuis la 3DS (`SyncEndpoints.MaxSaveBytes`).
- **Ce que le navigateur envoie peut être trafiqué** (valeurs d'événements, arguments des méthodes appelées par
  JavaScript) : les services revérifient le compte, l'amitié (`TradeService.SavePairsAsync`) et la visibilité
  (`ParkService.GetMemberAsync`, `PlayerService.FindAsync`).

### Administration

Rôles Identity **exclusifs** (`Data/SiteSetting.cs`, `SiteRoles`, créés avec la base, identifiants fixes) : **Standard**
pour tous, **Administrateur** en plus pour ceux qui gèrent le site. Page `/administration` (`Components/Pages/Admin.razor`,
lien du menu pour les seuls administrateurs ; les autres lisent « Accès refusé ») :

- **Code d'invitation** (`ABCD-EFGH`, sans caractères ambigus) : régénéré après confirmation, l'ancien ne marche plus.
  Table `SiteSettings` : au premier démarrage, il reprend `Bank:InviteCode` (`INVITE_CODE` du `.env`) ou est tiré au
  hasard ; ensuite la configuration n'est plus lue.
- **Comptes** : pseudo et rôle (jamais l'adresse), rôle administrateur donné ou retiré. Pas sur soi-même, et il en reste
  toujours un (vérifié dans la requête même du retrait). Un changement de rôle renouvelle le tampon de sécurité
  (déconnexion sous 30 minutes) ; chaque action revérifie le rôle en base (`AdminService`).
- **Premier administrateur** : le pseudo `Bank:AdminPseudo` (`ADMIN_PSEUDO` du `.env`), **tant que le site n'en a aucun**
  (au démarrage, à l'inscription ou au choix du pseudo) ; ignoré ensuite.
- **Mode debug** (`Services/DebugService.cs`) : **en mémoire** (`DebugMode`), pour le compte, jusqu'à sa déconnexion ou au
  redémarrage ; chaque action revérifie en base que le compte est administrateur. Inventaire : +10 000, +100 000 et
  +1 000 000 ₽ (montants fixes, tout autre refusé, écrits au journal) ; Parc : « + taille » / « − taille » (gratuits, hors
  journal ; réduit, le parc rend ses derniers arrivés à la collection), « Habitats (debug) », « Attaquer (debug) ».

### Sauvegardes stockées (consultation)

`Pages/Saves.razor`, `Services/StoredSaveService.cs`. Les Pokémon d'une sauvegarde stockée (équipe, boîtes, pension...)
sont dans la collection en **lecture seule** (ni retrait vers un jeu, ni téléchargement `.pkX`).

- **Une partie stockée une seule fois sur tout le serveur**, tous comptes confondus : génération + jeu + ID dresseur / ID
  secret + nom du dresseur (`SaveIdentity`) ; dissuasif, pas infaillible. Pas d'ID secret en 1re et 2e génération.
- Tant qu'elle est stockée, la sauvegarde est **refusée dans « Transfert »** (au chargement et à la validation).
- Mise à jour avec moins de temps de jeu : confirmation demandée.
- **Suppression** : « télécharger et supprimer » ou « supprimer sans télécharger », ce second choix refusé par le serveur
  (`StoredSaveService.DeleteWithoutDownloadAsync`) tant qu'une version modifiée sur le serveur n'a pas été téléchargée.
  Une copie est gardée dans `Data/backups/` à chaque fois (non mentionnée au joueur).
- **Nom de fichier** : il accompagne « Jeu — Dresseur » partout (en dessous, ou « Noire — Ghal · noir.sav » sur une
  ligne). Le journal garde le nom du moment de l'opération (`WalletOperation.SaveLabelOf` ; migration `JournalFileNames`).
- **Rouge Feu ou Vert Feuille** : même format, PKHeX répond toujours Rouge Feu. `SaveVersion` (à chaque lecture, rien
  d'écrit dans le fichier) nomme la sauvegarde Vert Feuille si les Pokémon du dresseur (même nom, ID et ID secret) en
  viennent en majorité ; son Deoxys prend alors la forme Défense en combat. `SaveIdentity` garde Rouge Feu pour les deux,
  pour que l'empreinte ne change pas. Les anciennes sont renommées au démarrage (`StoredSaveService.FillGameNamesAsync`).
- **Modifiée sur le serveur** (règle anti-copie, `SaveModification`, écrite par `InventoryService.SaveServerChangeAsync`) :
  après un retrait d'argent ou d'objets, un dépôt d'objets ou un échange, la mise à jour est bloquée tant que la version
  modifiée n'a pas été téléchargée, puis n'accepte qu'un fichier avec **plus** de temps de jeu, ou à temps égal le fichier
  exact rendu par le serveur (empreinte SHA-256 ; en Game Boy, la même mémoire du jeu, les 32 premiers Ko :
  `StoredSaveService.SameGameBoyMemory`). L'ancienne sauvegarde du jeu est refusée. Dissuasif, pas infaillible.

### Doublons (vieille copie d'une sauvegarde)

Un Pokémon déposé peut réapparaître dans une **vieille copie** de sa sauvegarde. Chaque Pokémon a une **empreinte**
(`BankPokemon.Fingerprint`, `Services/PokemonFingerprint.cs`), sans l'espèce, qui ne change pas à l'évolution :

- 3e génération et suivantes : PID + ID dresseur complet (ID + ID secret) ;
- 1re et 2e génération (sans PID) : IV + ID dresseur + nom du dresseur + famille d'évolution (environ 1 risque sur 65 000
  de confondre deux Pokémon du même dresseur et de la même famille).

Une sauvegarde dont un Pokémon est déjà **ailleurs dans le compte** (stockage interne ou autre sauvegarde stockée ;
**jamais entre comptes**) est **refusée**, avec 5 noms au plus dans le message : « Mes sauvegardes » (ajout et mise à
jour ; une mise à jour ne se compare pas à sa version précédente, et le contrôle passe avant la question du temps de jeu),
la 3DS (`raison` : `Duplicate`) et « Transfert » (au chargement ; la validation refuse aussi le dépôt d'un Pokémon déjà
au stockage interne, avant les retraits). Revers acceptés : Pokémon d'événement à PID fixe ou d'échange en jeu, et un
Pokémon échangé entre deux parties du même compte. Empreinte calculée à l'enregistrement, et au démarrage pour les plus
anciens (migration `PokemonFingerprints`). Détails : `docs/claude/collection.md`.

### Synchronisation depuis une 3DS

Le homebrew **PokeBank Sync** (dépôt à part, `3DS_Saves_Synchronization`) envoie les sauvegardes d'une 3DS vers les
sauvegardes stockées et les récupère. Page `/sauvegardes/3ds` (`Pages/Sync3ds.razor`), service `SyncService`.

- **Liaison** : code de 8 caractères (`ABCD-EFGH`, sans 0/O ni 1/I/L), 10 minutes, une seule fois, échangé contre un
  **jeton longue durée** gardé sur la carte SD ; le site n'en garde que l'empreinte SHA-256. La page relit toutes les 3
  secondes tant que le code est affiché. **Retirer** une console rend son jeton inutilisable.
- **Envoi** : ajoute une partie, ou met à jour celle **de ce compte**, avec toutes les règles des sauvegardes stockées.
  Fichier identique ignoré (`inchangee` ; en Game Boy, même mémoire du jeu) ; sous un autre nom, seul le nom change. Rien
  n'est jamais forcé. Nom de fichier nettoyé (sans dossier, 100 caractères au plus). Emplacement sur la console (`cle`)
  noté à chaque envoi réussi (table `SyncSaveLocations`).
- **Récupération** : la console fait **comparer** sa sauvegarde par le site avant de la remplacer : refusé si autre
  partie ou plus de temps de jeu sur la console ; identique : « à jour » ; sinon récupérable. Le téléchargement par la
  console vaut « Télécharger » (lève le blocage de « Mettre à jour »). Cas sans issue : la console a joué après une
  modification du site sans la récupérer.

API (JSON, messages en français prêts à afficher ; `MonsutaPotaru/Api/SyncEndpoints.cs`) :

| Requête | Réponse |
|---|---|
| `POST /api/sync/liaison` `{"code", "nom"}` | `{"jeton", "compte", "message"}` ; 400 si le code est faux ou expiré, 429 au-delà de 10 essais par minute depuis la même adresse IP (celle transmise par Caddy) |
| `GET /api/sync/compte` | `{"compte", "console"}` : vérifie le jeton (`compte` : le pseudo, ou l'adresse d'un compte qui ne l'a pas encore choisi) |
| `POST /api/sync/sauvegardes?fichier=nom.sav&cle=...`, corps = fichier brut (4 Mo au plus) | `{"statut", "raison", "message"}` ; `statut` : `ajoutee`, `mise-a-jour`, `inchangee` ou `refusee` (`raison` : `StoreStatus`) |
| `GET /api/sync/sauvegardes` | `{"sauvegardes": [{"id", "jeu", "dresseur", "idDresseur", "fichier", "empreinte", "tempsDeJeu", "modifieeSurSite", "console", "famille", "cles"}]}` ; `console` : `3ds`, `ds`, `gba`, `gb` ou `autre` ; `famille` en DS : `DP`, `Pt`, `HGSS`, `B`, `W`, `BW`, `B2`, `W2`, `B2W2` ; en Game Boy : `RB` (Rouge, Bleu, Vert), `YW` (Jaune), `GS` (Or, Argent), `C` (Cristal), suivis de `-JP` ou `-KO` (versions japonaise et coréenne, de disposition différente) (`SyncService.DescribeForConsole`) |
| `POST /api/sync/sauvegardes/{id}/comparer`, corps = sauvegarde de la console | `{"verdict", "raison", "modifieeSurSite", "message"}` ; `verdict` : `recuperable`, `a-jour` ou `refusee` ; `raison` d'un refus : `autre-partie` (la console cherche alors une autre place), `plus-avancee`, `illisible`, `absente` |
| `GET /api/sync/sauvegardes/{id}?cle=...` | Fichier brut de la version du site (404 si elle n'existe plus) |

Toutes sauf la liaison demandent `Authorization: Bearer <jeton>` (401 sinon, jeton inconnu ou console retirée).

### Filtres de « Ma collection »

Pastilles de filtres (`Pages/Collection.razor`), aussi dans la fenêtre de choix du « Parc » : génération (d'après le
numéro de Pokédex), type (types récents de l'espèce et de la forme), sexe, nature, talent (ceux de la collection),
IV (minimum par statistique), niveau (« de » / « à », bornes comprises, inversées si besoin), forme, Méga-Évolution
(gemme ou orbe tenus), chromatique, surnom (différent du nom de l'espèce) et œuf. Valeurs d'un filtre en « ou », filtres
en « et » ; les œufs sont exclus dès qu'un autre filtre qu'« Œuf » est actif (leur espèce reste cachée). Tout est gardé
dans l'adresse avec la recherche, l'origine et le tri
(`?generation=1,2&type=feu,eau&sexe=f&iv=pv31,vit25&niveau=20-40&surnom=oui&mega=oui`, `niveau=-29`, `niveau=51-`).

1re et 2e génération : IV ramenés à l'échelle actuelle (× 2 + 1 ; Atq. Spé. et Déf. Spé. = « Spécial »), nature de leur
transfert vers les jeux suivants (expérience modulo 25), pas de talent. Ces caractéristiques sont enregistrées avec
chaque Pokémon (`Services/PokemonTraits.cs`) : la collection filtre en base, sans relire les fichiers.

### Mode compact de « Ma collection »

Interrupteur « Mode compact » (`?compact=1`, désactivé par défaut) : un dossier par **espèce**, toutes formes réunies,
image de la forme de base non chromatique, nombre de Pokémon en bas à droite ; une espèce à un seul Pokémon garde une
case normale ; tous les œufs dans un dossier « Œufs », toujours à la fin. Recherche, origine et pastilles s'appliquent
d'abord ; dossiers et contenu suivent le tri (placés comme leur premier Pokémon). Un dossier ouvert déroule ses Pokémon
dans les cases suivantes ; plusieurs à la fois, pas gardés dans l'adresse. Marche aussi chez un autre joueur. La liste
reste chargée au défilement : une requête regroupe par espèce (`BankService.GetGroupsAsync`), puis seules les cases
visibles sont lues (`CollectionFolderLayout`).

### Images des Pokémon

Partout, l'image suit la **forme**, la **femelle** quand elle diffère, le chromatique ; un Pokémon qui tient sa
**Méga-Gemme** (ou Kyogre / Groudon leur orbe) est montré méga-évolué (jamais Méga-Rayquaza, sans gemme). Une forme sans
image à elle prend celle de l'espèce (liste dans `PokemonSpritesTests`). Images Gigamax rassemblées mais pas encore
utilisées. Détails : `docs/claude/images.md`.

### Inventaire (Pokédollars et objets)

`Pages/Inventory.razor`, `Services/InventoryService.cs`, règles des sacs dans `Services/SaveBag.cs` ; détails :
`docs/claude/inventaire.md`.

- Page : porte-monnaie et « Stockage » des objets, un onglet par catégorie (`?onglet=baies`), source `?source=interne`
  (« Tout » additionne l'interne et les sauvegardes ; une sauvegarde est en lecture seule, les transferts passent par les
  fenêtres). Grille : colonnes automatiques mesurées par le navigateur (`.item-grid-probe`, `watchGridColumns` dans
  `wwwroot/js/app.js`) ou fixées au curseur ; vue, colonnes et tris retenus par le navigateur.
- **Argent** : « Retirer de l'argent » prend la somme dans la **copie stockée** et la crédite au porte-monnaie, en une
  transaction (copie avant/après). Pas encore de transfert vers une sauvegarde.
- **Objets** dans les deux sens (objets, Pokéballs, médicaments, baies, CT / DT), plusieurs catégories par opération ; sac
  et **PC d'objets** pour les jeux de 1re à 3e génération. Le journal liste les objets déplacés.
- Catégories : `SaveBag.ItemPouches` (objets ordinaires), `BallItems`, `MedicineItems`, `BerryItems`, CT et DT.
- **Jamais montrés ni touchés** (ne pas bloquer la progression) : CS (même quand toutes les CT sont retirées), objets
  rares, Lettres (`SaveBag.MailItems`), Cristaux Z, Baie Enigma de 3e génération, baies plantées, objets d'Or / Argent
  sans équivalent. Chaque jeu décide de la poche : un objet rare chez lui est refusé au dépôt. Rouge / Bleu (pas de poche
  « Objets rares ») : seuls les objets de la table de conversion passent.
- Un objet est repéré par son **numéro PKHeX de 4e génération et suivantes** ; la 2e et la 3e sont converties par PKHeX,
  Rouge / Bleu par une table du site ; baies d'Or / Argent / Cristal vers leur équivalent, Appât Ball ajoutée à la main,
  Noigrumes Bleue et Jaune remises dans l'ordre. Objet cherché dans toutes les poches.
- **CT et DT par capacité**, pas par numéro : au dépôt, celle qui apprend la capacité dans le jeu choisi ; aucune (ou une
  CS seulement) : refusé. Correspondances : listes PKHeX dans `SaveBag`.
- Pas de limite sur le site ; le jeu limite au dépôt (objet inconnu du jeu, maximum du sac : 999 en 4e et 5e, 99 de la 1re
  à la 3e et pour les CT de la 4e, 20 objets différents dans le sac de Rouge / Bleu, 50 dans le PC). CT réutilisable : 1
  par sac. **CT à usage unique** : à 80 exemplaires ou plus dans une sauvegarde (sac et PC), cette CT y est cachée et
  bloquée, un dépôt ne peut pas l'y amener (`SaveBag.LimitedMachineThreshold`).
- Après un retrait ou un dépôt, la sauvegarde est **modifiée sur le serveur** (voir « Sauvegardes stockées »).

### Parc

`/parc`, `Pages/Park.razor`, `Services/ParkService.cs`, affichage et animation `wwwroot/js/parc.js` (Phaser), cartes
faites dans [Tiled](https://www.mapeditor.org) avec le pack Cute Fantasy (voir « Crédits »). Tout le reste (cartes de
chaque paysage, générateurs, types de cases, comportement, tests) : `docs/claude/parc.md`.

- Pokémon de la collection, toutes générations, hors œufs. **20 places** (`ParkService.BaseCapacity`), **Agrandir** :
  25 000 ₽ pour 20 de plus, 3 fois (`ParkService.ExpansionPrice`, `ExpandAsync`). Taille et débit vérifiés dans les mises
  à jour elles-mêmes (deux achats simultanés ne dépassent ni la taille ni le solde) ; achat au journal. Un parc déjà
  au-dessus de sa capacité garde ses Pokémon, sans pouvoir en ajouter.
- **Un parc par paysage** (`ParkLandscape`, table `Parks`, index unique compte + paysage) : Forêt créée à la première
  visite, puis Plage, Volcan, Désert, Océan, Grotte, Montagne et Donjon à **25 000 ₽** (`ParkService.ParkPrice`,
  `BuyAsync` : `INSERT OR IGNORE` et débit dans la même transaction). Chaque parc a sa taille. Chaque méthode revérifie que
  le parc (numéro venu du navigateur) est à ce joueur. **Un Pokémon dans un seul parc** (table `ParkMembers`, suppression
  en cascade quand il quitte la collection). Nom, cartes et textes de chaque paysage : `ParkLandscapes`.
- Océan : seuls les nageurs (`ParkLandscapes.Refusal`, revérifié par `ParkService.SetAsync`). Espèces sans sprites
  SpriteCollab : grisées et refusées.
- **Bonheur** (`ParkHabitats.MoodOf`, d'après la feuille « Habitat » de l'utilisateur) : malheureux si un habitat qu'il
  n'aime pas couvre 50 % ou plus de la zone « Parc », sinon heureux si un habitat qu'il aime en couvre plus de 50 %, sinon
  normal ; un double type aime un habitat si l'un de ses types l'aime.
- La fiche n'est donnée que pour un Pokémon de ce parc (`ParkService.GetMemberAsync`). Positions et déplacements à la
  souris ne sont pas enregistrés (navigateur seulement). Visiteurs : rien qui modifie.

### Living dex

`/livingdex`, `Pages/LivingDexes.razor` et `Pages/LivingDexPage.razor`, table `LivingDexes` (`Data/LivingDex.cs`), jeux
et modes dans `LivingDexGames` (`Services/LivingDexService.cs`), global dans `Services/GlobalLivingDex.cs`. Modes et cases
jeu par jeu, préférences de l'utilisateur, images : `docs/claude/living-dex.md`.

- Jeux de la 1re à la 9e génération (pas Colosseum, XD, ni Légendes Z-A). **Un living dex par jeu et par compte**, une
  sauvegarde pour un seul living dex (index uniques) ; supprimer la sauvegarde supprime son living dex (cascade).
- PKHeX ne distingue pas Rouge de Bleu, Or d'Argent, Rubis de Saphir, Rouge Feu de Vert Feuille, ni Diamant de Perle :
  la sauvegarde est proposée pour les deux jeux. À partir de la 5e génération, chaque sauvegarde dit son jeu.
- **Comptage** : équipe et boîtes de la sauvegarde liée (pas les œufs), donc à jour à chaque mise à jour de la sauvegarde.
- **Modes** : Régional, National, Complet (chaque forme qu'une sauvegarde du jeu peut contenir, pas le sexe) ; en
  Régional et National n'importe quelle forme compte, en Complet la forme exacte. Un mode identique à un autre, ordre
  compris, est caché. Cases qui demandent un exemplaire de plus (Méga, Primo, Gigamax), un objet dans le sac (Shaymin
  Céleste, Hoopa Déchaîné jusqu'à la 6e, Méga de Let's Go) ou une forme fixée par le jeu (Deoxys de 3e génération).
- Vue générale : `?mode=regional|complet|ultime`, `?tri=completion`, `?ordre=desc` (`LivingDexGames.Sort`) ; pourcentage
  arrondi vers le bas (`LivingDexGames.Percent`). Page : `?filtre=manquants|possedes`, colonnes `?colonnes=12`
  (`Shared/GridColumns.cs`, commun avec « Ma collection »).
- **Global** (`LivingDexGame.Global`, `StoredSaveId` null) : stockage interne seulement, sans œufs ni chromatiques,
  chaque exemplaire pour une seule case ; formes reconnues par leur nom anglais chez PKHeX ; National 1 025, Complet
  1 283, Ultime 1 578. **Global chromatique** (`LivingDexGame.GlobalShiny`) : les chromatiques seulement, sans
  `GlobalLivingDex.ShinyLocked` (National 1 000, Complet 1 244, Ultime 1 508).
- Images : `outils/images.py livingdex` (sprites PokéAPI, jaquettes Bulbagarden Archives) ; sans elles, l'image de la
  collection (`LivingDexGames.Fallback`).

### Taille de la collection

Chaque compte a une limite (stockage interne + sauvegardes stockées, œufs compris) : 1 000 au départ. Toute opération qui
la dépasserait est refusée **en entier** avec un message qui chiffre le manque : dépôt dans le stockage interne (dès le
glisser-déposer, puis à la validation), ajout ou mise à jour d'une sauvegarde stockée. Ce qui réduit la collection n'est
jamais bloqué. « Agrandir » achète des lots de 50 emplacements pour 10 000 ₽ (journal de l'inventaire). Valeurs dans
`Services/CollectionCapacity.cs`.

## Organisation du code

| Fichier | Rôle |
|---|---|
| `MonsutaPotaru/Services/SaveSession.cs` | Sauvegarde chargée + dépôts/retraits en attente (PKHeX) |
| `MonsutaPotaru/Services/AdminService.cs` | Administration : rôles (donner, retirer, garde-fous), liste des comptes, code d'invitation (lecture, régénération), initialisation au démarrage (code, premier administrateur) |
| `MonsutaPotaru/Services/DebugService.cs` | Mode debug d'un administrateur : comptes actifs en mémoire (`DebugMode`, coupé à la déconnexion), argent ajouté (montants fixes, journal), parc agrandi ou réduit gratuitement |
| `MonsutaPotaru/Components/Pages/Admin.razor` | Page « Administration » : code d'invitation, comptes et rôles |
| `MonsutaPotaru/Services/PlayerService.cs` | Multijoueurs : recherche par pseudo, joueur consultable selon sa visibilité et le lien avec le visiteur, visibilité du profil |
| `MonsutaPotaru/Services/FriendService.cs` | Amis : code ami, demandes (par pseudo ou par code), acceptation, refus, annulation, retrait, listes, compteur du menu |
| `MonsutaPotaru/Components/Pages/Friends.razor` | « Multijoueurs » → « Amis » : onglets Amis (recherche, retrait) et Demandes (code ami, reçues, envoyées) |
| `MonsutaPotaru/Data/Friendship.cs` | Tables des amitiés (deux lignes par amitié) et des demandes d'ami |
| `MonsutaPotaru/Services/TradeService.cs` | Échanges : sauvegardes compatibles, Pokémon proposés, évolution par échange, écriture des deux sauvegardes, statistiques |
| `MonsutaPotaru/Services/TradeHub.cs` | Échanges en direct (singleton, en mémoire) : demande, choix, validation, annulation, expiration |
| `showdown/` | Simulateur des combats : `package.json` (`@pkmn/sim`, version fixée), `combat.js` (pont : Pokémon par numéros, PP Plus, choix seulement) |
| `MonsutaPotaru/Components/Pages/Battles.razor` | « Multijoueurs » → « Combats » : demandes, ami et sauvegardes, choix de l'équipe, combat en direct (interface façon Platine) |
| `MonsutaPotaru/Services/Battle/BattleHub.cs` | Combats en direct (singleton, en mémoire) : demande, équipes, simulateur, choix vérifiés, minuteur, abandon, fin |
| `MonsutaPotaru/Services/Battle/BattleService.cs` | Combats : sauvegardes compatibles entre amis, Pokémon proposés pour l'équipe, équipe vérifiée, statistiques |
| `MonsutaPotaru/Services/Battle/BattleSprites.cs` | Sprites d'un combat vus par un joueur : ceux de son jeu, de face et de dos, chromatiques, formes (Zarbi, Morphéo, Deoxys) |
| `MonsutaPotaru/Services/Battle/BattleRecorder.cs` | Résultat de chaque combat terminé écrit en base (statistiques) |
| `MonsutaPotaru/Data/BattleRecord.cs` | Table des combats terminés (vainqueur ou match nul) |
| `MonsutaPotaru/Services/Battle/ShowdownBattle.cs` | Un combat dans le simulateur : processus Node.js, envoi des choix, réception du protocole |
| `MonsutaPotaru/Services/Battle/BattleTeam.cs` | Pokémon d'une sauvegarde → Pokémon du simulateur (format par génération, forme de Deoxys) |
| `MonsutaPotaru/Services/Battle/BattleProtocol.cs` | Lecture du protocole de Showdown : lignes, parties secrètes de chaque joueur, requêtes |
| `MonsutaPotaru/Services/Battle/BattleView.cs` | Combat vu par un joueur : terrain, PV, statuts, météo, étapes à animer, messages en français |
| `MonsutaPotaru/Services/Battle/BattleNames.cs` | Noms anglais de Showdown → noms français de PKHeX (espèces, capacités, objets, talents, types) |
| `MonsutaPotaru/Components/Pages/Trades.razor` | « Multijoueurs » → « Échanges » : demandes, choix de l'ami et des sauvegardes, échange en direct |
| `MonsutaPotaru/Components/Shared/TradePokemon.razor` | Pokémon choisi dans un échange (image, nom, place) |
| `MonsutaPotaru/Components/Pages/Statistics.razor` | Statistiques du joueur (menu du pseudo) : échanges, combats |
| `MonsutaPotaru/Data/TradeRecord.cs` | Table des échanges réussis (statistiques, succès) |
| `MonsutaPotaru/Components/Pages/PlayerSearch.razor` | « Multijoueurs » → « Rechercher un joueur » (`/joueurs`) |
| `MonsutaPotaru/Components/Pages/PlayerProfilePage.razor` | Profil d'un joueur (`/joueurs/{pseudo}`) : ses pages publiques en onglets |
| `MonsutaPotaru/Components/Shared/PlayerHeader.razor`, `PlayerHidden.razor` | En-tête des pages d'un joueur (pseudo, onglets, « lecture seule ») ; réponse d'un profil privé ou introuvable |
| `MonsutaPotaru/Components/Account/Pages/Manage/Visibility.razor` | « Mon compte » → « Visibilité » : profil public ou privé, pages publiques |
| `MonsutaPotaru/Services/SiteThemes.cs` | Thèmes du site (clé, nom, description) et thème par défaut |
| `MonsutaPotaru/Components/Account/Pages/Manage/Themes.razor` | « Mon compte » → « Thèmes » : choix du thème, miniatures en CSS |
| `MonsutaPotaru/wwwroot/themes/pokedex.css` | Thème Pokédex, sous `[data-theme="pokedex"]` : menu, barre du haut, contenu de toutes les pages |
| `MonsutaPotaru/wwwroot/themes/ardoise.css` | Thème Ardoise : son cadre (classes `ardoise-…`, menu replié, téléphone) et le contenu des pages sous `[data-theme="ardoise"]` |
| `MonsutaPotaru/Components/Shared/PageHeader.razor` | En-tête de chaque page : titre, nombre, description, actions, icône (bandeau dans le thème Ardoise) |
| `MonsutaPotaru/Components/Shared/Block.razor` | Partie titrée d'une page, avec ses actions (panneau blanc dans le thème Ardoise) |
| `MonsutaPotaru/Components/Layout/ArdoiseLayout.razor` | Cadre du thème Ardoise (choisi par `MainLayout`) : menu repliable, barre du haut (fil d'Ariane, porte-monnaie, menu du compte) |
| `MonsutaPotaru/Components/Layout/SiteMenuBase.cs` | Commun aux menus (`NavMenu`, `ArdoiseLayout`) : adresse de la page, solde, demandes reçues en direct, rubriques ouvertes |
| `MonsutaPotaru/Components/Layout/Breadcrumbs.cs` | Fil d'Ariane du thème Ardoise, déduit de l'adresse de la page (`BreadcrumbsTests`) |
| `MonsutaPotaru/Data/SiteSetting.cs` | Table des réglages du site (code d'invitation) et rôles des comptes (`SiteRoles`) |
| `MonsutaPotaru/Services/AccountService.cs` | Comptes : règles du pseudo, inscription, connexion par l'adresse (blocage après 5 échecs), choix du pseudo des anciens comptes, changement d'adresse ; réglages d'Identity, messages d'Identity en français |
| `MonsutaPotaru/Services/AccountLimiter.cs` | Limites d'envois par adresse IP : connexion (10 par minute), inscription (10 par heure) |
| `MonsutaPotaru/Components/Account/Pages/` | Pages du compte : connexion, inscription, « Choisis ton pseudo », « Mon compte » (`Manage/` : profil, adresse, mot de passe, données personnelles) |
| `MonsutaPotaru/Components/Account/Shared/PseudoGuard.razor` | Renvoie un compte sans pseudo sur « Choisis ton pseudo » (autour du contenu de `MainLayout`) |
| `MonsutaPotaru/Services/BankService.cs` | Accès base, validation transactionnelle, copies de sécurité |
| `MonsutaPotaru/Services/CollectionFolders.cs` | Mode compact de « Ma collection » : dossiers par espèce (clé, nombre, place dans le tri) et cases de la grille quand des dossiers sont ouverts |
| `MonsutaPotaru/Services/PokemonFingerprint.cs` | Empreinte d'un Pokémon et recherche de ses doublons dans le compte (vieille copie d'une sauvegarde) |
| `MonsutaPotaru/Services/PokemonInfo.cs` | Noms français (dont « Rouge / Bleu », « Or / Argent », « Diamant / Perle », « Or HeartGold / Argent SoulSilver » pour les groupes de jeux : une sauvegarde de ces jeux ne dit pas lequel des deux ; migrations `GameNames` et `GameGroupNames` pour les noms déjà enregistrés), sprites, légalité |
| `MonsutaPotaru/Components/Pages/Bank.razor` | Page « Transfert et stockage interne » (`/transfert`) : boîtes, glisser-déposer, sélection multiple |
| `MonsutaPotaru/Components/Pages/Park.razor` | Page « Parc » : carte où vivent de 20 à 80 Pokémon, liste déroulante des parcs, « Agrandir le parc » et « Acheter un parc » (confirmation, prix et solde), fenêtre de choix (« Seulement ceux du parc », « Tout retirer », Pokémon d'un autre parc), « Vider le parc », fiche au clic |
| `MonsutaPotaru/SecurityHeaders.cs` | En-têtes de sécurité de toutes les réponses : nosniff, Referrer-Policy, CSP stricte (plus aucun script écrit dans la page) |
| `MonsutaPotaru/wwwroot/js/app.js` | Scripts du site (chargés dans le `<head>`) : images de remplacement (`data-remplacement`), téléchargement, largeur d'écran, glisser-déposer, menu sur téléphone, cadre du thème Ardoise (repli du menu, rubriques, menu du compte) |
| `MonsutaPotaru/wwwroot/js/parc.js` | Parc avec Phaser : carte Tiled, brouillard hors de la zone « Départ », vue qu'on fait glisser ; animation et comportement des Pokémon (énergie, intentions, famille, nuit, curiosité, chemins autour des obstacles, nage des types Eau et survol des types Vol, sommets de la montagne) ; pénombre des grottes ; `simulate` et `view` pour les essais |
| `MonsutaPotaru/wwwroot/lib/phaser/` | Phaser 3.90.0 (`phaser.esm.min.js`, module ES) et sa licence MIT |
| `MonsutaPotaru/wwwroot/parc/plage-1-1.json` … `plage-1-4.json` | Cartes de la plage, une par taille, même format |
| `MonsutaPotaru/wwwroot/parc/volcan-1-1.json` … `volcan-1-4.json` | Cartes du volcan, une par taille, même format (plus le calque « Bulles » et les tuiles animées) |
| `MonsutaPotaru/wwwroot/parc/desert-1-1.json` … `desert-1-4.json` | Cartes du désert, une par taille, même format (cascade et touffes d'herbe animées) |
| `MonsutaPotaru/wwwroot/parc/ocean-1-1.json` … `ocean-1-4.json` | Cartes de l'océan, une par taille, même format (rochers animés) |
| `MonsutaPotaru/wwwroot/parc/donjon-1-1.json` … `donjon-1-4.json` | Cartes du donjon, une par taille, même format (plus la couleur de fond, le calque d'objets « Lumières », les torches et les pics animés) |
| `MonsutaPotaru/wwwroot/parc/montagne-1-1.json` … `montagne-1-4.json` | Cartes de la montagne, une par taille, même format (plus les calques « Sommets » et « Survol », lus par `parc.js` pour les volants) |
| `MonsutaPotaru/wwwroot/parc/grotte-1-1.json` … `grotte-1-4.json` | Cartes de la grotte, une par taille, même format (plus la couleur de fond, le calque d'objets « Lumières » et les torches animées) |
| `MonsutaPotaru/wwwroot/parc/parc-1-1.json` … `parc-1-4.json` | Cartes de la forêt, une par taille (export de Tiled par `outils/carte-parc.py` : numéros de tuiles, calques, zones « Départ » et « Parc », types de cases) |
| `MonsutaPotaru/Services/ParkHabitats.cs` | Types de cases du parc : feuille « Habitat » (ce que chaque type de Pokémon aime ou non), types de chaque carte et leur nombre dans la zone « Parc » (lus au démarrage), humeur d'un Pokémon (`MoodOf`), façon de se déplacer (`MovementOf` : nage, vol ; à la montagne, tout type Vol vole) |
| `MonsutaPotaru/Services/ParkService.cs` | Parcs d'un joueur (la Forêt créée à la première visite, achat d'un autre paysage, agrandissements) et leurs Pokémon : liste, choix (20 à 80, avec des sprites Donjon Mystère, un parc par Pokémon), retrait, vidage, famille d'évolution |
| `MonsutaPotaru/Services/ParkLandscapes.cs` | Paysages des parcs (Forêt, Plage, Volcan, Désert, Océan, Grotte, Montagne, Donjon) : nom, carte, pictogramme, textes de la page, et qui ne peut pas y vivre (`Refusal` : l'océan, seulement les nageurs ; `ChooserNote` : la remarque de la fenêtre de choix, à l'océan et à la montagne) |
| `MonsutaPotaru/Data/Park.cs` | Table des parcs (un par paysage et par compte, avec ses agrandissements) et énumération `ParkLandscape` |
| `MonsutaPotaru/Data/ParkMember.cs` | Table des Pokémon au parc (leur parc ; supprimée avec le Pokémon ou le parc) |
| `MonsutaPotaru/Components/Pages/LivingDexes.razor` | Page « Living dex » : cartes des living dex du compte (progression dans le mode choisi), création (jeu, générations repliables, puis sauvegarde) |
| `MonsutaPotaru/Components/Shared/LivingDexProgress.razor` | Barre de progression d'un living dex, pourcentage au centre |
| `MonsutaPotaru/Components/Shared/LivingDexCover.razor` | Image d'un living dex : jaquette du jeu, ou Master Ball / Charme Chroma pour les globaux |
| `MonsutaPotaru/Components/Pages/LivingDexPage.razor` | Page d'un living dex : grille des sprites du jeu (silhouettes des manquants), progression, fiche au clic |
| `MonsutaPotaru/Services/LivingDexService.cs` | Living dex : jeux (`LivingDexGames` : jaquette, sprites, Pokémon de chaque mode, modes cachés), comptage dans la sauvegarde, création, suppression |
| `MonsutaPotaru/Data/LivingDex.cs` | Table des living dex (un par jeu et par compte, supprimé avec sa sauvegarde ; le global n'a pas de sauvegarde) |
| `MonsutaPotaru/Services/GlobalLivingDex.cs` | Living dex global : cases des modes National, Complet et Ultime, attribution des exemplaires du stockage interne |
| `MonsutaPotaru/Components/Pages/Collection.razor` | Toute la collection sur une page (chargée au fil du défilement), filtres en pastilles (`Shared/CollectionFilterChips.razor`), filtre par origine, tri par arrivée (les plus récents d'abord, par défaut), par nom d'espèce ou par numéro de Pokédex (croissants), inversable, gardé dans l'adresse (`?tri=nom&ordre=desc`) ; les œufs toujours à la fin (leur espèce reste cachée), à égalité les plus récents d'abord ; le tri par nom ignore les accents (colonne `SortName`, remplie au démarrage pour les Pokémon plus anciens) ; 5 / 10 / 15 / 20 colonnes par défaut selon la largeur de la fenêtre en pixels physiques (téléphone, jusqu'à 1920, jusqu'à 2560, au-delà ; mise à l'échelle de Windows comprise), recalculées au redimensionnement, modifiables au curseur ; mode compact (un dossier par espèce, `?compact=1`) |
| `MonsutaPotaru/Components/Pages/Saves.razor` | Gestion des sauvegardes stockées (ajout, mise à jour, suppression) |
| `MonsutaPotaru/Components/Pages/Sync3ds.razor` | « Synchronisation 3DS » : consoles reliées (code de liaison, attente de la console, retrait) |
| `MonsutaPotaru/Services/SyncService.cs` | Synchronisation 3DS : codes de liaison, jetons, envoi d'une sauvegarde (ajout, mise à jour ou « déjà à jour »), emplacements sur la console, liste, comparaison et téléchargement pour « Récupérer » |
| `MonsutaPotaru/Api/SyncEndpoints.cs` | API du homebrew PokeBank Sync (`/api/sync/...`) et limite d'essais de codes |
| `MonsutaPotaru/Services/StoredSaveService.cs` | Stockage, mise à jour et suppression des sauvegardes stockées |
| `MonsutaPotaru/Services/SaveIdentity.cs` | Empreinte d'une sauvegarde (unicité sur le serveur) |
| `MonsutaPotaru/Services/SaveVersion.cs` | Rouge Feu ou Vert Feuille : jeu d'une sauvegarde deviné d'après le jeu d'origine des Pokémon du dresseur |
| `MonsutaPotaru/Services/InventoryService.cs` | Inventaire : retrait de Pokédollars, retrait et dépôt d'objets (sauvegardes stockées), achat d'emplacements, journal |
| `MonsutaPotaru/Services/SaveBag.cs` | Objets du sac et du PC d'une sauvegarde (objets, Pokéballs, médicaments, baies, CT/DT par capacité ; jamais les objets rares, Lettres ni CS) : lecture, retrait, dépôt (PKHeX), conversion des numéros d'objets des jeux de 1re à 3e génération |
| `MonsutaPotaru/Services/CollectionCapacity.cs` | Taille maximale de la collection : règles, occupation, messages |
| `MonsutaPotaru/Components/Pages/Inventory.razor` | Page « Inventaire » (solde, stockage en onglets avec recherche, tri par onglet, liste/grille (colonnes de la grille au curseur) et choix de la source — interne, tout, une sauvegarde —, journal et détail d'un objet en panneau latéral, fenêtre de retrait d'argent) |
| `MonsutaPotaru/Components/Shared/ItemTransferDialog.razor` | Fenêtre de retrait / dépôt d'objets (toutes catégories en une opération ; onglets Sac / PC pour les jeux de 1re à 3e génération) |
| `MonsutaPotaru/Components/Shared/ItemSprite.razor` | Image d'un objet ; celle de l'objet inconnu quand le site n'en a pas |
| `MonsutaPotaru/Services/PokemonCriteria.cs` | Filtres de « Ma collection » : valeurs choisies, requête en base, lecture et écriture dans l'adresse |
| `MonsutaPotaru/Services/PokemonTraits.cs` | Caractéristiques d'un Pokémon enregistrées pour ces filtres (types récents, nature, talent, IV, forme, Méga) |
| `MonsutaPotaru/Services/PokemonSprites.cs` | Choix de l'image d'un Pokémon : forme (noms de PKHeX → noms de PokéAPI), Méga-Évolution selon l'objet tenu, femelle, chromatique |
| `MonsutaPotaru/wwwroot/images/` | Hors du dépôt Git (voir « Lancer en local ») ; images servies par le site : `pokemon/<numéro d'espèce>.png`, `pokemon/<numéro>-<forme>.png` (`19-alola.png`, `6-mega-x.png`, `6-gmax.png`), `pokemon/femelle/`, `pokemon/chromatique/` (mêmes sous-dossiers), `objets/<numéro d'objet>.png`, `objets/ct-<type>.png` pour les CT/DT, un `inconnu.png` dans chaque dossier |
| `MonsutaPotaru/wwwroot/images/parc/tuiles/` | Planches du pack Cute Fantasy utilisées par la carte du parc (hors Git : copiées par `outils/carte-parc.py`, envoyées par `deploy/fond-parc.cmd`) |
| `outils/generer-parc.py` | Génère les cartes du parc 1 (`Parc_1-1.tmx` à `Parc_1-4.tmx`, pour Tiled) : herbe, chemins et eau à bordures automatiques, forêt, arbres, bancs, rochers, fleurs et collisions, avec des aperçus en PNG (Python avec Pillow) |
| `outils/generer-plage.py` | Génère les cartes de la plage (`Plage_1-1.tmx` à `Plage_1-4.tmx`) : côte, îlots, ponton, palmiers, récif, environ 65 % de mer (la moitié à la taille 1) |
| `outils/generer-volcan.py` | Génère les cartes du volcan (`Volcan_1-1.tmx` à `Volcan_1-4.tmx`) : cascades de lave animées, coulées, mares, pont, bulles, champ de rochers autour, 15 à 25 % de lave |
| `outils/generer-desert.py` | Génère les cartes du désert (`Desert_1-1.tmx` à `Desert_1-4.tmx`) : dunes, plateau et escaliers, lac, cascade animée, oasis, prairies, acacias, cactus, champ de cactus et de rochers autour, ~70 % de sable |
| `outils/generer-ocean.py` | Génère les cartes de l'océan (`Ocean_1-1.tmx` à `Ocean_1-4.tmx`) : pleine mer, trois îlots de sable (aucun dans la vue de départ), rochers animés, récif autour |
| `outils/generer-donjon.py` | Génère les cartes du donjon abandonné (`Donjon_1-1.tmx` à `Donjon_1-4.tmx`) : salles et couloirs par taille, murs, bassins d'égout et ponts, piliers, tombes, objets, torches, candélabres et leurs lumières |
| `outils/generer-montagne.py` | Génère les cartes de la montagne (`Montagne_1-1.tmx` à `Montagne_1-4.tmx`) : vallée, rivière et pont, massifs à flancs de pierre sur deux étages, sapins, rochers, nids, calques « Sommets » et « Survol » |
| `outils/generer-grotte.py` | Génère les cartes de la grotte (`Grotte_1-1.tmx` à `Grotte_1-4.tmx`) : caverne, piliers de roche, lacs souterrains, cristaux, stalagmites, torches et leurs lumières |
| `outils/parc_carte.py` | Moteur commun des générateurs (forêt, plage, volcan, désert, océan, grotte, montagne, donjon) : jeux de tuiles, calques, formes, bordures automatiques, objets, poches enfermées, écriture des `.tmx` (couleur de fond, calque « Lumières ») et aperçus |
| `outils/carte-parc.py` | Prépare une carte de Tiled pour le parc : vérifications, types de cases (`habitats`), carte dans `wwwroot/parc/`, planches dans `images/parc/tuiles/` (Python seul ; un `.tmx` est exporté par Tiled en ligne de commande) |
| `deploy/fond-parc.cmd` (`fond-parc.ps1`) | Envoie les planches du parc dans les images du serveur, par SSH |
| `MonsutaPotaru.Tests/ParkMapTests.cs` | Carte du parc : ce que `parc.js` lit, zone « Départ » dans la carte, chaque case libre accessible |
| `MonsutaPotaru.Tests/ParkHabitatsTests.cs` | Types de cases : feuille « Habitat » recopiée, un type par case de chaque carte, d'après ses tuiles ; humeur (seuils, double type, cartes actuelles), choix du portrait |
| `MonsutaPotaru.Tests/PageTestContext.cs` | Base des tests de pages (bUnit) : vrais services sur une base temporaire, compte connecté, JavaScript simulé, aides (sauvegarde stockée, Pokémon au stockage interne, argent, amis, administrateur, page rendue pour un autre compte) |
| `MonsutaPotaru.Tests/PageTestExtensions.cs` | Aides des tests de pages : bouton par son texte, clic, texte affiché, attente d'un texte |
| `MonsutaPotaru.Tests/*PageTests.cs`, `AccountPagesTests.cs`, `LivingDexPagesTests.cs` | Pages et composants rendus par bUnit : Inventaire, Transfert, Parc, Combats, Échanges, Ma collection (filtres), Living dex, Amis, Mes sauvegardes, 3DS, Administration, joueurs |
| `MonsutaPotaru.Tests/SiteTests.cs` | Site entier (`SiteFactory`, WebApplicationFactory) : inscription, connexion, pages du compte, rendu serveur de chaque page, thèmes, API de la 3DS, environnement de production |
| `MonsutaPotaru/wwwroot/images/donjon/` | Sprites animés de SpriteCollab (Idle, Walk, Sleep, Charge, Attack, Hurt) dans les dossiers de SpriteCollab (`0025/`, `0025/0000/0001/` chromatique...) : planches `<Animation>-Anim.png` et `AnimData.xml` réduit à ces animations ; portraits Normal, Happy, Sad dans `portraits/<même dossier>/` ; `credits.txt` (auteurs) |
| `outils/recuperer-images.cmd` | Copie les images du serveur dans `wwwroot/images` par SSH (après un clonage) |
| `deploy/images-serveur.cmd` (`images-serveur.ps1`) | Remplit ou met à jour les images du serveur (`~/monsuta-potaru-images`) et redémarre le site |
| `outils/images.py` | Rassemble ces images (PokéAPI ; pour les objets, puis pokesprite et PKHeX par numéro d'objet) ; à relancer pour en ajouter |
| `MonsutaPotaru/Data/BankPokemon.cs` | Table des Pokémon (octets bruts + métadonnées ; `StoredSaveId` si lecture seule) |
| `MonsutaPotaru/Data/StoredSave.cs` | Table des sauvegardes stockées |
| `MonsutaPotaru/Data/SyncDevice.cs` | Tables des consoles 3DS reliées (empreinte du jeton), des emplacements des sauvegardes sur chaque console et des codes de liaison |
| `MonsutaPotaru/Data/Wallet.cs` | Tables du porte-monnaie et du journal des opérations |
| `MonsutaPotaru/Data/OwnedItem.cs` | Table des objets de l'inventaire (objets, Pokéballs, médicaments, baies ; CT/DT sous une clé de capacité, `SaveBag.MachineKey`) |

Ajouter une migration après modification du modèle :

```bash
dotnet tool restore
dotnet ef migrations add NomDeLaMigration --project MonsutaPotaru
```

## Mise en ligne sur un serveur

Sur un VPS Ubuntu / Debian, avec Docker : l'application tourne derrière **Caddy**, qui reçoit les visiteurs et gère
seul le certificat HTTPS (Let's Encrypt). Il faut un nom de domaine dont l'enregistrement DNS (type A) pointe vers
l'IP du VPS.

| Fichier | Rôle |
|---|---|
| `Dockerfile` | Image de l'application |
| `docker-compose.yml` | Application + Caddy, volume de données `monsuta-potaru_monsuta-potaru-data` |
| `Caddyfile` | Réglages de Caddy (le domaine vient de `.env`) |
| `.env.example` | Modèle des réglages : `DOMAIN`, `INVITE_CODE` (code de départ) et `ADMIN_PSEUDO` (premier administrateur) ; le vrai `.env` est créé sur le serveur |
| `deploy/publier.cmd` (`publier.ps1`) | Depuis Windows : envoie le code sur le VPS et le met à jour |
| `deploy/installer-serveur.sh` | Sur le VPS, une seule fois : pare-feu, Docker, `.env`, sauvegarde nocturne, démarrage |
| `deploy/sauvegarder.sh` | Sauvegarde du volume de données (lancée chaque nuit par cron, gardée 14 jours) |
| `deploy/renommer-serveur.cmd` (`.ps1`, `.sh`) | Une seule fois : passage du serveur de « PokeBank » à « Monsuta Potaru » (voir plus bas) |
| `.github/workflows/ci.yml` | GitHub Actions, CI : tests à chaque push, et analyse SonarCloud (voir « CI/CD ») |
| `.github/workflows/cd.yml` | GitHub Actions, CD : déploiement par un bouton, après lecture du résultat de la CI (voir « CI/CD ») |

**Première installation**

1. Depuis Windows, dans le dossier du projet : `deploy\publier.cmd ubuntu@IP_DU_VPS`
2. Sur le VPS (`ssh ubuntu@IP_DU_VPS`) : `bash ~/monsuta-potaru/deploy/installer-serveur.sh`
   (demande le nom de domaine et le pseudo de ton compte, futur administrateur ; génère le code d'invitation de départ et
   l'affiche à la fin). Inscris-toi ensuite avec ce pseudo : le compte devient administrateur.
3. Depuis Windows : `deploy\images-serveur.cmd ubuntu@IP_DU_VPS` (images, voir plus bas), puis
   `deploy\fond-parc.cmd ubuntu@IP_DU_VPS` (planches du parc, copiées en local depuis le pack).

**Mises à jour** : par GitHub (« CI/CD » ci-dessous), ou `deploy\publier.cmd ubuntu@IP_DU_VPS` depuis Windows. Les
migrations de la base s'appliquent au démarrage ; les données restent dans le volume Docker.

**Serveur installé avant les rôles** (une seule fois) : ajouter `ADMIN_PSEUDO=<ton pseudo>` au `.env` du serveur
(`~/monsuta-potaru/.env`) avant le déploiement. Si ton compte n'a pas encore choisi son pseudo, il devient administrateur
quand il le choisit.

**Changement de nom du serveur** (une seule fois, pour un serveur installé sous le nom « PokeBank ») : Docker préfixe ses
volumes du nom du projet, le nouveau nom repartirait donc d'une base vide. Après avoir poussé le code renommé :

1. `deploy\renommer-serveur.cmd ubuntu@IP_DU_VPS` : sauvegarde (comme chaque nuit), arrêt de l'ancien site, **copie**
   des volumes `pokebank_*` vers `monsuta-potaru_*` (base, copies de sécurité, clés de connexion, certificats HTTPS ;
   nombre de fichiers vérifié), `~/pokebank-images` déplacé en `~/monsuta-potaru-images`, `.env` copié dans
   `~/monsuta-potaru`, sauvegarde nocturne repointée. Relancé, il ne refait rien. Le site est arrêté à partir d'ici.
2. « Run workflow » : envoie le code dans `~/monsuta-potaru` et redémarre le site (sa sauvegarde du début est sautée
   cette fois-là : le dossier n'a encore que le `.env`).

`~/pokebank` et les volumes `pokebank_*` restent pour revenir en arrière ; le script affiche à la fin les commandes de
retour et de ménage (à faire une fois le site vérifié). Les anciennes archives `pokebank-*.tgz` de `~/sauvegardes`
partent au bout de 14 jours, comme les nouvelles.

**CI/CD** (GitHub Actions, deux workflows séparés : `.github/workflows/ci.yml` et `cd.yml`, l'un se lance sans l'autre) :

- **CI, tests** à chaque push (ou « Run workflow » du workflow « CI » pour les relancer), sur une machine Ubuntu propre (un fichier oublié dans un commit s'y voit) : .NET 10, simulateur de combat (`npm ci --ignore-scripts --prefix showdown`), images
  copiées depuis `~/monsuta-potaru-images` du VPS par SSH (exigées par les tests d'images), puis `dotnet test`. Résultat dans
  l'onglet « Actions » du dépôt et à côté de chaque commit. Attention : Linux distingue majuscules et minuscules dans les
  noms de fichiers, Windows non.
- **Qualité du code** : dans le même job, [SonarCloud](https://sonarcloud.io) (organisation `ghalhyus`, projet
  `Monsuta-Potaru`, clé `ghalhyus_monsuta-potaru`) analyse le code pendant la compilation des tests (`dotnet-sonarscanner` : début avant les tests,
  envoi après), avec la **couverture** des tests (coverlet, format OpenCover). Exclus : `Data/Migrations` (généré),
  `wwwroot/lib` (Bootstrap) et `wwwroot/images` (copiées du VPS) ; pas de couverture comptée pour `outils`, `deploy` et
  `wwwroot`. **Pour information seulement** : un échec de l'analyse (jeton absent, SonarCloud injoignable, Quality Gate
  non passée) est signalé en avertissement, la CI reste verte si les tests passent, et le déploiement n'en dépend pas.
  Dans SonarCloud, l'**analyse automatique** doit rester désactivée (Administration → Analysis Method) : sinon l'envoi
  de la CI est refusé.
- **CD, déploiement** : onglet « Actions » → « CD » → « Run workflow » sur `main`. Il **ne relance pas les tests** : il lit
  le résultat de la CI déjà passée sur ce commit (API de GitHub, quelques secondes) et refuse de déployer si elle a
  échoué, n'a pas fini ou n'a jamais tourné, avec le motif en rouge ; la case **« Forcer »** du bouton déploie quand
  même (avertissement orange). Puis :
  sauvegarde des données (`sauvegarder.sh`, quelques secondes de coupure), envoi de la même archive que `publier.ps1`,
  **images manquantes** (`outils/images.py --sortie ~/monsuta-potaru-images`, comme `images-serveur` : les présentes sont gardées ;
  un échec ne bloque pas, avertissement orange et journal complet dans `~/monsuta-potaru-images.log` sur le VPS),
  `docker compose up -d --build`, nettoyage (anciennes images, cache de construction de plus de 3 jours : `docker builder prune`,
  comme `publier.ps1` ; sans lui, il atteignait 20 Go), et vérification que le site répond, script de Blazor compris (`_framework/blazor.web.js` ; une minute au plus, sinon échec). Le dépôt est
  public, ses journaux d'Actions aussi : ils n'affichent ni secret ni journal du site (à lire par SSH). Jamais deux déploiements à la fois. La première installation se fait toujours avec `publier.cmd`.

Mise en place (une fois), avec une clé SSH réservée à GitHub (révocable sans toucher à la tienne), dans PowerShell :

1. Créer la clé, sans phrase de passe (Entrée deux fois) :
   `ssh-keygen -t ed25519 -C github-actions-monsuta-potaru -f "$HOME\.ssh\monsuta-potaru-github"`
2. L'autoriser sur le VPS :
   `Get-Content "$HOME\.ssh\monsuta-potaru-github.pub" | ssh ubuntu@IP_DU_VPS "cat >> ~/.ssh/authorized_keys"`
3. Lire l'empreinte du VPS que ton PC connaît déjà (lignes sans `#`) : `ssh-keygen -F IP_DU_VPS`
4. Sur GitHub, « Settings » → « Secrets and variables » → « Actions » → « New repository secret » :
   `VPS_HOST` (IP du VPS), `VPS_USER` (`ubuntu`, `debian`...), `VPS_SSH_KEY` (tout le contenu de
   `$HOME\.ssh\monsuta-potaru-github`, la clé **privée**, lignes `-----BEGIN/END` comprises), `VPS_KNOWN_HOSTS` (les lignes
   de l'étape 3).
5. Pour SonarCloud : `SONAR_TOKEN` (jeton créé dans SonarCloud, « My Account » → « Security »).

Pour retirer l'accès de GitHub : effacer la ligne `github-actions-monsuta-potaru` de `~/.ssh/authorized_keys` sur le VPS
(`github-actions-pokebank` pour la clé créée avant le changement de nom : elle reste valable, rien à refaire).

**Images** : ni dans le dépôt ni dans la publication. Elles sont stockées sur le VPS dans `~/monsuta-potaru-images` (hors de la
sauvegarde nocturne : elles se retéléchargent), monté en lecture seule à la place de `wwwroot/images` (`docker-compose.yml`)
et servi depuis le disque (`Program.cs` : `MapStaticAssets` ne connaît que les fichiers présents à la compilation).
`deploy\images-serveur.cmd ubuntu@IP_DU_VPS` lance `outils/images.py --sortie ~/monsuta-potaru-images` sur le VPS (téléchargement
depuis les projets d'origine, ~10 minutes la première fois, les images présentes sont gardées), puis redémarre le site,
qui lit la liste des images au démarrage. À relancer pour mettre les images à jour. Les développeurs les copient depuis le
VPS avec `outils\recuperer-images.cmd` (voir « Lancer en local »). Seule exception : les planches du parc, qui ne se
téléchargent nulle part (pack acheté, `outils/carte-parc.py`, voir « Parc ») ; `deploy\fond-parc.cmd ubuntu@IP_DU_VPS` les
envoie depuis le PC.

**Sauvegardes** : la sauvegarde de l'hébergeur ne couvre souvent que 24 h, or la base est le seul endroit où sont les
Pokémon déposés. `deploy/sauvegarder.sh` archive chaque nuit le volume dans `~/sauvegardes` sur le VPS ; en garder aussi
une copie ailleurs, par exemple : `scp -r ubuntu@IP_DU_VPS:~/sauvegardes C:\Sauvegardes\Monsuta Potaru`.
Pour restaurer une archive : `docker compose stop monsuta-potaru`, puis
`docker run --rm -v monsuta-potaru_monsuta-potaru-data:/data -v ~/sauvegardes:/backup alpine sh -c "rm -rf /data/* && tar xzf /backup/ARCHIVE.tgz -C /data"`,
puis `docker compose start monsuta-potaru`.

Réglages avancés (variables d'environnement, jamais de secret dans `appsettings.json`) : `Bank__InviteCode`,
`ConnectionStrings__DefaultConnection`, `Bank__BackupPath`, `Bank__KeysPath` (clés de connexion ; par défaut dans
`Data/keys`, donc dans le volume).

## Points de vigilance

- Licence : PKHeX est sous GPL-3.0 ; si tu publies ce code, il doit l'être sous GPL aussi (voir « Crédits »).
- Les images des Pokémon et des objets sont servies par le site (`wwwroot/images`, stockées sur le serveur, voir « Images » de
  la mise en ligne) : le navigateur ne charge plus rien depuis GitHub. Les formes, femelles et Méga-Évolutions sont décrites plus haut (« Images des Pokémon »). Pour
  les objets, PokéAPI les nomme d'après le nom anglais, qui ne correspond pas toujours (pokesprite et PKHeX complètent, par numéro).
  Depuis le 17/10/2026, PokéAPI a aussi les objets d'Épée/Bouclier et suivants (bonbons de Let's Go, ingrédients...) : il
  ne reste sans image (~330) que des objets rares, Cristaux Z en double, objets de Légendes Arceus et accessoires de
  pique-nique, qui affichent l'objet inconnu en attendant qu'on dépose leur image sous leur numéro d'objet.
- Usage privé uniquement.
- Sur Noir 2 / Blanc 2, PKHeX accepte les Pokémon de génération 5 ou antérieure ; un Pokémon de génération 6+
  déposé depuis un autre jeu restera dans le stockage interne.

## Crédits

Monsuta Potaru s'appuie sur le travail d'autres projets :

| Projet | Utilisé pour | Licence |
|---|---|---|
| [Pokémon Showdown](https://github.com/smogon/pokemon-showdown) (Guangcong Luo et contributeurs), paquet [`@pkmn/sim`](https://github.com/pkmn/ps) | Simulateur des combats (`showdown/combat.js`) : règles, capacités, talents, objets de chaque génération | MIT |
| [PKHeX](https://github.com/kwsch/PKHeX) (Kaphotics et contributeurs), paquet `PKHeX.Core` | Lecture et écriture des sauvegardes, Pokémon, sacs, noms français, légalité ; images d'objets « Big Items » et « Artwork Items » (`outils/images.py`) | GPL-3.0 |
| [PokéAPI sprites](https://github.com/PokeAPI/sprites) | Images des Pokémon (normales et chromatiques, formes, femelles, Méga-Évolutions, Gigamax), de la plupart des objets et des CT (`wwwroot/images`) ; sprites de Rouge / Bleu, Jaune, Or, Argent, Cristal, Rubis / Saphir, Émeraude, Rouge Feu / Vert Feuille, Diamant / Perle, Platine, HeartGold / SoulSilver et Noire / Blanche des living dex, formes comprises (`versions/generation-i` à `generation-v`, `wwwroot/images/livingdex`) ; sprites de dos et chromatiques de Rubis / Saphir, Émeraude et Rouge Feu / Vert Feuille des combats (`back/`, `shiny/`) | CC0 1.0 pour le dépôt ; images © The Pokémon Company |
| [PokéAPI](https://github.com/PokeAPI/pokeapi) (données) | Liste des formes de chaque espèce et des espèces dont la femelle est différente (tables `pokemon`, `pokemon_forms`, `pokemon_species`, lues par `outils/images.py` ; `has_gender_differences` reprise dans `GlobalLivingDex.GenderDifferences`) ; ordre des Pokédex régionaux des living dex (`pokemon_dex_numbers`, Pokédex « original-johto », « hoenn », « original-sinnoh », « extended-sinnoh », « updated-johto », « original-unova », « updated-unova », « kalos-central », « kalos-coastal », « kalos-mountain », « updated-hoenn », « original-alola », « updated-alola », « letsgo-kanto », « galar », « isle-of-armor », « crown-tundra », « hisui », « paldea », « kitakami » et « blueberry », repris dans `LivingDexGames`) | BSD-3-Clause |
| [PokéSprite](https://github.com/msikma/pokesprite) (msikma) | Images d'objets absentes de PokéAPI, retrouvées par numéro d'objet (`data/item-map.json`) | MIT ; images © Nintendo / Creatures Inc. / GAME FREAK Inc. |
| [SpriteCollab](https://github.com/PMDCollab/SpriteCollab) (PMD Collab, [sprites.pmdcollab.org](https://sprites.pmdcollab.org)) | Sprites animés façon Donjon Mystère du « Parc », animations Idle, Walk, Sleep, Charge, Attack et Hurt, et portraits Normal, Happy et Sad (humeur dans la fiche d'un Pokémon du parc) (`wwwroot/images/donjon`, `donjon/portraits`, par `outils/images.py donjon`) ; auteurs de chaque sprite et portrait dans `wwwroot/images/donjon/credits.txt` | CC BY-NC 4.0 (usage non commercial, auteurs à citer) |
| [Cute Fantasy RPG](https://kenmi-art.itch.io/cute-fantasy-rpg) (Kenmi), version premium, et ses extensions | Tuiles des cartes du « Parc » (herbe, chemins, eau, arbres, décors ; plage et palmiers de l'extension Desert, et tout le désert (sable, falaises, eau, cascade, herbe, cactus, rochers, acacias, plantes) ; îlots et rochers animés de l'océan ; sol, parois, eau, cristaux, stalagmites et torches de la grotte ; herbes, hauteurs à flancs de pierre, rivière, pont de bois, sapins et nids de la montagne ; murs, dallage, bassins d'égout, piliers, pics, objets, or, coffres et petit pont de l'extension Dungeons ; roche, lave, cascades, pont, rochers et plantes de l'extension Volcano ; `wwwroot/images/parc/tuiles`, hors Git, copiées par `outils/carte-parc.py`) | Licence du pack (acheté) : usage dans tout projet, même commercial, modifications permises ; redistribution ou revente interdite, même modifié (ni le pack ni ses planches dans le dépôt) |
| [Phaser](https://phaser.io) 3.90.0 (Richard Davey, Phaser Studio) | Moteur de jeu 2D du « Parc » : carte en tuiles, caméra, dessin (`wwwroot/lib/phaser`) | MIT |
| [Tiled](https://www.mapeditor.org) (Thorbjørn Lindeijer) | Éditeur de la carte du « Parc » (format lu par Phaser ; outil seulement, rien de Tiled dans le site) | GPL-2.0 (l'éditeur ; les cartes faites avec appartiennent à leur auteur) |
| [Bootstrap](https://getbootstrap.com) | Mise en page et composants de l'interface (`wwwroot/lib/bootstrap`) | MIT |
| [Bootstrap Icons](https://icons.getbootstrap.com) | Icônes des menus (dessins SVG dans `wwwroot/app.css`, « Icônes des menus ») et des boutons du « Parc » (`ICONS` dans `wwwroot/js/parc.js` : dash-lg, plus-lg, fullscreen, fullscreen-exit) | MIT |
| [ASP.NET Core, Blazor, Identity, Entity Framework Core](https://dotnet.microsoft.com) (Microsoft) | Site, comptes, accès à la base | MIT |
| [SQLite](https://sqlite.org) | Base de données | Domaine public |
| [xUnit](https://xunit.net), [coverlet](https://github.com/coverlet-coverage/coverlet) | Tests, couverture des tests (envoyée à SonarCloud) | Apache-2.0, MIT |
| [bUnit](https://bunit.dev) (Egil Hansen et contributeurs), avec [AngleSharp](https://anglesharp.github.io) | Tests des pages et composants Blazor (`PageTestContext`) ; AngleSharp lit aussi les pages HTML dans `SiteTests` | MIT, MIT |
| [Microsoft.AspNetCore.Mvc.Testing](https://learn.microsoft.com/aspnet/core/test/integration-tests) (Microsoft) | Site entier démarré dans les tests (`SiteFactory`, WebApplicationFactory) | MIT |
| [SonarScanner for .NET](https://github.com/SonarSource/sonar-scanner-msbuild) (SonarSource), [SonarCloud](https://sonarcloud.io) | Revue de la qualité du code dans la CI (`.github/workflows/ci.yml`), service en ligne gratuit pour les projets publics | LGPL-3.0 (scanner) |
| [Caddy](https://caddyserver.com), [Docker](https://www.docker.com) | Mise en ligne (HTTPS, conteneurs) | Apache-2.0 |
| [GitHub Actions](https://github.com/features/actions) : [actions/checkout](https://github.com/actions/checkout), [actions/setup-dotnet](https://github.com/actions/setup-dotnet) | Tests et déploiement (`.github/workflows/ci.yml`, `cd.yml`) | MIT |
| [Bulbapedia](https://bulbapedia.bulbagarden.net) | Référence consultée pour vérifier l'ordre des Noigrumes d'Or/Argent/Cristal | — |
| [Bulbagarden Archives](https://archives.bulbagarden.net) | Jaquettes officielles de Pokémon Rouge, Bleu, Jaune, Or, Argent, Cristal, Rubis, Saphir, Émeraude, Rouge Feu, Vert Feuille, Diamant, Perle, Platine, HeartGold, SoulSilver, Noire, Blanche, Noire 2, Blanche 2, X, Y, Rubis Oméga, Saphir Alpha, Soleil, Lune, Ultra-Soleil, Ultra-Lune, Let's Go Pikachu, Let's Go Évoli, Épée, Bouclier, Diamant Étincelant, Perle Scintillante, Légendes Arceus, Écarlate et Violet (versions anglaises réduites, « Red/Blue/Yellow/Gold/Silver/Crystal/Ruby/Sapphire/Emerald/FireRed/LeafGreen/Diamond/Pearl/Platinum/HeartGold/SoulSilver/Black/White/Black 2/White 2/X/Y/Omega Ruby/Alpha Sapphire/Sun/Moon/Ultra Sun/Ultra Moon/Lets Go Pikachu/Lets Go Eevee/Sword/Shield/Brilliant Diamond/Shining Pearl/Legends Arceus/Scarlet/Violet EN boxart », Émeraude, Diamant, Perle, HeartGold et SoulSilver en JPEG), images des living dex (`wwwroot/images/livingdex/jaquettes`, par `outils/images.py livingdex`) | Images © Nintendo / Creatures Inc. / GAME FREAK Inc. ; usage de fan, non commercial |

Pokémon et tous les noms et images associés sont des marques et créations de Nintendo, Creatures Inc., GAME FREAK Inc. et
The Pokémon Company. Monsuta Potaru est un projet de fan privé, sans lien avec eux.
