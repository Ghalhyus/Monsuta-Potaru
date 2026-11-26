# Cache-cache (Mini-jeux)

README : « Cache-cache (Mini-jeux) ». Page `Components/Pages/HideSeek.razor` (`/mini-jeux/cache-cache`), réglages et
tirage `Services/HideSeek/HideSeekDraw.cs`, parties en direct `HideSeekHub.cs`, résultats `HideSeekRecorder.cs`
(+ `HideSeekService` : Pokémon des collections, statistiques), parc `wwwroot/js/parc.js` (celui du Parc, options en
plus). Tests : `HideSeekDrawTests` (réglages, tirage, collections), `HideSeekHubTests` (salle d'attente, clics, tours,
fin, départs, recommencer, enregistrement), `HideSeekPageTests` (page, deux joueurs, invité sans compte), statistiques dans
`StatisticsPageTests`, page sans compte dans `SiteTests`.

## Demande de l'utilisateur (2026-11-23)
« Où est Charlie ? » avec des Pokémon, en multijoueur : trouver un Pokémon précis dans un parc, en cliquant dessus. Pokémon
tous différents ; ils doivent pouvoir aller au parc. Un nombre de tours, une durée maximale par tour ; temps écoulé sans
que personne ne trouve : égalité, tour suivant. À la fin, celui qui a trouvé le plus de Pokémon gagne. Réglages de l'hôte :
nombre de tours, surnommés seulement, d'où viennent les Pokémon (collection de l'hôte, de tous les joueurs, aléatoires
hors collection), durée d'un tour, parc (aléatoire ou toujours le même), nombre de Pokémon (20 au moins).

Réponses à mes questions :
- **2 à 8 joueurs**, amis invités **et** code / lien comme aux échecs (invités sans compte compris) ; salle d'attente,
  l'hôte lance.
- **Mauvais clic : bloqué 3 secondes** (`HideSeekTiming.Penalty`), « Raté ! C'était … ».
- **Les Pokémon se promènent** (comportement du parc, sans attaques ni humeurs : tous « normal ») ; le serveur vérifie
  le numéro cliqué. **Depuis le 2026-11-23, tous les joueurs voient les mêmes Pokémon aux mêmes endroits** (demande de
  l'utilisateur, « si ce n'est pas trop coûteux ») : simulation commune de `parc.js` (option `sync`, voir `parc.md`),
  graine du tour `HideSeekRound.Seed` (tirée par `Draw`), début = `StartsAt` du serveur (`startsIn` passé à `create`).
  Rien ne passe par le réseau pendant le tour. Pas de Pokémon qui viennent voir la souris au cache-cache. **La nuit**
  (demandée ensuite) : `HideSeekDraw.NightLevel(StartsAt)`, l'heure de **Paris** au début du tour (le serveur est en UTC
  dans Docker ; sans base des fuseaux : UTC+1), même formule que `nightLevel` (`parc-modele.js`), passée dans `sync.night`.
- **Unicité : espèce + forme** (`HideSeekCandidate.Key` = dossier Donjon de la forme, sans chromatique ni femelle : deux
  formes au même dessin comptent pour une). Une cible ne revient pas dans la partie (les leurres, si).
- **Cible : image + nom** ; avec « surnommés seulement », « Bob (Pikachu) » et « Pokémon de Sacha ».
- **Pool trop petit : des Pokémon aléatoires complètent le parc**, la cible vient toujours du pool. La salle d'attente
  dit à l'hôte combien de Pokémon différents son choix donne (`poolInfo`). Il faut au moins une cible par tour
  (`HideSeekDraw.Check`) : sinon « Lancer la partie » est grisé et le message dit quoi changer.
- **Parc aléatoire : un paysage à chaque tour**, les 8, sans les avoir achetés ; le tirage respecte
  `ParkLandscapes.Refusal` (océan : nageurs seulement, cible et leurres). « Toujours le même » : l'hôte choisit.
- **20 à 80 Pokémon**, carte selon le nombre (`MapSize` : 20 par taille, comme les parcs).
- **Tours et durée en champs libres** : 1 à 30 tours, 15 s à 5 min (proposé : listes ; choisi : champs).
- **Nom : « Cache-cache »** (« Où est Charlie ? » est une marque) ; icône loupe (`bi-search-nav-menu`).
- **Toute « Ma collection », telle quelle** : banque, sauvegardes **et mondes Minecraft** (le Parc, lui, les refuse ;
  choix de l'utilisateur pour ce jeu), chromatiques et formes compris ; pas les œufs ; seulement les espèces qui ont des
  sprites Donjon (`HasDonjonSprites`). Surnommé = `Nickname != SpeciesName` (comme le filtre « Surnommé »).
- **Statistiques + « Recommencer »**.
- **Retours du 2026-11-23, après essai** : « le parc prend toute la fenêtre » → choix : **parc plein cadre, infos
  par-dessus** (panneau en haut à droite, `.hide-seek-panel` ; plus de colonne) ; « la cible annoncée dans un pop-up avec
  le décompte » (`.hide-seek-announce`, sur le parc caché) puis **un petit rappel** de la cible dans le panneau ;
  **décompte réglable de 3 à 10 s, 5 par défaut** (`HideSeekSettings.CountdownSeconds` ; 3 s au moins : le parc se charge
  pendant ce temps).
- Proposés par moi, sans objection : 5 tours d'1 min et 40 Pokémon par défaut ; 5 s de pause (`HideSeekTiming.Reveal`)
  pour montrer la cible et qui l'a trouvée ; un joueur qui part sort (compté perdant), l'hôte qui part arrête tout (rien
  n'est compté) ; un invité sans compte n'apporte pas de Pokémon au pool « tous les joueurs » ; une partie à la fois par
  joueur (par hub : on peut jouer aux échecs en même temps) ; un tricheur pourrait reconnaître la cible dans le
  JavaScript (les sprites des Pokémon envoyés) : accepté entre amis.

## Partie en direct (`HideSeekHub`)
- Calqué sur `ChessHub` : en mémoire (perdu au redémarrage), verrou du hub puis de la session (`Gate`), jamais l'inverse ;
  pages abonnées à `HideSeekSession.Changed`, lecture par `Snapshot` (copie). Minuteur toutes les 250 ms (`Tick`).
  `UtcNow` : l'heure du hub, celle avec laquelle la page compte les secondes (sinon le test au temps manuel ne marche
  pas).
- Étapes : `Lobby` (réglages modifiables par l'hôte, `UpdateSettings` revérifie ; invitations `Invite` / `Accept`, code
  `JoinWithCode` ; ferme après `LobbyLifetime` = 15 min), `Playing`, `Done`, `Cancelled`.
- Un tour : `Countdown` (`CountdownSeconds`, parc caché par `.hide-seek-cover` et la cible annoncée dans le pop-up :
  chacun le charge, personne ne voit avant les autres ; un clic y est refusé « Attends le début du tour. »), `Searching` (jusqu'à `Deadline`), `Reveal` (5 s, `TargetId` donné
  aux pages : `focus` dans parc.js). Le clic qui trouve passe tout de suite en `Reveal`.
- **Lancement** : la page de l'hôte lit le pool en base (`HideSeekService.PoolAsync`), tire les tours
  (`HideSeekDraw.Draw`) puis `Start(session, hôte, réglages, joueurs, tours)` ; refusé si les joueurs ou les réglages ont
  changé entre-temps. Les invitations restantes tombent.
- Numéros des Pokémon : 1 à N dans chaque tour, mélangés (ni l'ordre ni le numéro ne disent la cible). Le `Snapshot` porte
  la cible entière (`Target`) côté serveur, mais la page n'en envoie au navigateur que l'image et le nom ; `TargetId`
  seulement à la fin du tour.
- Fin : `OutcomeOf` — partis : perdu ; sinon le meilleur score gagne, plusieurs en tête : nulle (personne n'a rien trouvé :
  nulle pour tous) ; seul joueur restant : il gagne. `Finished` → `HideSeekRecorder` (une ligne `HideSeekRecords` par
  joueur, invités compris, reliées par `GameId`, migration `HideSeekRecords` écrite à la main).
- **Recommencer** : l'hôte seulement (il a les réglages) ; nouvelle salle d'attente, mêmes réglages, les autres joueurs
  encore là invités (`RematchOf`) ; ils voient la proposition sur leur écran de fin (`RematchOffer`), ou dans « Invitations
  reçues ». Un invité sans compte qui quitte l'écran de fin la refuse (`DeclineGuestOffers`).
- **Options du 2026-11-23 (après essai)** : « les Pokémon du parc doivent aimer le parc » → réglage **Humeur des Pokémon
  dans le parc** (`HideSeekSettings.Mood`, `HideSeekMood` : Tous par défaut, Neutres et heureux, Heureux seulement ; le
  joueur choisit, demandé ainsi), valable pour **tous** les Pokémon du parc (cible, leurres du choix, aléatoires du
  complément). « Ne pas compléter par des Pokémon au hasard » → case **Compléter le parc avec des Pokémon au hasard s'il
  n'y en a pas assez** (`FillWithRandom`, cochée par défaut) ; décochée : **moins de Pokémon, même carte** (choix de
  l'utilisateur : la carte reste celle du nombre réglé, `MapSize`). Case masquée avec des Pokémon aléatoires (sans effet).

## Tirage (`HideSeekDraw`)
- Paysage du tour, puis la cible (une clé au hasard, puis un Pokémon au hasard de cette clé : un Pikachu en 10 exemplaires
  n'a pas plus de chances), puis les leurres : d'abord le reste du pool (une clé chacun), ensuite les aléatoires (sauf
  `FillWithRandom` décoché : le parc s'arrête au pool).
- **Filtre commun** `HideSeekCandidate.Fits(paysage, réglages)` : `Accepted` (paysage) et l'humeur demandée, calculée
  comme au Parc (`ParkHabitats.MoodOf` sur la carte du paysage à la taille `MapSize`) par `HideSeekDraw.MoodIn`, gardée
  par (types, paysage, taille) : 1 025 aléatoires × 8 paysages × 30 tours sinon. Types de cases pas lus (tests sans
  `ParkHabitats.Load`) : neutre. Utilisé pour le paysage au hasard (seulement ceux où une cible convient), la cible et
  les leurres, et `Usable` (Pokémon différents possibles, pour `Check` et la salle d'attente ; sans paysage : ceux qui
  conviennent à l'un d'eux). Salle d'attente sans complément : « le parc n'en aura que N », ou, au hasard, « de A à B
  selon le paysage du tour » (paysages où une cible au moins est possible). Vérifié le 2026-11-23 : volcan, aléatoires,
  « Heureux seulement » → 204 Pokémon possibles, parc rempli de Feu ; tests `HideSeekDrawTests.Draw_WithAMood_*`,
  `Draw_WithoutRandomFill_*`, `HideSeekPageTests.Lobby_MoodAndRandomFill_*`.
- `RandomPool()` : chaque espèce 1 à 1025 qui a des sprites Donjon, forme 0, calculé une fois (gardé seulement s'il n'est
  pas vide : images absentes au démarrage).
- `HideSeekCandidate.Key` est calculée à la construction (des `File.Exists` : ne pas la recalculer à chaque accès).

## Page et parc (`HideSeek.razor`, `parc.js`)
- **Plein cadre** : `create` avec `fill: true` (parc.js) : le canvas prend toute la largeur et la hauteur qui reste dans
  la fenêtre (moins les marges du bas des parents, `spaceBelow` : sans ça, la page défilait de la marge de
  `.ardoise-content`). **Zoom de départ = le plus dézoomé** (demande du 2026-11-23 : « voir toute la map sans la
  bouger ») : `fitZoom`, où toute la zone visible (`viewArea` : zone « Parc » et `VIEW_MARGIN`) tient dans le canvas,
  **pas forcément entier** (un zoom entier laissait la carte toute petite) ; la forêt de la carte, ou le fond, remplit
  les côtés. On ne dézoome pas en dessous (`minZoom`) ; on zoome par crans entiers (`stepZoom` : le nombre entier
  d'à côté) jusqu'à deux de plus que le zoom entier qui couvre tout le canvas (`coverZoom`, l'ancien zoom de départ),
  puis on fait glisser. `spriteK` reste entier. Vérifié le 2026-11-23 en 1333 × 800 (Forêt, 40 Pokémon : zoom 1,29 puis
  2, 3, 4 et retour) ; canvas jusqu'en bas, pas de défilement, Parc inchangé (`fill` absent). Dans une fenêtre étroite,
  le panneau peut couvrir le bord droit de la zone (zoomer et faire glisser) ; sur l'écran de l'utilisateur (21:9), la
  carte laisse de la place de chaque côté.
- Un nouveau parc à chaque tour (`dispose` puis `create`, comme `StartEngine` du Parc), créé dans `OnAfterRenderAsync`
  (`ShowRound`, l'élément du parc doit être rendu ; `boardRound` empêche d'en créer deux). Options de `create` ajoutées
  pour le jeu : `names: false` (pas de nom au survol, il ferait trouver la cible) ; méthode `focus(id)` (vue centrée,
  Pokémon arrêté face à nous, cercle jaune qui pulse ; `null` l'efface).
- Plein écran sur le bloc `.hide-seek-board.park`, qui contient aussi le pop-up et le panneau : ils restent visibles.
  Téléphone (< 640 px) : le panneau en bas, sans les scores.
- Après un mauvais clic, le parc est teinté de rouge (`.hide-seek-blocked`, `pointer-events: none` : le serveur ignore les
  clics de toute façon).
- Message au-dessus du parc : `data-hors-mesure` (voir `parc.md`).
- Essais de codes limités avec ceux des échecs (`AccountLimiter.TryChessCode`, 10 par minute et par adresse).

## Tester
- `HideSeekPageTests` remplace le hub de `PageTestContext` par un hub au temps manuel (`Services.AddSingleton` après :
  le dernier gagne) et appelle `Tick` après chaque avance. Il charge les vraies images (`PokemonSprites.Load`) : la partie
  se joue avec des Pokémon aléatoires.
- Dans le navigateur : l'hôte avec un compte sur `localhost:5299`, un invité sans compte sur `127.0.0.1:5299` (code), ou
  deux comptes (`navigateur.md`). Le clic sur un Pokémon : `OnPokemonClicked` par le composant ; pour trouver la cible,
  lire son nom dans la colonne et la chercher dans le parc (ou la console : rien ne la donne, c'est voulu).
