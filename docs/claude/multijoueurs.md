# Multijoueurs : pages des joueurs, amis, échanges

README : « Multijoueurs », « Échanges ».

## Pages vues par un visiteur
- Collection, Parc, Inventaire, Living dex et Complétion (liste et page de chacun) ont une seconde route `/joueurs/{Pseudo}/...` :
  `Visitor` (paramètre `Pseudo`), compte trouvé par `PlayerService.FindAsync(pseudo, page)` à la place du sien.
- Blazor garde le même composant d'une adresse à l'autre (`/joueurs/sacha/collection` → `/collection`) : `MainLayout`
  met `@key` = pseudo de l'adresse (`Routes.PageOwnerOf`) pour recréer la page quand le joueur change. Les pages peuvent
  donc lire le compte dans `OnInitializedAsync`. Testé par `RoutesTests` (routeur et menu rendus par bUnit).
- En visiteur :
  - tout le contenu dans le `else` d'un `@if (Visitor && player is null)` ;
  - boutons d'action masqués **et** méthodes d'action qui sortent tout de suite (`if (Visitor) return;`) ;
  - ni journal, ni noms de fichiers (`LabelAndFile` → `Label`, `PublicSaveLabel` des living dex), ni ID de dresseur
    (`PokemonCard.HideTrainerId`), ni téléchargement.
- Nouvelle action ou nouvelle donnée sur ces pages : décider ce qu'en voit un visiteur.
- Le premier rendu d'un visiteur a lieu avant que le joueur soit trouvé : ce qui touche aux éléments de la page dans
  `OnAfterRenderAsync` (parc, grille de l'inventaire) attend que le contenu soit affiché (`engineStarted`, `contentStarted`).

## Visibilité
- Trois valeurs (`ProfileAudience` : tout le monde, amis seulement, personne). `PlayerService.FindAsync` prend le compte
  qui regarde : un ami (ou le joueur lui-même) voit les pages d'un profil « Amis seulement » ; « Personne » reste
  introuvable, même des amis.
- Profil caché, page non visible, compte sans pseudo : même réponse (`PlayerHidden`), administrateurs compris.
- Pages visibles : `PublicPages` (drapeaux) et `PlayerService.Pages` (onglets du profil, ordre). Une nouvelle page : son
  drapeau dans `All`, sa case dans `Visibility.razor`, une migration SQL pour les comptes existants (`StatisticsVisibility`,
  cochée pour tous ; le modèle ne change pas), son étape dans `Breadcrumbs.Player`.

## Amis
- `FriendService` ; amitiés en double (une ligne par compte).
- Une action qui change les demandes passe par `FriendService.Notify` (le menu relit son compteur).

## Échanges
- `TradeService` : mécanique, testée sans navigateur. `TradeHub` : déroulé en direct, singleton ; tout changement passe
  par ses méthodes sous verrou, les pages s'abonnent à `TradeSession.Changed`.
- La page du second à valider écrit les deux sauvegardes.
- Confirmation de départ (`NavigationLock`) : se teste en remplaçant `window.confirm` dans la page.
- Compatibilité : `TradeFamily` (contexte PKHeX + Time Capsule + région Game Boy), la même pour l'échange entre amis et
  l'échange interne. `SAV1.Japanese`, `SAV2.Japanese` / `Korean`. Conversion PK1 ↔ PK2 par `EntityConverter` ;
  `IsCompatiblePKM` ne suffit pas vers Rouge / Bleu : `ConvertFor` vérifie aussi `MaxSpeciesID`, `MaxMoveID` et refuse
  les œufs. Un Pokémon de 5e génération n'a pas de dresseur actuel (`HandlingTrainerName` vide).
- Mécanique commune : `PrepareAsync` (relecture, empreintes, œufs, conversion, évolution sur des copies) puis
  `WriteAsync` (transaction, copies, trace anti-copie ; ligne `TradeRecords` pour les amis seulement).

## Échange interne (`Pages/InternalTrade.razor`)
- Étapes : `Step` (Saves, Send, Receive, Confirm, Done). Seule la paire est dans l'adresse (`?de=&vers=`, traitée dans
  `OnParametersSetAsync`, une fois par paire : `shownPair`) ; les Pokémon choisis restent en mémoire. « Retour » de
  l'étape 2 navigue vers `echange-interne` sans paire : les listes gardent `firstId` / `secondId`.
- Choix revérifiés : liste du serveur pour les `<select>` (`Partners`), puis le service (`ReadPairAsync`, `PrepareAsync`
  avec le compte pour les deux côtés). La confirmation appelle `InternalPreviewAsync` (rien d'écrit) pour annoncer les
  évolutions et détecter un changement avant « Échanger ».
- Échec avec `Retry` : retour à l'étape 2, Pokémon relus ; sinon retour à l'étape 1, sauvegardes relues.
- Pas de visiteur, pas de `NavigationLock` (rien en cours chez un autre joueur).
- Étapes 2 et 3 : repère, titre, boutons, Pokémon choisi et recherche dans `.internal-trade-sticky` (collé sous la barre
  du haut, 3,5 rem, à partir de 641 px de large et 600 px de haut, comme la barre de « Ma collection ») ; la grille est
  dans un second bloc. L'espace sous le bloc collé est un `padding` du bandeau (fond de la page), pas une marge du bloc :
  sinon la grille se voit entre les deux.

## Valeurs trafiquées (Blazor Server)
- La valeur d'un événement (`<select>`, champ, `[JSInvokable]`) vient du navigateur. Un service appelé avec l'identifiant
  d'un autre compte revérifie la relation lui-même (amitié : `FriendService.RelationAsync`, visibilité :
  `PlayerService.FindAsync`), sans se fier à la liste affichée.
- Test d'un `<select>` : changer la `value` d'une option dans la page, puis déclencher `change`.
- Test d'un argument de `[JSInvokable]` (parc : `OnPokemonClicked`) : remplacer `WebSocket.prototype.send` en gardant
  l'original dans une variable, réécrire l'argument dans le message (« [137] » → « [136] », même longueur), puis
  **remettre l'original** (le supprimer casse la page). Le Pokémon du parc se clique en cherchant ses pixels dans le
  canvas (`getImageData`).

## Essai à deux dans le navigateur
Cookies partagés entre onglets : un joueur sur `localhost:5299`, l'autre sur `127.0.0.1:5299` (un cookie est propre au
nom d'hôte).
