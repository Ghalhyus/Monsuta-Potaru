# Combats

README : « Combats ».

## Moteur
- Simulateur de Pokémon Showdown (`showdown/`, `@pkmn/sim` en version fixée ; `npm ci --ignore-scripts --prefix showdown` en local et
  dans la CI ; étape `node` du `Dockerfile`). Aucune mécanique de combat réécrite en C# : le site traduit le protocole
  (`BattleView`) et dessine l'interface.
- Nouveau format (génération) : `BattleTeam.Format`.
- `combat.js` reçoit les Pokémon **par numéros** (PKHeX) et les traduit en noms de Showdown : `byNum` préfère l'entrée
  standard de la génération (un objet « Past » de la 2e génération partage parfois son numéro : « Berry » / « Oran Berry »).
- `combat.js` ne transmet que les choix et la fin forcée. `>eval` existe dans le simulateur : **jamais**.

## Protocole
- Lignes `|split|<camp>` suivies de la version secrète (pour ce camp) puis de la publique.
- Un choix peut être refusé (« [Unavailable choice] ») : Showdown renvoie une requête corrigée, à laquelle on répond de nouveau.
- Nouvelle ligne ou nouvel effet : `BattleView` note ce qu'elle ne sait pas traduire (`Unhandled`).
- Cas rares : monter temporairement le nombre de combats de `RandomBattles_EndAndAreFullyTranslated` (300 : ~2 min 30) ;
  relire les phrases : écrire le journal d'un combat (`Steps1`) dans un fichier.
- Recensement des lignes produites par Showdown : en Node (`BattleStreams`, équipes au hasard en « gen3customgame » :
  `@pkmn/sim` n'a pas le générateur d'équipes aléatoires).

## Combat en direct
- `BattleHub` tient l'état sous `BattleSession.Gate` (le simulateur écrit sur son propre fil) ; les pages lisent par
  `Snapshot` et `Steps` (copies).
- Ordre des verrous : celui du hub, puis celui de la session, jamais l'inverse.
- Délai de choix : à partir de la requête + la durée des étapes du tour (`BattleTiming.Playback`).
- Page : `PlayAsync` ne fait rien quand il n'y a pas d'étape nouvelle (un simple « l'autre a choisi » refermerait le menu
  ouvert sous le clic du joueur).
- Résultats : `BattleRecorder` (service hébergé) écoute `BattleHub.Finished` et écrit `BattleRecords` à côté du fil du
  simulateur. Tests : `BattleRecorder.RecordAsync` appelé directement. `Recorder_WritesEachFinishedBattle` attend le
  signal `SavedChanges` de son contexte (`SignalingFactory`) au lieu d'interroger la base : `TestDatabase` n'a qu'une
  connexion SQLite, que deux fils ne doivent jamais utiliser en même temps (échecs au hasard en CI, « SQLite Error 5:
  'not an error' » dans `CreateFunction`, le 2026-11-01).

## Interface (deux écrans, façon 4e génération)
- Tout est dessiné en CSS (`app.css`, « Combats ») d'après des captures d'écran fournies par l'utilisateur (carte
  Trello 108) : disposition, formes et couleurs reprises, **aucun graphisme découpé d'un jeu**, police du site (son choix).
- Les deux écrans ont la même largeur (`--ecran`) en 4:3 ; leur contenu se mesure en `em` (1em = 1/32 de la largeur) et
  en % : ne jamais y mettre de `rem` ni de `px` (les proportions casseraient à une autre taille).
- Disposition : `BattleLayout` (empilés d'origine, côte à côte, hybride), boutons `.battle-layout` et « Préférences » ;
  retenue dans le compte (`docs/claude/comptes.md`, « Préférences »). `.battle-zone` est un conteneur CSS : sous 40rem de
  large, toujours empilés sur toute la largeur et boutons cachés, quel que soit le mode choisi.
- **Mode hybride** (`.battle.hybride`, maquette de l'utilisateur du 2026-11-14) : même page, mêmes éléments, seul le CSS
  change (bloc `@container` « Mode hybride ») : le terrain en grand, `.battle-bottom` réduit à un panneau à droite (44 %
  de la largeur du terrain, même hauteur) où tout passe en colonne (`position: relative`, `inset: auto` sur les boutons
  placés en absolu ailleurs). Ce que la page change pour lui : ABANDONNER rendu après POKéMON (ordre de la colonne).
  La ligne des sauvegardes et les boutons de disposition restent **au-dessus** du combat, comme dans les autres modes
  (l'utilisateur a refusé de les passer dessous pour gagner de la hauteur, le 2026-11-14). Dans le panneau : barre des Pokéballs toujours affichée, sur deux lignes ; équipe sur
  une ligne par Pokémon, sans niveau ni boîte de message. Un bouton ajouté au menu doit aussi tenir dans cette colonne.
- `.battle-btn` pose `gap` (donc `row-gap`) : dans une grille serrée (`.battle-member` du mode hybride), le remettre à
  zéro, sinon la dernière ligne déborde et se fait rogner.
- Zone large : la taille fait tenir les écrans dans la hauteur de la fenêtre, `--dessus` étant la hauteur de ce que la
  page affiche au-dessus (mesurée dans Ardoise ; `--alertes` compte les alertes, posé par la page). Un élément ajouté
  au-dessus des écrans : corriger `--dessus`, sinon la page défile. À 2293×850 px CSS (écran de
  l'utilisateur) : environ 305×230 px CSS par écran empilé, 610×460 côte à côte, terrain de 610×460 en hybride (même hauteur que côte à côte).
- Boutons du jeu : `.battle-btn` (deux tons `--haut` / `--bas`, coins rouges au survol et au focus clavier). Les
  capacités prennent la couleur du type par `--type`. Plus de bouton SAC depuis le 2026-11-14 (inutile pour l'instant,
  choix de l'utilisateur) : ABANDONNER à gauche et POKéMON à droite se partagent le bas de l'écran tactile.
- Le menu de l'équipe prend tout l'écran du bas (`ShowsTeam`, classe `.equipe`) : la barre de Pokéballs, toujours
  rendue, est cachée par le CSS (sauf en mode hybride) et le minuteur passe dans la boîte de message. Un Pokémon K.O. n'a plus ses PV maximum dans la requête de Showdown (« 0 fnt ») : pas de « 0/159 ».

## Sprites (`BattleSprites`)
- Ceux du jeu **de celui qui regarde** (sa sauvegarde, connue quand son équipe est acceptée : `BattleTeamResult.Sprites`),
  dans les dossiers des living dex de 3e génération (`dos/`, `chromatique/`, `outils/images.py combats`).
- Forme qui ne change que l'image (lettre de Zarbi) : passe quand même par `forme` ; `combat.js` la donne à Showdown comme
  forme d'aspect (`cosmeticFormes`), qui la remet dans la description du Pokémon.
- Nouvelle génération de combats : ses dossiers dans `BATTLE_SPRITES` (PokéAPI a des dos jusqu'à la 5e).

## Tests
- Les tests de combat lancent le vrai simulateur (`BattleTests.Showdown()` : échec clair s'il n'est pas installé ; il faut Node 22+).
- Le simulateur tire au hasard (coups critiques, précision) : un test qui attend un menu choisit des attaques sans
  dégâts (`BattlesPageTests` : Sécrétion contre Groz'Yeux). Un K.O. force un remplacement, sans menu ni ABANDONNER (échec en
  CI le 2026-11-13 : Balayage critique de Férosinge sur Chenipan niveau 4).

## Essai à deux dans le navigateur
- Sacha sur `127.0.0.1:5299`, Ondine sur `localhost:5299` ; chacune a une sauvegarde de 3e génération dans la base de
  test (Vert Feuille, Rouge Feu).
- Clics par `javascript_tool` (un script ne dépasse pas 45 s : découper les attentes). « ABANDONNER » : remplacer
  `window.confirm` avant de cliquer. Le minuteur se vérifie sans rien choisir pendant ~70 s.
- Recharger la page pendant un combat le fait perdre (le circuit fermé quitte le combat) : une retouche de CSS se
  regarde au combat suivant. Pour voir les détails, agrandir les écrans le temps d'une capture
  (`.battle` : `style.setProperty('--ecran', '520px')`) ; chaque joueur n'a que 60 secondes par choix.
- Pas de champ pour demander en ami sur « Amis » sans code : passer par `/joueurs/<pseudo>`, « Demander en ami ».
