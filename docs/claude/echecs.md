# Échecs Pokémon (Mini-jeux)

README : « Échecs (Mini-jeux) ». Page `Components/Pages/Chess.razor` (`/mini-jeux/echecs`), règles
`Services/Chess/ChessGame.cs`, types `ChessTypes.cs`, équipes `ChessTeams.cs`, parties en direct `ChessHub.cs`, résultats
`ChessRecorder.cs` (+ `ChessService`, statistiques), plateau `wwwroot/js/echecs.js` (Phaser). Tests : `ChessGameTests`
(règles), `ChessHubTests` (hub, enregistrement, pièces prises), statistiques dans l'onglet Mini-jeux
(`docs/claude/statistiques.md`), `ChessPageTests` (page, deux joueurs, fenêtres « Pièces » et « Table de type des
pièces », cases « Désactiver les animations » et « Contraste élevé », marques de la pièce choisie, même équipe).

## Choix de l'utilisateur (2026-11-22)
- Demande d'origine : un seul type par Pokémon ; super efficace = la pièce rejoue ; neutre = elle mange ; pas très
  efficace ou sans effet = elle ne mange pas ; les pièces des échecs ; le Pokémon **marche** jusqu'à sa case, attaque s'il
  mange, l'autre joue sa blessure ; invitation acceptée comme les échanges et les combats ; chaque joueur choisit une
  équipe (d'abord **pas la même** ; depuis, à l'aveugle et la même permise : voir « Choix à l'aveugle ») et peut
  **revenir sur son choix** tant que l'autre n'a pas choisi la sienne (`ChessHub.ClearTeam`, bouton « Changer
  d'équipe », demandé le 2026-11-22) ; l'invité joue les noirs ; équipes par défaut seulement (Psy, Feu,
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

## Choix à l'aveugle, même équipe, noms des équipes (demandés le 2026-11-22)
- **Noms** : d'après le roi (`ChessTeam.Name` calculé : « Équipe Nidoking ») ; `ShortName` = le roi (« Équipe favorite »
  des statistiques). Les identifiants (`psy`, `feu`…) ne changent pas (`ChessRecords.WhiteTeam`) : les parties déjà
  enregistrées prennent le nouveau nom. Choix de l'utilisateur : le type du thème n'est plus rappelé nulle part.
- **À l'aveugle** : la page ne lit plus l'équipe de l'autre pendant le choix (`TakenBy` supprimé) mais
  `ChessSession.TeamChoice` (`ChessTeamChoice` : la sienne, `OtherChose` sans dire laquelle, `Same`, `IKeep`,
  `OtherKeeps`, `OtherChanging`). Choix de l'utilisateur : « X a choisi son équipe » affiché.
- **La même** : `ChooseTeam` ne la refuse plus ; deux équipes différentes, la partie commence (`Start`) ; la même, la
  partie attend. Chacun « Garder l'équipe … » (`KeepTeam` ; les deux : `Start`) ou « Changer d'équipe » (`ClearTeam`).
  Celui qui change d'une même équipe est noté (`ChangingId`) : l'autre lit « X change d'équipe : tu peux garder la
  tienne, ou en changer aussi » (le simple « Changer d'équipe » d'avant lui suffit, l'autre n'ayant plus d'équipe) ;
  si l'autre change aussi, ou que celui qui changeait rechoisit, `ChangingId` s'efface. Choisir ou changer remet les
  deux « garder » à faux (même équipe reprise : on redemande aux deux).
- **Partie à même équipe** (choix de l'utilisateur) : les noirs en **chromatique** (`ChessGame.SameTeams`,
  `ChessUnit.Shiny`, posé par `NewUnit` ; `ChessUnit.SpriteUrl` / `DonjonFolder` ; `ChessMon.SpriteUrlOf` /
  `DonjonFolderOf`) sur le plateau, chez les K.O., dans « Pièces » (`ChessTeamList Shiny`) et la table de type
  (`HelpImages`). Seulement dans ces parties : les autres ne changent pas. Tous les Pokémon des équipes ont un dossier
  Donjon chromatique (`…/0000/0001/` ou `…/<forme>/0001/`) **sauf Roserade** (407) : `DonjonFolder` retombe sur
  l'ordinaire ; son image fixe chromatique existe. Piège signalé aux joueurs : des Pokémon du même type ne se mangent
  souvent pas (pions face à face, les deux rois).

## Pièce choisie : cadres, croix, aperçu (demandés le 2026-11-22)
- `ChessOptions.Matchups` (`ChessGame.Matchups`) : toutes les pièces adverses du plateau, même hors de portée, d'efficacité
  ≠ 1. Choix de l'utilisateur : **vert** = super efficace (> 1), **rouge** = elle ne peut pas la manger (< 1), neutres sans
  cadre ; vert même pendant le tour bonus (c'est le type qui compte, pas le tour bonus). Cadre de 2 px (`frame` dans
  `drawMarks`), sous les coins des prises.
- **Croix rouge** : sur les pièces de `Blocked` (atteintes mais pas mangeables), à la place de la petite croix grise ; dès
  la sélection ; **en contraste élevé seulement** depuis le 2026-11-23 (demande de l'utilisateur : par défaut, la pièce
  est déjà bleue avec son bouclier ; la légende n'en parle pas non plus, `MatchupLegend`) ; dessinée dans un second `Graphics` (`crosses`, profondeur 50) **par-dessus** les sprites (profondeur 10 +
  y), bord sombre de 4 px et cœur rouge de 2 px en diagonale.
- **Aperçu pendant le tour adverse** (choix de l'utilisateur) : `CanPreview` (page) ; `ChessSession.Options` rend les
  options d'une pièce du joueur même hors de son tour : cadres (`Matchups`) et croix (`Blocked`, qui ne dépend pas du
  tour), mais **ni cases ni échange de pion** (le 2026-11-23, l'utilisateur ne voulait pas de points là où on ne peut
  pas jouer ; la première version montrait les cases qu'elle atteindrait, `PreviewFrom`, supprimée). Le texte ne dit
  donc pas « ne peut pas bouger » en aperçu (`Select`). Rien ne s'y joue : `Preview` sélectionne, explique
  une prise impossible, ou efface ; `Play` revérifie le tour. `PlayAsync` efface la sélection quand l'autre joue ; la fin
  de la partie aussi (`OnSessionChanged`). Texte : « Aperçu (au tour de X) : … », puis la légende des marques présentes
  (`MatchupLegend`).
- Boutons « Pièces » et « Table de type des pièces » (ancien « Aide ») en `btn-primary` : la couleur principale du thème
  (bleu, orange avec Ardoise), choix de l'utilisateur ; « Abandonner » reste en contour.

## Pièces faibles ou qui résistent : teintes, boucliers, animations (demandés le 2026-11-23)
- Mode par défaut depuis le 2026-11-23 ; les cadres vert et rouge (ci-dessus) sont devenus le mode **« Contraste
  élevé »** : case `#chess-contrast` sous « Désactiver les animations », gardée dans le compte
  (`SortPreferences.ChessContrast`, `"echecsContraste":true`, voir `docs/claude/comptes.md`) ; invité sans compte : pour
  la partie. Passée à `create` (`contrast`) puis changée par `setContrast` (redessine les marques). La légende
  (`MatchupLegend`) suit le mode ; changer de mode remplace seulement la légende dans le texte (`ContrastChanged` : une
  nouvelle sélection aurait annulé un échange de pion en cours).
- Choix de l'utilisateur (captures fournies, couleurs relevées dessus) : pièce **qui résiste** (`marks.resist`, elle ne
  peut pas la manger) = **teinte bleue**, petit bouclier bleu ; pièce **faible** (`marks.weak`, super efficace) =
  **teinte rouge**, bouclier gris **cassé**, et pas de croix rouge. Contraste élevé : cadres et croix (ni teinte, ni
  bouclier). Test : `ChessPageTests.RedCross_OnlyInHighContrast` (position posée par réflexion).
- **Ni animation ni demi-tour** (2026-11-23) : la première version faisait jouer l'attaque (qui résiste) ou la blessure
  (faible) en boucle, le Pokémon tourné vers la pièce choisie ; trop rapide pour l'utilisateur, puis une fois toutes les
  3 s, puis retirée à sa demande, et le demi-tour aussi. Le Pokémon reste dans son état de repos, face à l'adversaire.
- Teinte : copie de chaque planche recolorée une fois (`tinted`, texture `<clé>~weak` / `~resist`) d'après la
  luminosité de chaque pixel, en trois paliers (`FILTERS`) : pas de `setTint` de Phaser (une multiplication noircit un
  Pokémon de la couleur opposée, un Pokémon Feu sous du bleu). Boucliers : 9 × 10 pixels (`SHIELDS`, dessinés comme les
  lettres), en bas à droite de la case, dans le `Graphics` des croix (par-dessus les sprites).
- `tick` : `matchupKinds` (vide en contraste élevé et pendant un coup) donne la teinte de chaque pièce (`a.filter`),
  rien d'autre. `current` est déclarée avant `new Phaser.Game` : `tick` la lit.

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
  sur la connexion** (sans compte ; d'abord aussi un bouton sur l'accueil, retiré le 2026-11-23 à sa demande) ; l'invité **tape un prénom** (`ChessHub.GuestPseudo` : 2 à 20 caractères,
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
  `SiteTests` (page ouverte sans compte, code du lien rempli, lien de la connexion, plus de bouton sur l'accueil).
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
- **Prise super efficace qui met son roi en échec** (bug signalé par l'utilisateur le 2026-11-22 : le roi Gallame ne
  pouvait pas manger Démolosse sur une case tenue par une tour, alors qu'il rejouait aussitôt) : hors tour bonus, `Legal`
  la permet si le tour bonus a au moins un coup permis (roi à l'abri, ou roi adverse mangé). `BonusCanSave` pose le
  plateau d'après le coup dans `board` (avec `BonusSquare`, sans prise en passant) le temps d'appeler `MovesFrom`, puis
  le remet : la partie est toujours lue sous le verrou de la session. Pendant ce tour bonus, **pas de passe**
  (`CanPass`, aussi dans `ChessSnapshot` : la page cache « Passer » et dit « ton roi est en échec, choisis une case qui
  le met à l'abri ») ; le récit ajoute « Il doit mettre son roi à l'abri. ». Vaut pour toute pièce (une prise qui
  découvre son roi et peut le recouvrir en rejouant), pas seulement le roi. Une prise neutre reste interdite. Le mat en
  tient compte (une telle prise peut être la seule parade).
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
  l'attaque et la blessure, la marche du K.O. jusqu'à sa place, 1 s de marge) + temps par coup. echecs.js reçoit les
  mêmes durées (`timing`).

## Recommencer (demandé le 2026-11-22 ; règles communes : `docs/claude/multijoueurs.md`)
- **Couleurs inversées** : `Rematch` crée `ChessSession(ancien Guest, ancien Host)` ; l'hôte reste **les blancs**
  (`ColorOf`, `Player`, `ChessRecord.WhiteId` inchangés), mais celui qui invite peut être l'adversaire
  (`InvitedByGuest`, `Inviter` / `Invitee`). Tout ce qui parle de la demande (`Active`, `Busy`, `Incoming`, `Accept`,
  refus dans `Leave`, expiration de `Tick`, `RequestsChanged`) passe par `Inviter` / `Invitee`, jamais par `Host` /
  `Guest`. Un invité sans compte peut donc jouer les blancs (sans clé étrangère : rien à changer aux statistiques).
- Partie publique : la revanche est une invitation directe (pas de code) ; une partie publique que personne n'a
  rejointe n'est pas `Accepted`.
- Invité sans compte : il n'a pas de liste d'invitations ; quand il quitte l'écran de fin (« Nouvelle partie », page
  fermée : `DeclineGuestOffers`), les propositions qui lui sont adressées sont refusées (« Léo (invité) a quitté la page. »).
- **Piège trouvé dans le navigateur** : une partie finie pendant le chargement de son plateau (abandon tout de suite),
  puis « Recommencer » : `create` (echecs.js) revenait après coup et `StartBoard` gardait ce plateau de l'ancienne partie
  (rattaché à un élément disparu) et son `Snapshot` (« À toi de jouer » chez les noirs) ; plus de plateau pour la
  nouvelle. `StartBoard` compare la session avant et après chaque `await` : l'ancien plateau est fermé (`dispose`) et
  `OnAfterRenderAsync` redemande un rendu. bUnit ne sait pas retarder un appel qui rend un `IJSObjectReference` : pas de
  test automatique, vérifié dans le navigateur (abandon 0,3 s après le choix des équipes).

## Marche des K.O. (demandée le 2026-11-22)
- Avant : le Pokémon mangé s'effaçait et réapparaissait endormi chez les K.O. Maintenant il **marche** jusqu'à sa place
  (`koRoute`, `toKo` dans echecs.js), puis s'endort. Choix de l'utilisateur : **entre les cases** (demi-case vers la
  ligne entre deux rangées, du côté de sa place ; cette ligne jusqu'à l'espace de 8 px entre le plateau et le panneau ;
  cet espace jusqu'à la ligne voisine de sa place ; sa place : il ne passe sur aucun Pokémon), le mangeur **avance en
  même temps** (une demi-case de marche après le départ du K.O.), **vitesse d'une pièce** (350 ms par case).
- Proposés par moi : le roi mangé s'efface toujours (pas de place chez les K.O., la partie est finie) ; le pion au bout
  et le Pokémon qui revient prennent le même chemin (le revenu à l'envers, depuis sa place dans la position d'avant).
- **Temps du coup** : `ChessTiming.KoRoutePixels` refait le calcul de `koRoute` (mêmes constantes de géométrie : à
  changer des deux côtés). Les K.O. blancs sont à droite pour les blancs, à gauche pour les noirs, et le plateau est
  retourné : la longueur horizontale est la même vue des deux côtés, pas la verticale ; `KoWalkSquares` prend la plus
  longue. Pour le Pokémon qui revient, sa place d'avant le coup n'est pas dans `ChessStep` : la plus longue des places
  possibles. `ChessStep.Squares` ne compte plus l'échange (avant : 4 cases forfaitaires). Test :
  `ChessGameTests.Playback_CountsTheWalkToTheKnockedOut` (306 px pour d4 → première place, 498 px pour b8 vu des noirs).
- Jusqu'à ~17 cases de marche (≈ 6 s) dans le pire cas : `MAX_PLAY` passé de 10 à 15 s.
- Essai dans le navigateur : copier le canvas dans un `requestAnimationFrame` (après le rendu de Phaser ; avec un
  `setTimeout`, la copie du WebGL est vide) ; panneau du navigateur **affiché** (`show_pane` « preview ») : masqué, ~1
  image/s et la marche ne tient pas dans `MAX_PLAY`.
- Quitter la page (ou la recharger) pendant la partie = défaite (`Dispose` → `Leave`), comme les combats.

## Fenêtre « Table de type des pièces » (demandée le 2026-11-22, d'abord « Aide »)
- Bouton « Table de type des pièces » à côté de « Pièces » ; fenêtre du même genre (fond grisé). Choix de l'utilisateur : **deux tableaux
  croisés** (« Tes Pokémon contre les siens », « Ceux de <pseudo> contre les tiens » : les menaces), marques **×2** (mange
  et rejoue), **✓** (mange), **✗** (ne mange pas, sans distinguer « pas très efficace » de « sans effet ») ; en têtes
  l'image à 4rem, le nom, le type, la pièce. Une ligne (colonne) par pièce : les huit pions une fois.
- Marques par `ChessGame.CanEat` puis `PokeTypes.Effectiveness` (`HelpMark`) : la même règle que les parties.
- Taille : un tableau ≈ 744 × 570 px CSS ; côte à côte (≈ 1550 × 760 px pour la fenêtre) sur l'écran de l'utilisateur
  (2293 px CSS de large), l'un sous l'autre sinon (la fenêtre défile) ; trop étroit pour un tableau (téléphone) : il
  défile de côté (`.chess-help-scroll`). Dans les lignes, nom et type sur une ligne (`.chess-help-title`), sinon un
  tableau ne tenait pas en hauteur.

## Pokémon au repos (demandé le 2026-11-22)
- Case « Désactiver les animations » (`#chess-idle` ; d'abord « Animer les Pokémon au repos », sens inverse : renommée
  à la demande de l'utilisateur, elle n'arrête toujours que le repos) dans `.chess-side`, sous les boutons. Cochée : les
  pièces du plateau en état `idle` et les K.O. en état `sleep` restent sur la première image (`tick` remet `frame` à 0), **sauf
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
