# Échecs Pokémon (Mini-jeux)

README : « Échecs (Mini-jeux) ». Page `Components/Pages/Chess.razor` (`/mini-jeux/echecs`), règles
`Services/Chess/ChessGame.cs`, types `ChessTypes.cs`, équipes `ChessTeams.cs`, parties en direct `ChessHub.cs`, résultats
`ChessRecorder.cs` (+ `ChessService`, statistiques), plateau `wwwroot/js/echecs.js` (Phaser). Tests : `ChessGameTests`
(règles), `ChessHubTests` (hub, enregistrement, pièces prises), statistiques dans l'onglet Mini-jeux
(`docs/claude/statistiques.md`), `ChessPageTests` (page, deux joueurs, fenêtres « Pièces » et « Aide », case des
Pokémon au repos).

## Choix de l'utilisateur (2026-11-22)
- Demande d'origine : un seul type par Pokémon ; super efficace = la pièce rejoue ; neutre = elle mange ; pas très
  efficace ou sans effet = elle ne mange pas ; les pièces des échecs ; le Pokémon **marche** jusqu'à sa case, attaque s'il
  mange, l'autre joue sa blessure ; invitation acceptée comme les échanges et les combats ; chaque joueur choisit une
  équipe, **pas la même** (la partie bloquerait), et peut **revenir sur son choix** tant que l'autre n'a pas choisi
  la sienne (`ChessHub.ClearTeam`, bouton « Changer d'équipe », demandé le 2026-11-22) ; l'invité joue les noirs ; équipes par défaut seulement (Psy, Feu,
  Électrik : `ChessTeams.All`, types choisis par lui ; quatre autres ajoutées ensuite, voir plus bas, Motisma Lavage = forme 2, Raichu d'Alola = forme 1). Espèces
  vérifiées d'après les noms français de PKHeX (`GameInfo.GetStrings("fr").specieslist`) : Kaorine = 344, Rhinolove =
  528, Boumata = 776 (la première version avait pris Tiboudet, Chovsourir et Draïeul).
- Réponses à mes questions :
  - tour bonus : **le même Pokémon**, il peut passer ; d'abord **en chaîne** (chaque prise super efficace redonnait
    un tour), puis, à sa demande (2026-11-22), **un seul** : une prise super efficace pendant le tour bonus ne fait pas
    rejouer encore (« … C'est super efficace, mais Démolosse a déjà rejoué. »), et ses cases n'ont pas les coins dorés ;
  - un tour bonus **peut manger le roi = victoire** ; hors de là, **échec et mat gardé** (on ne laisse pas son roi
    mangeable ; mat = victoire, pat = nulle) ;
  - **en direct**, comme les combats (en mémoire, perdu au redémarrage) ; **l'hôte choisit le temps par coup** en créant
    la partie (30 s, 1, 2, 5, 10 min ; 2 min par défaut, proposition de ma part) ;
  - **pion au bout** : il ne remplace qu'un Pokémon **K.O.** ; les deux échangent leurs places **en marchant** ; les K.O.
    sont **visibles sur le côté** ; sans K.O., il attend et l'échange se fait plus tard, à la place d'un coup ;
  - roque et prise en passant gardés (avec la règle des types) ;
  - bouton d'aide : fenêtre **« Pièces »** avec les deux équipes côte à côte (puis, le même jour, une fenêtre « Aide » à
    part : voir plus bas) ;
  - plateau **pixélisé comme les sprites**, **dessiné par le code** (pas de tuiles d'un pack).
- Proposés par moi, sans objection : un Pokémon revenu pendant un tour bonus prend ce tour bonus ; un pion K.O. ne revient
  pas ; deux rois seuls = nulle ; pas de nulle par répétition ni par 50 coups ; une partie à la fois par joueur ; résultats
  en base et bloc « Échecs » des statistiques ; mes Pokémon tournés vers l'adversaire (on voit leur dos, comme les siens
  en combat) ; pas de musique.

## Équipes Poison, Eau, Plante, Sol (ajoutées le 2026-11-22)
- Composition et types donnés par l'utilisateur (`ChessTeams.All`) ; noms corrigés d'après PKHeX : **Roserade** (sans
  accent), **Libégon** (avec). Noms vérifiés en cherchant la chaîne UTF-8 dans `PKHeX.Core.dll` (Windows PowerShell 5.1
  ne charge pas une DLL .NET 10).
- **Deux espèces de pion** (équipe Poison) : deux membres `Pawn` dans `Members`, `PawnAt(colonne)` les alterne
  (Nidoran♂ en a, c, e, g, choix de l'utilisateur, pour les deux camps) ; `Of(pièce)` rend le premier. Ils doivent avoir
  **le même type** (test `Teams_HaveOnePokemonPerPiece`) : l'aide n'a qu'une ligne par pièce et calcule sa marque avec le
  premier. Choix de l'utilisateur : **une ligne « Pion », deux images** (`MembersOf`, `NamesOf` « Nidoran♂ / Nidoran♀ »,
  `NamesInSentence` « … ou … » dans les phrases de l'aide). `.chess-team-list` est une grille dont les lignes sont des
  sous-grilles : la colonne des images prend 8rem pour cette équipe, les noms restent alignés. Les tableaux de l'aide
  passent alors à ≈ 800 px CSS de large (tiennent encore côte à côte chez l'utilisateur).
- `FromPosition` place un pion selon la colonne de sa case ; un pion K.O. donné par `ko` est le premier.

## Partie publique (demandée le 2026-11-22)
- Bouton « Créer une partie publique » dans « Nouvelle partie » (même sans ami ; le temps par coup est en premier dans la
  rangée). `ChessHub.CreatePublic` : code de 8 caractères (`CodeAlphabet`, sans 0, O, 1, I, L), unique parmi les parties,
  `Guest` vide (`ChessParty("", "")` ; `Involves("")` toujours faux) jusqu'à `JoinPublic`, qui le remplace (verrou du
  hub puis de la session). Vaut `RequestLifetime` (expiration par `Tick`, message « le code a expiré ») ; une fois.
- Choix de l'utilisateur : **lien à partager** (`?code=`, `[SupplyParameterFromQuery]`, code déjà rempli ; « Copier le
  code » / « Copier le lien » par `navigator.clipboard.writeText`, message de repli si la copie est refusée) et **lien
  sur l'accueil et la connexion** (sans compte) ; l'invité **tape un prénom** (`ChessHub.GuestPseudo` : 2 à 20 caractères,
  lettres, chiffres, espaces, tirets, apostrophes, points ; « Léo (invité) ») ; parties **comptées pour l'hôte**
  (`ChessRecords.BlackId` = identifiant d'invité, pas de clé étrangère) ; un joueur **connecté** peut aussi saisir un
  code (sous son pseudo, comptée pour lui).
- Page sans `[Authorize]` : sans compte, `userId` = `ChessHub.NewGuestId()` (« invite-… », tiré par le serveur, perdu au
  rechargement = page quittée = défaite) ; seulement le bloc « Rejoindre une partie » (`JoinBlock`) et un lien de
  connexion qui revient avec le code. Connecté : ce bloc en premier s'il vient d'un lien, sinon sous « Nouvelle partie ».
- Bouton et non `<form>` : un formulaire envoyé avant l'interactivité partirait en POST au serveur sans `FormName`.
- **Essais limités par IP** : `AccountLimiter.TryChessCode` (10 par minute). Un composant interactif n'a pas de
  `HttpContext` : App.razor passe `RemoteIpAddress` à `Routes` (`ClientIp`), qui le cascade (`Name = "ClientIp"`) ; les
  paramètres d'un composant racine interactif sont protégés dans la page (non modifiables par le navigateur).
- Tests : `ChessHubTests` (code, saisie libre, une fois, expiration, joueur occupé, prénoms, résultat compté),
  `ChessPageTests` (invité sans compte de bout en bout, lien pour un joueur connecté, limite d'essais : `RenderAnonymous`),
  `SiteTests` (page ouverte sans compte, code du lien rempli, liens de l'accueil et de la connexion).
- Piège de test : la page de l'hôte a un `NavigationLock` (quitter annule la partie) ; un `NavigateTo` vers le lien avec
  sa page ouverte est refusé (`confirm` faux dans bUnit). Créer alors la partie par le hub.

## Règles (`ChessGame`)
- Cases 0 à 63 = 8 × rangée + colonne, a1 = 0. Une pièce = `ChessUnit` (numéro fixe toute la partie : echecs.js anime
  par numéro), son `ChessMon` (espèce, forme, type, pièce).
- « Atteint » (`Reaches`) = la marche de la pièce, chemin libre ; « attaque » (`Attacked`) = atteint **et** peut manger
  le type de la victime. L'échec, les cases menacées du roque, le mat se calculent avec le **type du roi** : une case
  n'est pas « menacée » en soi. Ainsi Kaorine (Sol) n'empêche pas le roque de Dracaufeu (Vol).
- Coups permis : candidats (marche + types), puis `Legal` (son roi pas mangeable après le coup, `Simulate`), sauf un coup
  qui mange le roi (toujours permis : la partie est gagnée).
- Pion au bout : à l'arrivée, un coup **par Pokémon K.O. revivable** (`Revive`) ; sans K.O., le coup simple (il attend).
  Plus tard : `ChessMove(case, case, id)` (`IsSwap`). Le pion part chez les K.O. de son camp.
- Tour bonus : `BonusSquare` (case de la pièce qui rejoue, ou du Pokémon revenu à sa place) ; abandonné tout de suite si
  elle ne peut plus bouger. `LegalMoves` y ajoute `ChessMove.Pass`. Un coup joué avec `BonusSquare` posé
  (`onBonusTurn` dans `Play`) n'en redonne jamais un autre. La page ne dore pas les prises super efficaces pendant le
  tour bonus (`Highlight` : `super` faux).
- Fin du coup (`Record`) : au tour de l'autre (sauf bonus), puis rois seuls, mat ou pat. Le récit (`ChessStep.Text`) est
  écrit ici (« Démolosse mange Tarsal en f2. C'est super efficace ! Démolosse rejoue. »).
- Position de test : `ChessGame.FromPosition` (pièces, K.O. ; roques seulement si roi et tour sur leurs cases).

## En direct (`ChessHub`)
- Calqué sur `BattleHub` : verrou du hub puis celui de la session (`Gate`), jamais l'inverse ; pages abonnées à
  `ChessSession.Changed`, lecture par `Snapshot`, `Steps`, `Options`, `At`, `Units` (copies sous verrou).
- Délai : `Deadline` = maintenant + durée des animations du coup (`ChessTiming.Playback` : 350 ms par case, 1,6 s pour
  une prise, 1 s de marge) + temps par coup. echecs.js reçoit les mêmes durées (`timing`).
- Quitter la page (ou la recharger) pendant la partie = défaite (`Dispose` → `Leave`), comme les combats.

## Fenêtre « Aide » (demandée le 2026-11-22)
- Bouton « Aide » à côté de « Pièces » ; fenêtre du même genre (fond grisé). Choix de l'utilisateur : **deux tableaux
  croisés** (« Tes Pokémon contre les siens », « Ceux de <pseudo> contre les tiens » : les menaces), marques **×2** (mange
  et rejoue), **✓** (mange), **✗** (ne mange pas, sans distinguer « pas très efficace » de « sans effet ») ; en têtes
  l'image à 4rem, le nom, le type, la pièce. Une ligne (colonne) par pièce : les huit pions une fois.
- Marques par `ChessGame.CanEat` puis `PokeTypes.Effectiveness` (`HelpMark`) : la même règle que les parties.
- Taille : un tableau ≈ 744 × 570 px CSS ; côte à côte (≈ 1550 × 760 px pour la fenêtre) sur l'écran de l'utilisateur
  (2293 px CSS de large), l'un sous l'autre sinon (la fenêtre défile) ; trop étroit pour un tableau (téléphone) : il
  défile de côté (`.chess-help-scroll`). Dans les lignes, nom et type sur une ligne (`.chess-help-title`), sinon un
  tableau ne tenait pas en hauteur.

## Pokémon au repos (demandé le 2026-11-22)
- Case « Animer les Pokémon au repos » (`#chess-idle`) dans `.chess-side`, sous les boutons. Décochée : les pièces du
  plateau en état `idle` et les K.O. en état `sleep` restent sur la première image (`tick` remet `frame` à 0), **sauf
  la pièce sélectionnée** (son numéro retrouvé par `marks.selected` dans `position.board` : la sélection repart du début
  de l'animation). Marche, attaque, blessure toujours animées. Les K.O. dormaient d'abord toujours (premier choix de
  l'utilisateur) ; figés aussi depuis, à sa demande, le même jour.
- Choix de l'utilisateur : la case sur la page **et** dans « Préférences » (`SortPreferences.ChessIdle`, JSON
  `"echecsRepos":false`, détails dans `docs/claude/comptes.md`) ; un **invité sans compte** a la case, pour la partie
  en cours seulement. Défaut : animés (comme avant).
- Passée à `create` (`idle`) puis changée par `setIdle` ; la page enregistre aussitôt (`SetChessIdleAsync`).
- Essai dans le navigateur masqué : les Pokémon à animation Idle lente (Tarsal : deux images de 40 et 25 ticks, Gallame)
  ne changent même pas en mode animé à ~1,5 image/s (Phaser plafonne le temps par image) : mesurer avec un cavalier
  (Rhinolove, ~200 pixels changés), ou observer **30 s** (Sleep d'un K.O. : 500 et 583 ms par image ; le script dans
  une fonction lancée sans attendre, résultat dans `window`, relu ensuite : `javascript_tool` coupe à 45 s). Comparer deux copies du canvas (`drawImage` réduit à 420 × 276) case par case. Choisir
  **10 min par coup** : à 2 min, le temps s'écoule pendant les mesures et les clics ne sélectionnent plus rien.

## Cartes des équipes et fenêtre « Pièces »
- Images des Pokémon (96 × 96) à **4rem** : un pixel d'image par pixel d'écran chez l'utilisateur (150 %) ; plus petites,
  elles étaient réduites sans lissage (dentelées). Nom en entier et type sur une ligne, pièce dessous (jamais coupé).
- « Pièces » : 43rem de large, pour deux colonnes de 19rem (la largeur par défaut des fenêtres, 35 % de l'écran, n'en
  laisse qu'une).

## Plateau (`echecs.js`)
- Géométrie en pixels du plateau : cases de 32, cadre de 10, panneaux des K.O. (2 × 8 places) de chaque côté : 420 × 276.
  Un nombre entier `k` de pixels d'écran par pixel, le plus grand qui tient (`fit`) : en largeur, la rangée
  `.chess-play` moins la colonne d'à côté ; en hauteur, la fenêtre sous le haut du plateau, moins les marges, bordures et
  rembourrages des blocs qui l'entourent (mesurés : marche avec chaque thème). Recalculé quand la rangée ou la page
  change de taille ; plus étroit qu'à `k = 1` : le CSS réduit le canvas.
- **Page d'une partie sans défilement** (demande de l'utilisateur, 2026-11-22) : pendant la partie (et à la fin), pas de
  bandeau de titre (`PageHeader` remplacé par un `h1` masqué) ni d'en-tête de bloc ; adversaire, boutons « Pièces » /
  « Abandonner », tour, temps, dernier coup, messages et échange du pion dans la colonne `.chess-side`, collée à droite
  du plateau (au-dessus sur un écran de moins de 1100 px). Avant (2026-11-22, première version) : le plateau était sous
  le bandeau, l'en-tête du bloc et l'état de la partie (334 px CSS), estimés à 170 : la page défilait.
- Tout est dessiné dans une texture `createCanvas` (ImageData, grain tiré d'une graine fixe) ; lettres 3 × 5 pixels
  (`GLYPHS`). Les marques sont un `Graphics` redessiné (`highlight`).
- Sprites : `loadSheets` **exporté par parc.js** (planches partagées) ; textures recréées par jeu de Phaser.
- **Pièges rencontrés** :
  - Phaser appelle `build()` / `tick()` avant la suite de `create()` (après `await started`) : ce qu'elles utilisent
    doit être déclaré avant `new Phaser.Game` ou être une **déclaration de fonction** (hissée), sinon « Cannot access …
    before initialization ».
  - La case cliquée se calcule depuis `p.event.clientX` et le `getBoundingClientRect()` **du moment** : `p.worldX` de
    Phaser garde la place du canvas mesurée au démarrage (la page a bougé depuis : clic décalé).
  - `play` est plafonné à 10 s (`MAX_PLAY`) ; une animation dépassée est annulée (`current`, `CANCELLED`) : sans quoi
    elle continuait et remettait les pièces dans une ancienne position après le coup suivant.
  - La page attendait le plateau pendant le chargement des sprites et en créait plusieurs : `boardStarting`.
- **Essai dans le navigateur intégré masqué** : `requestAnimationFrame` à ~2 images/s, les animations dépassent les 10 s ;
  l'onglet du second joueur ne tourne pas en arrière-plan (`tabs_select` avant de le faire jouer). Une capture d'écran ne
  montre pas le canvas WebGL : le copier (`drawImage` sur un canvas 2D, `toDataURL`) dans une `<img>` posée sur la page.
  Clics : `MouseEvent` `mousemove`/`mousedown`/`mouseup` sur le canvas aux coordonnées de la case. Le navigateur garde
  l'ancien `echecs.js` en cache : `fetch('/js/echecs.js', { cache: 'reload' })` puis recharger la page.
