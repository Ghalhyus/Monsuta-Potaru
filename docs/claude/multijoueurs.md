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

## Recommencer (échange entre amis, combat, échecs ; demandé le 2026-11-22)
- But de l'utilisateur : ne pas renvoyer une demande pour refaire la même chose avec le même joueur. Choix de
  l'utilisateur : la proposition s'affiche **sur l'écran de fin de l'autre** (Accepter / Refuser, sans passer par la
  liste ; s'il a quitté la page, c'est une demande ordinaire : pastille du menu, « Demandes reçues », 10 minutes) ;
  bouton seulement après une activité **acceptée** (finie, abandonnée ou annulée en cours de route), jamais après une
  demande refusée ou expirée ; **tout est à rechoisir** (Pokémon de l'échange, équipes) ; échecs : **couleurs
  inversées**, invité sans compte compris.
- Hubs : `Rematch(précédente, …)` crée une demande ordinaire marquée `RematchOf` (id de la précédente) ; `Accepted`
  (posé par `Accept`, et `JoinPublic` aux échecs) dit si l'on peut recommencer ; `RematchOffer(précédente, joueur)` trouve
  la proposition reçue (affichée sur l'écran de fin, relue à chaque `RequestsChanged`). Si l'autre a déjà proposé,
  `Rematch` **accepte** sa demande (message vide) : deux clics croisés ne font pas deux demandes.
- Échange et combat : la page relit la paire par `SavePairsAsync` (amitié, sauvegardes encore là ; libellés à jour) avant
  `Rematch`, qui revérifie que les deux joueurs sont ceux de la précédente. Celui qui propose devient `First`.
- Pages : `Reset()` (comme « Nouvel échange » / « Nouveau combat », sans effacer le message) avant de rejoindre la
  nouvelle session ; sinon l'étape affichée (`shownStage`) ou les Pokémon de l'équipe (`candidates`, `team`) restaient
  ceux de l'activité finie. Bouton grisé pendant les animations (`Playing`, `animating`).
- Textes des listes : « te propose de recommencer votre échange / combat », « t'invite à recommencer votre partie ».

## Animation d'échange (`Shared/TradeAnimation.razor`, `wwwroot/js/echange.js`, `Services/TradeShow.cs`)
- Choix de l'utilisateur (2026-11-15) : à chaque échange (entre amis, interne, GTS), façon 4e génération ; entre amis,
  chacun voit ce qu'il reçoit (pas l'évolution du Pokémon donné) ; échange interne : les deux évolutions (reçu, puis
  envoyé) ; GTS : celui qui accepte tout de suite, le déposant à la récupération ; boutons « Passer » et son coupé.
- Musiques (fournies par l'utilisateur, le 2026-11-15) : pistes « Trade », « Evolution » et « Congratulations! Your
  Pokémon Evolved! » de la bande originale de HeartGold / SoulSilver, © Nintendo, **jamais dans Git** :
  `wwwroot/images/sons/echange.mp3`, `evolution.mp3`, `felicitations.mp3` (dossier des images, déjà ignoré et monté depuis
  le serveur), envoyées par `deploy\sons-serveur.cmd`, rapatriées par `recuperer-images.cmd`. Choix : sans fichier, rien
  (pas de jingle de remplacement).
- `Services/TradeSounds` (`Load(webRoot)` dans `Program.cs`) regarde la présence à chaque animation : le composant ne met
  `data-musique-echange` / `-evolution` / `-felicitations` (les deux dernières seulement s'il y a une évolution) que pour un
  fichier présent, sans quoi aucune requête ; sans aucune musique, pas de bouton du son. `TradeAnimationTests` passe par
  `TradeSounds.Load` sur un dossier temporaire (état statique : ne pas s'y fier dans d'autres tests).
- echange.js (`Music`) : échange en boucle dès l'envoi ; à chaque évolution, fondu de l'échange (0,5 s) et évolution depuis
  le début ; à la nouvelle forme, l'évolution s'arrête (0,2 s) et la fanfare (5 s) joue ; le message « Félicitations ! »
  reste jusqu'à la fin de la fanfare (au plus 8 s). Sans évolution : fondu de 1,5 s après l'arrivée. `close()` à « Passer »
  et à la fermeture. Un `play()` refusé par le navigateur (pas de clic sur la page) ne bloque pas l'animation.
- `TradeShow(Partner, Sent, Received)` : `TradeShowPokemon` avec nom, image **avant** l'évolution et, s'il évolue, espèce
  et image après. L'image d'avant se prend avant `EvolveByTrade` (`PrepareAsync` des deux services). Œuf : pas d'image
  (`SpriteUrl` null, dessiné en CSS), elle révélerait l'espèce.
- Où il arrive : `TradeOutcome.FirstShow` / `SecondShow` (pseudos relus en base : `HasPseudo`, sinon « ton ami » ;
  interne : « « nom de la seconde sauvegarde » »), `GtsResult.Show` (`AcceptAsync`, `RetrieveAsync`). Le déposant de la
  GTS : JSON dans `GtsPokemon.TradeShow`, écrit à l'échange (null pour une offre expirée, annulée, ou reçue avant la
  migration `GtsTradeShow` : pas d'animation).
- Les pages : `show` (interne, GTS) ; entre amis, condition sur `TradeStage.Done` et `closedShow` (id de la session) pour ne
  pas la rejouer à chaque rendu. Composant hors du `Block` : `position: fixed`.
- Les textes sont dans le composant (`Lines`, clés `envoi`, `arrivee`, `evolution-<i>-debut` / `-fin`, éléments
  `data-ligne` cachés) ; echange.js les lit et ne fait que le minutage (Web Animations, `AbortController` pour « Passer »).
  Acteurs centrés par la propriété CSS `translate` : echange.js anime `transform` sans les décentrer.
- **Le fond ne bouge jamais** (demandé par l'utilisateur le 2026-11-15 : anneaux qui grossissaient pendant le voyage et
  étincelles qui montaient pendant l'évolution retirés). Seul l'assombrissement de l'évolution reste, en fondu.
- Navigateur intégré masqué : ses animations tournent ~3 fois trop lentement (500 ms en 1,7 s) ; ne pas en conclure un
  retard du code. Son clic réel peut échouer (« page did not finish rendering ») : cliquer par script ; les musiques ont
  quand même joué. Pour voir ce qui joue : envelopper `HTMLMediaElement.prototype.play` / `pause` dans la page (journal).
- Essai dans le navigateur : `noir.sav` + `noire-2.sav` (Carabing de Noir ↔ Escargaume de Noire 2 : deux évolutions ;
  Géolithe de Noir contre n'importe quoi : une).

## Recherche des Pokémon à choisir (échanges, équipe de combat)
- `Shared/PokemonPickerSearch.razor` (champ et `CollectionFilterChips`), sur les trois pages ; état dans la page
  (`InternalTrade.PickerFilter`, un par étape), rien dans l'adresse.
- `TradeCandidate.Traits` / `BattleCandidate.Traits` (`PokemonFilterTraits.Of`, calculé comme `PokemonTraits.Fill`) :
  valeurs simples, car les pages retrouvent le Pokémon choisi **par égalité des records** (`sent = sendable.FirstOrDefault(c
  => c == sent)`) : n'y mettre ni `BankPokemon` ni tableau.
- `PokemonSearch.Filter` applique `PokemonCriteria.Apply` (le code de la base) sur des `BankPokemon` faits en mémoire
  (`ToRow`) : une règle de filtre ajoutée à `PokemonCriteria` vaut ici aussi, si `PokemonFilterTraits` porte la valeur.
- Un œuf a `SpeciesName` vide dans ses caractéristiques : la recherche ne révèle pas son espèce.
- Combat : `NoEggs` retire la pastille « Œuf ».

## Échange interne (`Pages/InternalTrade.razor`)
- Étapes : `Step` (Saves, Send, Receive, Confirm, Done). Seule la paire est dans l'adresse (`?de=&vers=`, traitée dans
  `OnParametersSetAsync`, une fois par paire : `shownPair`) ; les Pokémon choisis restent en mémoire. « Retour » de
  l'étape 2 navigue vers `echange-interne` sans paire : les listes gardent `firstId` / `secondId`.
- Choix revérifiés : liste du serveur pour les `<select>` (`Partners`), puis le service (`ReadPairAsync`, `PrepareAsync`
  avec le compte pour les deux côtés). La confirmation appelle `InternalPreviewAsync` (rien d'écrit) pour annoncer les
  évolutions et détecter un changement avant « Échanger ».
- Échec avec `Retry` : retour à l'étape 2, Pokémon relus ; sinon retour à l'étape 1, sauvegardes relues.
- Pas de visiteur, pas de `NavigationLock` (rien en cours chez un autre joueur).
- Étapes 2 et 3 : repère, titre, boutons, Pokémon choisi, recherche et pastilles dans `.internal-trade-sticky` (collé sous la barre
  du haut, 3,5 rem, à partir de 641 px de large et 600 px de haut, comme la barre de « Ma collection ») ; la grille est
  dans un second bloc. L'espace sous le bloc collé est un `padding` du bandeau (fond de la page), pas une marge du bloc :
  sinon la grille se voit entre les deux.

## GTS (`Services/GtsService.cs`, `Pages/Gts.razor`)
- Choix de l'utilisateur (2026-11-15) : boîte de réception (rien n'est écrit dans une sauvegarde sans son joueur : le
  Pokémon reçu, expiré, annulé ou d'une sauvegarde supprimée attend qu'il le récupère), dépôt depuis toute « Ma
  collection », offre retirée et Pokémon rendu quand sa sauvegarde est supprimée, 5 offres par compte.
- Une seule table `GtsPokemon` : `Status` `Offered` = offre, sinon case de la boîte de réception (au moment de l'échange,
  la ligne de l'offre reçoit les octets du Pokémon donné). Elle compte dans `CollectionCapacity` et dans
  `PokemonFingerprint.FindAsync` (étiquette `GtsLabel`) : sinon une vieille copie de la sauvegarde ferait revenir le
  Pokémon pendant qu'il est sur la GTS.
- Un Pokémon d'une sauvegarde se retrouve par `BankPokemon.Position` (ordre de `StoredSaveService.ReadAllPokemon` :
  équipe, boîtes, puis le reste) : `GtsService.SlotAt` ; l'empreinte confirme. Le reste (pension, Boîte Combat...) et les
  cases verrouillées ne partent pas. Dans la grille, les grisés se devinent d'après `BankListItem.Location` (`PickProblem`) ;
  le service revérifie.
- Famille de l'offre (`FamilyContext`, `FamilyRegion`) : celle de la sauvegarde, ou `TradeFamily.Of(PKM)` au stockage
  interne (région des cartouches Game Boy lue dans le Pokémon). Celui qui accepte depuis le stockage interne garde les
  deux formats tels quels.
- Échange : la demande est revérifiée sur le Pokémon lu (niveau, sexe, chromatique, **légalité recalculée** : la colonne
  `IsLegal` n'est qu'un affichage). L'offre est prise par `ExecuteUpdateAsync` conditionnel (`Status == Offered`, pas
  expirée) dans la transaction : un second joueur reçoit « n'est plus disponible ». Le Pokémon donné évolue par échange
  avant d'aller dans la boîte du déposant.
- Expiration : `GtsExpiry` (`BackgroundService`, toutes les heures) et `ExpireAsync` à l'ouverture de la page ; la
  recherche ignore déjà une offre dont `ExpiresAt` est passé.
- Pastille du menu : `SiteMenuBase.gtsInbox`, relue à chaque navigation et sur `GtsEvents.InboxChanged` (échange accepté
  par un autre joueur, expiration, annulation, récupération). La page GTS s'y abonne aussi (boîte et offres relues).
- Suppression d'une sauvegarde : `GtsService.EndOffersOfSaveAsync` dans la transaction de `StoredSaveService.RemoveAsync`
  (avant la suppression : la clé étrangère `StoredSaveId` est en `SetNull`). « Mes sauvegardes » annonce les offres retirées.
- Tests : `GtsServiceTests` (service), `GtsPageTests` (page, deux joueurs avec `RenderAs`, boîte mise à jour en direct).
  Les Pokémon de test doivent avoir réellement le niveau demandé (pas seulement la colonne `Level`) : la légalité et le
  niveau sont relus dans les octets.

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
