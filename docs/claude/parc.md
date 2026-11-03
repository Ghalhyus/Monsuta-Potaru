# Parc

README : « Parc ». Page `Park.razor`, service `ParkService`, affichage et animation `wwwroot/js/parc.js` (Phaser).

Projet à venir (refuge, IA par espèce) : `docs/projet-parc.md`.

## Plusieurs parcs (`Parks`, `ParkLandscapes`)
- Table `Parks` (compte, `ParkLandscape`, `Expansions`), index unique (compte, paysage) ; `ParkMembers.ParkId` (clé
  `BankPokemonId` inchangée : un Pokémon dans un seul parc). Migration `ParkLandscapes` : une Forêt par compte avec
  l'ancienne colonne `AspNetUsers.ParkExpansions` (supprimée), ses Pokémon rattachés. Vérifiée sur une copie de la base de
  l'utilisateur le 2026-11-01 (Forêt à 3 agrandissements, 60 Pokémon gardés).
- **Piège de la migration** : EF reconstruit une table SQLite (clé étrangère ajoutée ou retirée) **à la fin** de la
  migration, quel que soit l'ordre des opérations. Dans le `Down`, la reconstruction de `ParkMembers` venait après
  `DROP TABLE Parks`, qui la vidait (cascade) : reconstruction écrite en SQL avant la suppression. Regarder le SQL avec
  `dotnet ef migrations script <après> <avant>` dans la copie du projet.
- La Forêt est créée à la première visite (`ListParksAsync` → `EnsureForestAsync`, `INSERT OR IGNORE`), pas à
  l'inscription. `BuyAsync` : `INSERT OR IGNORE` (0 ligne : déjà acheté) puis débit, dans la même transaction.
- Tout numéro de parc vient du navigateur (liste déroulante) : chaque méthode filtre sur `OwnerId` (« Ce parc n'existe
  pas. »). `ParkServiceTests.ParkOfAnotherPlayer_IsRefused`.
- Nouveau paysage : une valeur de `ParkLandscape`, ses cartes `{MapName}-{taille}.json`, une ligne dans chaque méthode de
  `ParkLandscapes` ; `ParkHabitats.Load` et la fenêtre d'achat le prennent d'eux-mêmes. Un paysage réservé à certains
  Pokémon : `ParkLandscapes.Refusal` (raison courte, ou null), repris par `ListCandidatesAsync` (`ParkCandidate.Refusal` :
  grisé) et revérifié par `SetAsync` (`ParkService.RefusalMessage`).
- **Règle de l'utilisateur pour tout nouveau parc (2026-11-01) : une bordure de carte comme celle de la forêt, 2 à 3
  cases de large à l'œil** (arbres, palmiers ou rochers serrés, sans trou), à vérifier sur l'aperçu de la dernière taille.
  Forêt : `foret` de `generer-parc.py`, 6 cases en haut, 5 ailleurs, ± 2,2 ; plage et volcan : `EPAISSEUR` = 4,4 ± 0,9
  dans `generer-plage.py`, `generer-volcan.py`, `generer-desert.py` et `generer-ocean.py` (2,4 donnait une seule rangée trouée). Compter large : une case cachée sans décor reste libre, et
  un palmier ou un gros rocher prend deux cases de profondeur.

## Agrandissements (`ParkService`)
- Capacité `CapacityOf(Park.Expansions)` = 20 + 20 par agrandissement, `MaxExpansions` = 3, par parc. `ExpandAsync` :
  dans une transaction, `ExecuteUpdate` conditionnel sur la taille (`< MaxExpansions`) puis débit conditionnel du
  porte-monnaie ; débit refusé : `RollbackAsync` (la taille revient). Ligne de journal `WalletOperationKind.ExpandPark`
  (« Forêt : 20 → 40 Pokémon ») ; achat d'un parc : `BuyPark` (libellés dans `Inventory.razor`).
- `SetAsync` au-dessus de la capacité : accepté seulement si les Pokémon voulus sont tous déjà au parc (anciens parcs de
  30 ou de 25). La fenêtre de choix suit la même règle (`!c.InPark`).
- Après l'achat, `Park.razor` rafraîchit `WalletState` (solde du menu) et redémarre le parc (`StartEngine` : `dispose`
  puis `create` avec `map` = `ParkLandscapes.MapName` et `size = Expansions + 1`) ; de même après un achat ou un
  changement de parc dans la liste.
- **Planches gardées par le module** (`sheets`) d'un jeu de Phaser à l'autre : une texture doit être recréée dans chaque
  jeu (`shadowOf` vérifie `textures.exists`). Sans : la texture « manquante » de Phaser, un carré noir à la place des
  ombres (vu au redémarrage après un achat, et en revenant sur la page sans la recharger).

## Sprites
- `PokemonSprites.DonjonFolder` (forme, Méga, chromatique, femelle). Formes de SpriteCollab (`images/donjon/formes.json`,
  écrit par `outils/images.py donjon` depuis son `tracker.json`) rapprochées des noms de PKHeX : nom compacté (« Mega X »
  ↔ « Mega_X »), noms de PokéAPI, mots retirés à la fin (« Paldea Combat » ↔ « Paldea »), puis seul nom qui commence
  ainsi (« Dusk » ↔ « Dusk_Mane »). ~240 formes de PKHeX trouvent leur sprite ; les autres n'en ont pas encore chez
  SpriteCollab (casquettes de Pikachu, coupes de Couafarel...).
- Une espèce peut aller au parc si `images/donjon/<espèce sur 4 chiffres>/AnimData.xml` existe (`HasDonjonSprites`).
- SpriteCollab centre chaque image sur le même point, les pieds 4 à 10 px sous le centre (le dessin remonte l'image de
  6 px de sprite pour poser les pieds sur l'ombre).

## Carte (Tiled, pack Cute Fantasy)
- `wwwroot/parc/parc-1-1.json` à `parc-1-4.json`, une par taille (dans Git : numéros de tuiles seulement), écrites par
  `outils/carte-parc.py <carte .tmx>` (nom tiré du fichier : `Parc_1-2.tmx` → `parc-1-2` ; exporte le `.tmx` avec
  `tiled.exe --embed-tilesets --export-map json`, vérifie, copie les planches). Planches dans
  `images/parc/tuiles/<nom du fichier du pack>` : **jamais dans Git** (licence : ni redistribution ni revente, même
  modifié), envoyées par `deploy\fond-parc.cmd`, rapatriées par `recuperer-images.cmd`. Sans elles : herbe unie (`GRASS`).
- Pack : `C:\Sources\Asset_Cute_Fantasy` (+ Desert, Dungeons, ShroomLands, Volcano), tuiles 16 px. Les jeux de couleurs
  vont par numéro : `Grass_Tiles_2`, `Grass_2_Middle` et `Water_Tile_2` ont la même herbe.
- Cartes de l'utilisateur : `C:\Sources\Parcs` (`Parc_1-1.tmx` à `Parc_1-4.tmx` = les cartes du site, `Plage_1-*` : la plage, sorties telles
  quelles de `outils/generer-parc.py` : les corriger dans le script, pas dans Tiled ; bordures d'eau et de chemin
  automatiques, forêt en quinconce, graine fixe ; `Parc_1.tmx` : l'ancienne carte unique). Python du système sans Pillow :
  un `venv` dans un dossier temporaire. Lancer
  `python outils/generer-parc.py C:/Sources/Asset_Cute_Fantasy/Cute_Fantasy C:/Sources/Parcs <dossier des aperçus>`,
  puis `python outils/carte-parc.py C:/Sources/Parcs/Parc_1-<n>.tmx` pour chacune ; comparer l'ancien et le nouveau JSON
  calque par calque (seules les cases voulues doivent changer). Objets de plusieurs lignes (bancs, gros rochers) : toutes
  leurs lignes bloquées (le 2026-10-31, le dossier des bancs ne l'était pas : on marchait dessus).
- **Tailles** (`ZONES` du générateur, rectangle « Parc » de chaque carte ; « Départ » reste la première, la dernière est
  la carte entière : l'utilisateur veut retrouver toute la carte d'origine une fois tout débloqué) : le parc entier
  est tiré une fois (objets enregistrés dans `objets`, tuiles et cases bloquées), puis `taille(zone, libres_avant)` :
  forêt hors de la zone avec une **lisière irrégulière** (0 à 2 cases vers l'intérieur, `LISIERE` ; moins que l'écart de
  5 cases au moins entre deux zones), qui contourne les objets et plans d'eau tenant dans la zone ; l'eau coupée devient forêt ;
  arbres de forêt tirés d'après leur case (les mêmes d'une taille à l'autre) quand les deux cases du tronc sont boisées
  (feuillage qui déborde en bas sur la zone : un objet de la zone qui le croise n'est pas posé) ; buissons sur le reste ;
  toute la forêt bloquée, sauf le bord sans arbre de la forêt d'origine (bordure, bosquets) dans la zone : de l'herbe où
  l'on marche, comme sur la carte du parc entier ; objets gardés s'ils sont tout entiers dans la zone et ne bloquent aucune case libre de la
  taille d'avant ; poches d'herbe enfermées → buissons. Le script s'arrête si une taille bloque une case libre de la
  précédente (`ParkMapTests` le vérifie aussi). Libres : 362, 999, 1 784, 3 062 cases (14 à 30 par
  Pokémon).
- **Moteur commun** `outils/parc_carte.py` (classe `Carte` : jeux de tuiles, `animer` (tuiles animées de Tiled, écrites
  dans le jeu de tuiles du `.tmx`), calques, `poser`, `essayer`, `boucher_poches(zone, decors, bloque, remplissage)`,
  `ecrire_carte` (`autres` : calques de tuiles en plus, après « Collisions » ; sans eux, le `.tmx` est le même qu'avant),
  `apercu` ; fonctions de formes). Les générateurs
  gardent leur paysage et l'ordre de leurs tirages. Refonte du moteur : régénérer les deux parcs dans un dossier
  temporaire et comparer avec `cmp` (`.tmx` et aperçus identiques ; seuls les chemins des planches changent si le dossier
  de sortie change). Chemins des planches normalisés (`../Cute_Fantasy_Desert/...` pour l'extension).
- Tiled 1.12 installé (`C:\Program Files\Tiled`, `tmxrasterizer.exe` rend une carte en PNG pour vérifier).
- Calques lus, **dessinés une seule fois** au démarrage : « Sol » + « Décors » dans une `RenderTexture` (profondeur 0),
  brouillard hors « Parc » dans une autre (3 ; `FOG.steps` marches superposées, une par case à partir de la deuxième :
  la lisière reste claire). Ombres (1,9). Nuit (4, cachée le jour), nom et « z » (6).
  « Collisions » : toute tuile = case bloquée, jamais dessiné. « Zones » : « Départ » (vue de départ : sa hauteur donne
  le zoom, son centre la position) et « Parc » (`ground.area` : les Pokémon y restent ; sans lui, « Départ »).
  `create(..., { map, size })` charge `{map}-{size}.json` (`MAP_URL` ; `parc-1` si `map` n'est pas un nom simple).
- **Feuillages triés** (`foliage`) : une tuile de « Devant » appartient au tronc = première case sous elle (même colonne)
  qui bloque ou n'a plus de feuillage. Profondeur `depthOf(ligne du tronc × 16 + 8,5)`, comme les Pokémon
  (`depthOf(pieds)`) : pieds au-dessus du milieu du tronc = derrière. Morceaux = feuillages d'une même ligne de troncs à
  moins de 4 cases d'écart (84 à 122 avec la forêt dense, texture de 1280 × 1272 au plus ; 0,04 ms par image sans
  Pokémon, mesuré le 2026-10-31), rangés par étagères dans une seule `DynamicTexture` « feuillages » (une
  `Image` par morceau). Dessin d'un morceau : toutes les tuiles du calque `visible = false` sauf les siennes,
  `setSkipCull(true)` (dessiné ailleurs qu'à sa place). Dans la forêt, deux arbres en quinconce se chevauchent : le haut
  d'un feuillage peut prendre le tronc d'un voisin (cases bloquées, aucun Pokémon : sans effet).
- `ParkMapTests` : calques en CSV, jeux de tuiles incorporés avec un simple nom de fichier, « Départ » dans la carte,
  toutes ses cases libres reliées (même règle de diagonale que `parc.js`).

## Plage (`outils/generer-plage.py`, `ParkLandscape.Beach`)
- `Plage_1-1.tmx` à `Plage_1-4.tmx` dans `C:\Sources\Parcs`, puis `carte-parc.py` → `plage-1-{taille}.json` (le nom vient
  du fichier). Mêmes arguments que `generer-parc.py` ; aperçus `plage-<n>.png` et `plage-<n>_collisions.png`.
- Types de cases vus sur le site le 2026-11-01 (zone de 40 × 31, la taille 2 depuis) : 806 eau, 418 sable, 16 aucun (le
  ponton).
- **65 % de mer, 35 % de sable** dans la zone des tailles 2 à 4 (choix de l'utilisateur, 2026-11-01 ; tolérance de
  `ParkMapTests.Beach_IsAboutTwoThirdsSea_AtEverySize` : 60-70 % de mer, 30-40 % de sable ; Eau heureux, Feu malheureux,
  Normal normal). Taille 1 = la vue de départ, moitié plage moitié mer : 52,5 % de mer (test : 51-55 %, les Eau encore
  heureux, plus de 50 %). Le script affiche les parts : 44,2 / 33,7 / 34,9 / 37,0 % de sable. Réglages : `COTE` (ligne de la
  côte, 20), `ZONES` (le haut de chaque zone placé pour que la côte en soit au tiers), anses aux deux bouts (moins de sable
  dans la dernière taille, que la bordure haute de palmiers enrichit en sable). Une ligne de `COTE` = ~1 à 3 points de
  sable selon la taille.
- La mer et l'écume (bord du sable, tuiles de bordure de `Beach_Tiles`) sont bloquées. **Zones choisies par
  l'utilisateur** (dans Tiled, 2026-11-01, puis reportées dans `ZONES`) : « Départ » (24 × 20), 40 × 31, 56 × 43, toute
  la carte, soit 8,2 / 8,0 / 10,2 / 13,2 cases de sable libres par Pokémon (le test en exige 7 pour la plage, 14 pour la
  forêt ; les types Eau nagent en plus dans la mer). Une zone déplacée dans Tiled : la reporter dans `ZONES` (arrondie à
  la case) et régénérer, sinon le sable et la mer hors de la nouvelle zone restent visibles et libres.
- Bordure de la carte entière (`bordure_carte`, visible à la taille 4) : `EPAISSEUR` = 4,4 ± 0,9 cases (2,4 avant le
  2026-11-01 : une seule rangée de palmiers, trouée). Une case de bordure sans palmier ni rocher reste du sable libre, et
  un palmier ou un gros rocher demande deux cases de profondeur : il faut ~4 cases pour en voir 2 à 3. Elle sert aussi de
  lisière aux objets : la changer déplace quelques objets à toutes les tailles.
- Bords du sable : le sable est la forme (`PLAGE`, premier des six groupes de `Beach_Tiles`, les autres sont des variantes
  de l'écume), prolongée hors de la carte (`hors_carte` : pas de bordure le long du bord). `nettoyer_cote` nettoie le
  sable puis la mer jusqu'à ce que rien ne change. Sol tiré case par case (`tirage`) : le même à chaque taille.
- Ce qui n'est pas acheté (`cache_de`) : cocoteraie en quinconce sur le sable (`Palm_Tree_2` de Desert, le droit 2 × 3 ou
  le penché 2 × 2 une case plus bas) avec des buissons, **récif** de gros rochers 2 × 2 d'`Outdoor_Decor` (colonnes 4-7,
  lignes 11-14) en quinconce dans la mer, petits rochers dans les trous et sur l'écume. Les îlots hors de la zone sont
  retirés du sable (rien à deviner) ; chaque îlot tient dans un seul anneau entre deux zones et porte un palmier.
- Ponton : `Wooden_Deck_Tiles` 0/1 et 5/6 (planches à cheval sur deux colonnes, fond transparent) dans « Décors » au-dessus
  du sable, de l'écume et de la mer ; ses cases sont débloquées (`debloque`), type de case « aucun ». Poteaux du bout
  (28/29) bloqués. Barque (`Boat`, lignes 1-2) à côté.
- Objets : grands palmiers (`Palm_Tree_1`, 3 × 4, pied bloqué en ligne 3 colonne 1), palmiers isolés (pied colonne 0),
  palmes tombées (2 × 2, on y marche), herbes des dunes, rochers du désert sur le sable, rochers dans la mer. Les noix de
  coco des planches de palmiers sont à cheval sur deux tuiles : pas utilisées.

## Volcan (`outils/generer-volcan.py`, `ParkLandscape.Volcano`, 2026-11-01)
- `Volcan_1-1.tmx` à `Volcan_1-4.tmx` dans `C:\Sources\Parcs`, puis `carte-parc.py` → `volcan-1-{taille}.json`. Mêmes
  arguments que les autres générateurs ; aperçus `volcan-<n>.png`. Zones du parc 1 (24 × 20, 40 × 30, 56 × 40, tout).
- Choix de l'utilisateur : **tout de type volcan** (la règle `Cute_Fantasy_Volcano/` de `carte-parc.py` ; le pont :
  aucun), **plateau et coulées**, lave pour les Feu « de temps en temps », cascades et bulles animées.
- **Modèle de l'utilisateur** (2026-11-01, après « un peu trop de lave ») : il a peint dans Tiled sa version de la
  taille 4, gardée dans `C:\Sources\Parcs\Volcan_1-4_modele.tmx` (puis sa version avec les deux ponts ;
  `Volcan_1-4.tmx` est réécrit par le générateur) : un
  **grand lac** en haut qui alimente trois cascades (gauche, départ, droite ; celle du milieu à droite retirée, il l'avait
  cachée sous des tuiles de « Devant »), la mare sous le pont agrandie, la mare de la cascade de gauche fondue avec sa
  voisine, une mare en bas au centre ; plus de mares à gauche ni en haut. Ses contours sont repris ligne par ligne
  (`bandes`) ; « corriger » = bords de lave, objets sous la lave, collisions. Comparer un modèle avec le générateur :
  différences calque par calque, puis rendu des deux (`tmxrasterizer.exe --hide-layer Collisions`).
- **Quantité de lave** : 16,9 / 19,7 / 30,8 / 18,7 % (la dernière avec la bordure ; 14 à 29 % de lave libre). Le grand
  lac déborde des zones 1 à 3 : sa lave est coupée à la lisière (on en voit le bas dès la vue de départ, au-dessus de la
  cascade). **Malentendu à ne pas refaire** (avant le modèle) : rétrécir toutes les mares de 15 % a été refusé, « pas
  réduire la surface des lacs, mais la présence totale de la lave ».
- **Règle de l'utilisateur pour toute cascade (eau ou lave, 2026-11-01) : jamais rien en amont ou en aval, « ça doit être
  logique »** : un lac au-dessus (ou la cascade collée à la bordure de la carte), et en aval une coulée ou un lac. Ici :
  `lac(cascade, rx, graine)` (4 lignes au-dessus, les 2 dernières pleines sur la largeur de la cascade, sans objet : un
  rocher de lave y barrait l'écoulement, rien n'est posé sur les 3 lignes au-dessus d'une cascade), coulée, mare. Vérifié
  par le script (lac, cascades, coulées et mares d'un seul tenant ; lac libre au-dessus et coulée en dessous de chaque
  cascade montrée) et par `ParkMapTests.Volcano_EveryCascade_HasALakeAbove_AndLavaBelow`.
- Planche `Volcano_Tiles` (29 colonnes) : sol = tuiles de roche craquelée qui se raccordent toutes (aucune tuile unie :
  `SOL_LISSE`, `SOL_CRAQUELE`, `SOL_BRULANT` fendue de lave près des coulées). Lave = la forme (`LAVE` : bords 181-241 avec
  la lueur rouge, coins rentrants 184/185/213/214, centre 211, croûtes 186-246) ; les piliers et murs de la planche (12-28,
  41-173) ne sont pas utilisés : ils ne se raccordent pas à la cascade.
- **Cascade** (`Volcano_Lavafall`, 54 colonnes : 18 images de 3 × 5 tuiles) : posée dans « Décors », bloquée, animée
  (`carte.animer`, 110 ms par image) ; le lac au-dessus déborde dans la cascade, la coulée part sous le bouillonnement. Pour les bords, la forme de la lave comprend les cases des cascades (`forme_de`).
- **Groupes** (le grand lac avec ses cascades, coulées et mares ; ou une mare seule) : un groupe tout entier dans la zone
  (`dans_la_zone`) est contourné par la lisière ; sinon sa lave est coupée à la lisière (`nettoyer(lave - cache)`), les
  cases retirées par le nettoyage portent un rocher (bloquées : elles redeviennent de la lave plus tard, une case libre ne
  doit pas être reprise). Une cascade n'est montrée que tout entière.
- **Toute case bloquée hors de la lave porte un décor** (le script s'arrête sinon, `ParkMapTests` le vérifie) : `parc.js`
  prend une case bloquée de type volcan sans décor pour de la lave libre. Les bulles sont dans le calque « Bulles », pas
  dans « Décors », pour cette raison.
- Ponts (`PONTS`, `Volcano_Bridge`, colonnes 3 à 7 : les poteaux sont à cheval sur deux cases) : celui de la coulée de
  départ, et deux posés par l'utilisateur dans Tiled (dans « Devant », le 2026-11-01 : repris dans « Décors ») sur les
  coulées de gauche et de droite. Coulée droite sur 4 lignes sous chaque pont (`sous_pont`), pas de lave aux deux bouts
  (`bouts_de_pont`), rien de posé devant ; tablier débloqué, seule la lave dessous bloquée : les poteaux des bouts
  laissent passer (le pont de droite commence sur la dernière colonne de la taille 2, où ses cases sont libres : des
  poteaux bloquants l'empêchaient d'être posé). Arbres de braise (`Volcano_Plants`) jamais à moins d'une case
  de la lave (`loin_de_la_lave` : un feuillage débordait sur une mare).
- Hors de la zone : champ de gros rochers 2 × 2 en quinconce (`Volcano_Rocks` : amas, buttes, tours), petits rochers
  ailleurs, toutes bloquées.

## Désert (`outils/generer-desert.py`, `ParkLandscape.Desert`, 2026-11-02)
- `Desert_1-1.tmx` à `Desert_1-4.tmx` dans `C:\Sources\Parcs`, puis `carte-parc.py` → `desert-1-{taille}.json`. Mêmes
  arguments que les autres générateurs ; aperçus `desert-<n>.png`. Zones du volcan.
- Choix de l'utilisateur : dunes, plateau et oasis ; cactus, plantes, rochers et acacias ; teinte 1 (planches `_1`) ; hors
  zone, cactus et rochers serrés ; zones du volcan. Puis (2026-11-03, « très laid ») : **aride, un seul point d'eau**, herbe
  seulement autour de l'eau, plateau plus petit et à deux étages, plus de végétation en haut. Sable 52 / 71 / 76 / 87 %
  (test : 50-56 / 68-80 / 68-80 / 82-90, et plus de la moitié).
- **Depuis le 2026-11-03 au soir, les quatre cartes sont retouchées à la main par l'utilisateur** (tailles 1 à 3 dans
  Tiled, acacias sans ombre « volants » remplacés partout par l'acacia avec ombre) : **ne plus relancer
  `generer-desert.py`** sur `C:\Sources\Parcs` (il les écraserait et pose encore l'acacia sans ombre une fois sur deux) ;
  après une retouche, seulement `carte-parc.py` sur chaque carte. Un acacia peint dans « Sol » laisse un trou : le
  ranger dans « Devant » (lignes 0-1) et « Décors » (tronc, ombre), sans le supprimer.
- **La taille 4 est sa carte** : il l'a retouchée dans Tiled (`Desert_1-4_modele.tmx` : marche du plateau ;
  `_modele2` : corniche, rebords ; `_modele3` : la version validée ; `_modele4` : la même avec des remous qu'il a refusés).
  Ses retouches sont reprises **telles quelles** dans le générateur (`SOL_FORCE`, `DEVANT_FORCE`, `BERGES`, corniche,
  marche) : `generer-desert.py --seulement 4` doit redonner `Desert_1-4_modele3.tmx` à l'identique (comparer calque par
  calque ; le 2026-11-03, identique octet pour octet). Toute retouche du générateur : refaire cette comparaison.
- **Leçons** (2026-11-03, plusieurs allers-retours ratés) : la planche des falaises du désert et les bords de plage ne se
  raccordent pas d'eux-mêmes ; les essais de raccord « à l'œil » sur des aperçus réduits ont été mauvais. Regarder chaque
  raccord agrandi (×6 à ×8) avant d'envoyer ; laisser l'utilisateur dessiner les raccords dans Tiled et les reprendre.
  Refusés : pointes de cascade entourées de rebords (blocs découpés), remous de `Desert_Water_Foam_Animation` (« immonde »),
  ronds de dunes (carrés clairs), prairies, deux petites oasis.
- Cascade : seule la chute (lignes 2, 3, 3 de la planche, 3 lignes du flanc ; les lignes 4-5 ont des cailloux, refusés).
  Le ruisseau se resserre à **une case** sur les deux lignes au-dessus (`GOULOTS`, posés après le nettoyage), avec les
  pointes de la cascade de part et d'autre (tuiles 1, 18, 20 dans « Sol », coins 79/81 dans « Devant ») ; une case de
  sortie en dessous, entre deux berges droites.
- Objet bloquant coupé par la lisière : son pied visible porte un petit décor bloqué (sinon il ne peut pas apparaître à la
  taille suivante, et la taille 4 perdait des acacias). Cacher l'objet entier se propageait d'objet en objet (moitié de la
  vue de départ cachée) : refusé.
- **Planches** (lues le 2026-11-02, aucun exemple fourni avec le pack) :
  - `Desert_Cliff_Tiles_1` (13 colonnes) : le **sol de sable** est dans cette planche (en bas à droite : 128 uni, 139/140
    à peine marqués, ronds de dunes 2 × 2 en (9, 5) et (9, 7)) : règle tuile par tuile `DESERT_CLIFF_TILES` de
    `carte-parc.py` (sable, escaliers = aucun, le reste falaise). Plateau rectangulaire seulement (pas de coin rentrant) :
    rebord **hors du dessus** (14 15 16 au-dessus, 27 et 29 sur les côtés : le dessus lui-même est transparent, le sable du
    sol), flanc de 3 lignes sous la dernière ligne du dessus (40-42, 53-55, 66-68, colonne du milieu répétée). Escaliers
    3 × 3 (22-24, 35-37, 48-50) à la place du flanc. Le groupe (colonnes 4-6) au rebord épais et 70/71 : pas utilisés.
  - `Desert_Cliff_Waterfall_1` : 6 images de 3 × 6 tuiles (18 colonnes), les pointes de roche à cheval entre deux images.
    Lignes 0-1 : le ruisseau entre deux pointes, faites pour avoir de l'eau des deux côtés ; 2-4 : la chute ; 5 : le pied
    (cailloux en 4 et 5). Animée à 120 ms. (Les variantes 2 et 3 n'ont que 5 lignes.)
  - Eau : bords dessinés **dans le sable** (`Desert_Beach_Tiles_1`, le sable est la forme, coins rentrants 3/4/8/9),
    eau unie `Desert_Water_Tiles_1` 10. Le bord (écume) est bloqué, les Eau y nagent comme à la plage.
  - Herbe `Desert_Grass` : son bord passe **au milieu des cases** : tuile choisie d'après les quatre coins (`HERBE`),
    pleine (11) dans « Sol », bords dans « Décors » (aucun objet sur un bord d'herbe : `bord_herbe`). Anneau autour de
    l'eau (`anneau`, distance à l'eau), mesuré depuis l'eau d'avant la berge de la corniche (dessin validé).
  - Petit rebord sans flanc (79-81, 92, 94, 105-107) : `carte-parc.py` lui fait garder le type du dessous.
  - Cactus de `Cactus.png` : 2 × 2, **à cheval sur deux colonnes** (colonnes 2k et 2k+1), haut dans « Devant ». Acacias
    5 × 4 (avec l'ombre) ou 5 × 3, tronc en colonne 2.
- **Piège** : avec des bords dans le sable, une eau qui apparaît à l'agrandissement rend son bord bloqué. Le sable au bord
  de l'eau encore cachée est donc bloqué (un rocher) dès la taille d'avant ; sinon le script s'arrête (cases libres perdues).
- Morceaux d'herbe (celle du lac, celle de l'oasis) montrés seulement si 60 % au moins sont hors de la lisière, avec une
  case de sable entre l'herbe et ce qui est caché (sinon 49,6 % de sable dans la vue de départ : Sol pas heureux).
- Vue de départ : 11,3 cases libres par Pokémon (`ParkMapTests` : 11 au désert, 14 ailleurs sauf la plage).
- Essai (première version, 2026-11-02 (instance de test, achat dans la vraie page puis 15 min simulées aux tailles 1, 2 et 4) : nageurs
  47-54 % du temps dans l'eau, volants 7-15 % au-dessus, marcheurs jamais, aucune case interdite ; types de cases lus par
  `parc.js` : flanc et rebord falaise, escaliers aucun, eau et cascade eau, herbe végétation.

## Océan (`outils/generer-ocean.py`, `ParkLandscape.Ocean`, 2026-11-03)
- `Ocean_1-1.tmx` à `Ocean_1-4.tmx` dans `C:\Sources\Parcs`, puis `carte-parc.py` → `ocean-1-{taille}.json`. Mêmes
  arguments que les autres générateurs ; aperçus `ocean-<n>.png`. Zones du volcan.
- Choix de l'utilisateur : **pas de plage** ; **seuls les nageurs** viennent (« les autres ne peuvent pas venir » ; pas les
  volants seuls), grisés dans la fenêtre de choix avec la raison ; des **îlots pas bloqués** ; récif de rochers autour ;
  rochers animés dans l'eau. Pas choisis : poissons animés, barque et rondins. Première version (~90 % d'eau, 20 îlots,
  remous d'écume) refusée : « beaucoup trop d'îlots, ça doit être un océan » → **trois îlots, aucun dans la vue de
  départ**, plus d'écume (2026-11-03). Eau : 100 / 99 / 96,7 / 98,2 % (test : 95 % au moins).
- **Sans terre** (taille 1) : `Ground.openLiquid` part de la case d'eau la plus proche du centre de « Départ » (sinon
  aucune eau libre : elle est comptée depuis la terre) ; `randomSpot(LAND)` rend de l'eau, `destination` toujours l'eau.
  Les tests partent aussi de ce centre. À la taille 2, l'îlot de gauche, coupé, n'a que 12 cases de terre : des
  nageurs s'y serrent parfois.
- **Îlots** : `Water_Stone_Tile_1` (même disposition que l'herbe du désert : le bord passe au milieu des cases, tuile
  d'après les quatre coins d'herbe, `RIVAGE`) + `Grass_1_Middle` (même vert). Case à 3 coins d'herbe ou plus (tuiles 0,
  2, 6, 8, ou herbe pleine) = terre libre, type végétation (`WATER_STONE_TILES` de `carte-parc.py`) ; 1 ou 2 coins = eau
  bloquée sans décor, où l'on nage. Fleurs sur l'herbe pleine. Placés **à la main** (`ILOTS_PLACES` : centre, rayons ; un ovale plus un ou deux lobes,
  nouvelle graine tant qu'il touche un voisin à moins de 3 cases ou la bordure à moins de 2). Les anneaux entre deux zones
  sont trop minces pour y tenir un îlot : un îlot qui déborde de la zone est **coupé à la lisière** (`nettoyer(îlot -
  caché)`, bouts de moins de 4 cases de terre retirés) ; un agrandissement ne fait que gagner des coins d'herbe (le script
  vérifie qu'aucune case libre n'est perdue). Le hasard (bruit fort, îlots tirés au hasard) donnait des bras fins sans terre
  et trop peu de terre.
- Les îlots sont séparés : le test d'accès de `ParkMapTests` (et le script) passe par la terre **et l'eau libre** pour
  l'océan ; cases libres par Pokémon : terre + eau libre (20 à 45).
- Écume retirée (`Water_Foam_Animation`) : son eau est (0, 152, 220), la mer (0, 149, 233), un carré se voyait ; une
  copie recolorée la raccordait (si elle revient).
- Rochers animés (`Water_Rocks/Rock_*_Water_Anim`, 8 images d'une tuile, 150 ms), bloqués, à 2 cases au moins des îlots.
  « Enlève 30 % des rochers » (2026-11-03) : 3 sur 10 retirés **dans chaque anneau** entre deux zones (sur toute la carte
  d'un coup, la vue de départ n'en perdait aucun), les autres gardent leur place : 5 / 11 / 23 / 49 rochers.
  Toute case cachée porte un rocher (aucune case bloquée sans décor hors de l'eau libre, comme au volcan).
- Essai du 2026-11-03 (instance de test, achat et choix dans la vraie page, puis 15 min simulées aux tailles 1, 2 et 4,
  20 nageurs) : taille 1, 100 % du temps dans l'eau, répartis sur toute la vue ; tailles 2 et 4, 72-77 %, tous montés sur
  un îlot ; aucune case interdite, la nuit endormis dans l'eau ou à terre. Plage inchangée (67 %).

## Lave et tuiles animées (`parc.js`)
- `LAVA` (4) dans `Ground.walkable`, `LIQUID` = `WATER | LAVA`. `openLiquid(…, habitat, bit)` (l'ancien `openWater`) :
  une fois pour l'eau (5), une fois pour la lave (7) ; `ground.lava`, `ground.liquid` (survol), `liquidAt(x, y)`.
- `ParkHabitats.MovementOf` : « swim », « lava » (Feu, Feu/Vol compris), « swim-lava » (Feu/Eau), « fly », null.
  `Pokemon.reach` / `pass` / `bed` (où il dort : jamais la lave ; fatigué dans la lave, il marche vers `bed` avant de
  dormir). `destination` : eau 2/3 pour un nageur, puis lave `LAVA_BATH.share` (1/4) pour un Feu. Arrivée au parc : sur
  la terre pour un Feu. `wet` pour l'eau comme pour la lave ; `p.liquid` choisit les remous (`rippleOf(anim, lava)` : dorés).
- Essai du 2026-11-01 (15 min simulées, volcan taille 2) : Feu 21-22 % du temps dans la lave, volants 10 % au-dessus sans
  s'y arrêter, autres jamais, aucune case interdite ; 10 min de nuit : aucun Feu endormi dans la lave ; plage : nageurs
  67 %, volants 14 % (comme avant).
- **Tuiles animées** (`animatedTiles`, `animate`) : animations de Tiled lues dans `tilesets[].tiles[].animation` ; une
  `Image` par tuile animée de « Sol », « Décors » et « Bulles » à `ANIMATED_DEPTH` (0,5), frames ajoutées à la texture de la
  planche (`tuile<n>`) ; `setFrame` seulement quand l'image change. Bulles : un décalage au hasard chacune. Le calque
  « Bulles » n'est jamais dessiné dans la carte. Vu dans le navigateur : la cascade change d'image d'une capture à l'autre.

## Types de cases et bonheur
- Feuille Google « Gestion du parc » de l'utilisateur (id `1m_jWFdXRigcS3tdz2UUDggTtE8cK-1avN_mBvMoYoOE`, connecteur
  Google Sheets), onglet « Habitat » : 7 types de cases × 18 types de Pokémon, « o » aime, « x » n'aime pas. Recopiée
  dans `ParkHabitats.Sheet` (une chaîne par type, colonnes dans l'ordre de la feuille) : la feuille change → la
  recopier, et `ParkHabitatsTests.Sheet_MatchesTheSpreadsheet`. Autres onglets : « Taille », « IA » (à venir).
- Détection **par planche** (choix de l'utilisateur, pas de calque peint) : `HABITAT_RULES` de `outils/carte-parc.py`,
  première règle dont le motif est dans le chemin de la planche ; `DECOR` (objets, arbres, fleurs) = garde le type du
  dessous ; « Sol » puis « Décors », pas « Devant ». Planches d'herbe à bords (`Grass_Tiles_n`) : chemin de terre
  (80-82, 96-98, 112-114, 128, 129, 144, 145) et escaliers = aucun, flancs des hauteurs = falaise. Chemins = aucun (choix
  de l'utilisateur). Les règles des extensions (désert, volcan, donjons, ShroomLands) et des plages sont **devinées
  d'après les noms**, pas encore vues sur une carte : les vérifier avec « Habitats (debug) » à leur première utilisation
  (volcan : vu le 2026-11-01, tout en volcan, le pont en aucun ; désert : vu le 2026-11-02, voir « Désert »).
- Champ `habitats` du JSON (pas un calque : Phaser l'ignore) : une chaîne par ligne, chiffres 0 aucun, 1 sable, 2 cave,
  3 falaise, 4 végétation, 5 eau, 6 donjon, 7 volcan = enum `Habitat` = `Ground.habitats` de `parc.js`.
- L'eau est bloquée : aucun Pokémon ne s'y tient. D'où un bonheur selon ce que contient le parc
  (`ParkHabitatMap.Counts`, cases bloquées et sans type comprises : choix de l'utilisateur), pas selon sa case.
- `ParkHabitats.MoodOf(type1, type2, map)` (règles de l'utilisateur, 2026-10-31) : malheureux si un habitat détesté
  couvre ≥ 50 % de la zone, sinon heureux si un habitat aimé couvre > 50 % (seuils asymétriques voulus), sinon normal ;
  les deux à la fois (50/50 pile) : malheureux. Double type : « aime » l'emporte sur « n'aime pas » (choix de
  l'utilisateur, pas d'annulation). Types : `BankPokemon.Type1`/`Type2` (deux fois le même pour un seul type).
- Fiche (`Park.razor`, `OnPokemonClicked`) : `PokemonCard` avec `Portrait` (remplace le sprite, 40 px doublés,
  `data-remplacement` = le sprite) et `Summary` (la raison, `ParkMoodResult.Message`). Ma collection et la Living dex ne
  passent ni l'un ni l'autre. Visiteur : même humeur (parc du joueur, sa taille).
- Portraits : `images/donjon/portraits/<dossier SpriteCollab>/{Normal,Happy,Sad}.png` (`DONJON_PORTRAITS` de
  `images.py`, 8 578 fichiers le 2026-10-31). `PokemonSprites.DonjonPortraitUrl` : premier dossier de
  `DonjonCandidates` (comme `DonjonFolder`) qui a l'expression ou Normal (un dossier de forme sans Happy donne son
  Normal, pas le Happy de l'espèce). Testé avec une liste de fichiers (`ChoosePortrait`) : la CI n'a les portraits
  qu'après le déploiement suivant.
- Coût : cartes lues une fois au démarrage (`ParkHabitats.Load` dans `Program.cs`), comptes faits une fois ; dans le
  navigateur un `Uint8Array`, `habitatAt` sans déchets ; couche de debug (`showHabitats(colors)`, profondeur 3,2)
  dessinée une fois dans une `RenderTexture`, redonnée par `Park.razor` après chaque redémarrage du parc.

## Affichage (Phaser 3.90.0, `wwwroot/lib/phaser/phaser.esm.min.js`)
- Importé par `parc.js` (module ES, CSP : rien à ajouter ; `img-src blob:` déjà permis pour le chargeur de Phaser ; son
  `new Function("return this")` est dans un `try`). Phaser 4 existe (4.2.1 le 2026-10-30), pas adopté.
- Netteté : canvas en pixels physiques (`scale.zoom = 1 / devicePixelRatio`, `Scale.NONE`), caméra à un zoom entier `k`
  (hauteur = `k × 320`, la zone « Départ ») : chez l'utilisateur `k` = 3. Sprites : `spriteK = round(k × 2/3)` pixels
  d'écran par pixel de sprite (jamais moins de 2 si `k` ≥ 2), positions arrondies au pixel d'écran (`snap`). Il a voulu
  les sprites 20 % plus petits que ×3 ; 2,4 n'étant pas net, il a choisi ×2. Ne pas revenir à un étirement.
- Tous les Pokémon à la même taille : des tailles selon l'espèce (ratio de 1 à 4 d'après la taille réelle, feuille
  Google de l'utilisateur) ont été essayées puis abandonnées le 2026-10-31 (« ça ne me plaît pas »). Ne pas les reproposer.
- Glisser : au-delà de 6 px CSS, un appui devient un glissement (pas de clic) ; `pointerupoutside` aussi ; vue bornée par
  la caméra à la zone « Parc » plus `VIEW_MARGIN` (6) cases de forêt (`applyZoom`). `touch-action: none` sur le canvas ; `.park-empty` laisse passer les appuis (sauf son bouton).
- Zoom (`zoomTo`, `applyZoom`) : crans entiers de ×1 à `maxZoom` (zoom de départ), point visé gardé fixe. Limites de la
  caméra recalculées à chaque zoom : Phaser colle la carte à gauche quand la vue est plus large qu'elle (`clampX`), d'où
  des limites élargies et centrées. Hors carte : `OUTSIDE` (couleur de fond) ; sous la carte : rectangle `GRASS`.
- Boutons `.park-controls` créés par `parc.js` dans le conteneur (DOM, pas Blazor ; icônes SVG de Bootstrap Icons en
  `innerHTML`, pas de script : CSP respectée). Plein écran : `requestFullscreen` sur le bloc `.park` (option `fullscreen` de
  `create`, `parkBlock` de `Park.razor`) et non sur le conteneur de la carte : la fiche d'un Pokémon (`.collection-dialog`,
  rendue dans `.park`) y reste visible (demande du 2026-10-31 : hors du bloc, elle était cachée en plein écran). CSS
  `.park:fullscreen .park-stage`. `fullscreenchange` → `resize` (zoom de départ recalculé sur la hauteur de l'écran).
  Molette : zoom dès que la souris est sur le canvas, en plein écran ou non (demande de l'utilisateur ; la page ne défile
  plus sous la souris), cumulée par 100 (pavé tactile). Pincement : `activePointers: 2`, un cran par écart ×1,35.
- **Le panneau de navigateur de l'app refuse le plein écran** (« Permissions check failed », même sur un vrai clic) : pour
  l'essayer, simuler (`Object.defineProperty(document, "fullscreenElement", …)` sur le bloc `.park`, mis en `position: fixed; inset: 0`,
  puis événement `fullscreenchange`), ou demander à l'utilisateur.
- Textures : planches de SpriteCollab ajoutées à Phaser (`textures.addImage`, une image par case) ; textes à
  `resolution: k` (nets à la caméra).

## Déplacer un Pokémon (`parc.js`, propriétaire)
- `create(container, component, { canMove })` : `Park.razor` passe `canMove = !Visitor`. Rien côté serveur (positions
  jamais enregistrées).
- `pointerdown` sur un Pokémon (`at`) : `drag.target`. Souris : au-delà de `DRAG_THRESHOLD`, `pickUp` au lieu de faire
  glisser la vue. Doigt (`pointer.wasTouch`) : `tick` soulève après `LONG_PRESS` sans avoir bougé ; avant, un glissement
  fait défiler la vue. Clic sans bouger : fiche, comme avant.
- `held` : `{ p, from, dx, dy, pointer }` ; état `"held"` (animation `Charge` par `ANIMATION_OF`, `OPTIONAL` : planche
  absente = Idle ; `update` ne fait rien), dessiné `LIFT` px plus haut, profondeur `HELD_DEPTH` (3,5 : devant le
  brouillard, sous la nuit). `carry` à chaque image (Vector2 gardé : pas de déchets) suit le pointeur même quand la vue
  défile (`edgeScroll`, `EDGE_SCROLL`). `marker` (Graphics redessiné seulement quand la case change de permise à
  interdite) : `ground.isFree`. `putDown` : case libre, sinon `from` ; puis `rest(2 à 4 s)`. Pincement : `putDown(true)`.
- Essais : `MouseEvent` (`mousedown`, `mousemove` sur le canvas, `mouseup` sur `window`) ; doigt : `resize_window`
  « mobile », recharger la page, `new Touch(...)` + `TouchEvent` sur le canvas. Panneau masqué : la caméra n'est à jour
  qu'après un dessin (capture d'écran) : sans, `getWorldPoint` vise à côté et le Pokémon n'est pas pris ; l'appui long
  attend aussi une image (`tick`).

## Nage et vol (`parc.js`, demande du 2026-11-01)
- `ParkHabitats.MovementOf(type1, type2)` → `ParkPokemon.Moves` : « swim » si l'un des types est Eau, sinon « fly » si
  l'un est Vol, sinon null (un Eau/Vol nage). Lévitation et Spectre : pas pour l'instant (à proposer si l'utilisateur
  le demande). `setPokemon` met aussi à jour `moves` des Pokémon gardés.
- `Ground.walkable` est un masque : `LAND` (1, terre libre), `WATER` (2, eau libre), 0 bloqué pour tous. Eau libre
  (`openWater`, calculée au chargement, sans toucher aux cartes) : case bloquée sans tuile dans « Décors » ni « Devant »
  et de type eau (5), plus les cases bloquées sans objet qui la touchent (8 voisines : l'écume de la plage, de type
  sable), seulement si c'est relié par les côtés à la terre libre (pas de flaque entre les rochers du récif). Les îlots :
  leur bord est de l'écume (on y nage), l'intérieur reste bloqué.
- Chaque Pokémon : `pass` (cases où il passe : `LAND | WATER` s'il nage ou vole) et `reach` (où il s'arrête : l'eau
  seulement pour un nageur). `nearestFree`, `path`, `lineFree`, `isFree` prennent un masque (`Ground.mask` garde celui
  de la recherche en cours pour `canStep` et `smooth`, sans fonction créée). `walkTo(x, y, intent, reach)`.
- Nageur : `SWIM.waterShare` (2/3) de ses promenades et de ses arrivées au parc dans l'eau (`randomSpot(WATER)`), y
  dort. Volant : arrêté au-dessus de l'eau (clic, chemin barré), `decide` l'envoie d'abord se poser ; pas de petite pause
  en chemin au-dessus de l'eau ; `FLY_OVER` (30 %) de ses promenades sont un **survol** (intent « soar » vers une case
  d'eau, puis directement vers la terre) : sans ça, sur la plage (terre d'un seul tenant), il ne passait au-dessus de
  l'eau que 3 s sur 900. `preyOf` : seulement une proie là où l'attaquant peut s'arrêter. `putDown` et le cadre :
  `reach`.
- Rendu : `wet` (0 à 1) et `altitude` (0 à `FLY_HEIGHT` = 10 px de carte) suivent en douceur dans `step`. Dans l'eau :
  sprite descendu de `SWIM.sink` (4) pixels de sprite, `setCrop` à la ligne des pieds (hauteur `fh − wet × (fh/2 − 6 +
  sink)`, `setCrop()` en sortant), `rippleOf` (deux ellipses claires, texture faite une fois par taille comme
  `shadowOf`) qui pulse avec `clock`. Volant : sprite monté de `altitude`, ombre plus petite restée sur l'eau ; `at`
  (clic) et le nom suivent l'altitude.
- Essai (2026-11-01, 15 min simulées sur la plage taille 2, puis parc 1 tailles 1 et 4) : nageurs 61 à 83 % du temps dans
  l'eau, volants 13 à 27 % au-dessus sans jamais s'y arrêter, marcheurs jamais, aucune case interdite (`simulate` rend
  `moves`, `water`, et `free` selon `pass`). Charger la plage sans la brancher : remplacer `window.fetch` dans la page
  (`parc/parc-1-N.json` → `parc/plage-1-N.json`) avant `import("/js/parc.js?v=…")`.

## Animation et comportement (`parc.js`)
- **Malheureux** (demande du 2026-10-31) : humeur dans `ParkPokemon.Mood` (« happy », « normal », « sad »,
  `ParkService.ListAsync` d'après la carte de sa taille), mise à jour des Pokémon gardés par `setPokemon`. Réglages `SAD`
  (attaque toutes les 45 à 90 s, nouvel essai après 5 à 10 s sans cible à portée `reach` = 8 cases, boude toutes les 30
  à 50 s : fréquence « modérée » choisie par l'utilisateur), `STRIKE_RANGE`, `FLEE`. Cibles (`preyOf`) : éveillées **ou
  endormies** (choix de l'utilisateur), pas sa famille, ni portées, ni déjà frappées ou en fuite. `charge` → marche
  (intent « attack », ×1,3) → `strike` (suit la proie 3 fois au plus si elle a bougé) → `play("attack")` et
  `prey.hit` (a mal au milieu de l'attaque) → `hurt` → `play("hurt", flee)` → intent « flee » (×2, sans pause en
  chemin). États joués une fois : `ONCE` (`play`, dernière image gardée, `then` ensuite). Soulever un Pokémon annule
  `prey`, `hit` et `then`. Debug : `attack(id)` (le plus proche, famille comprise, quel que soit le bonheur), bouton
  de la fiche dans `Park.razor`. Planches Attack / Hurt absentes : Idle (`OPTIONAL`).
- **Cache du navigateur** (2026-10-31, « les animations ne se jouent pas ») : `/images` n'avait aucun `Cache-Control`,
  le navigateur gardait l'ancien `AnimData.xml` (sans Attack ni Hurt) des Pokémon déjà vus, et jouait Idle. Depuis :
  `loadSheets` demande `AnimData.xml?animations=<ANIMATIONS>` (nouvelle adresse dès que la liste change) et `Program.cs`
  met `no-cache` sur les `.xml` et `.json` de `/images` (middleware : en développement, c'est `MapStaticAssets` qui sert
  ces fichiers, `OnPrepareResponse` de `UseStaticFiles` n'est pas appelé). Une animation ajoutée : vérifier dans les
  requêtes réseau que ses planches sont demandées pour un Pokémon **déjà vu** avant.
- Unités : pixels de la carte (16 par tuile). Réglages en tête du fichier (`ENERGY`, `WALK_SPEED`...) et dans `decide` /
  `update` ; distances divisées par 1,5 lors du passage à la carte (anciennes unités : 1,5 par pixel).
- `Ground` : cases libres, `nearestFree`, `path` (A* 8 directions sans couper les coins, lissé en lignes droites,
  vérifiées tous les 4 px). En marchant, une case bloquée n'est jamais franchie (glisse le long, sinon s'arrête).
- Fluidité : la vitesse rejoint progressivement celle voulue, freine à l'arrivée ; l'animation de marche suit la vitesse.
- **Pas de déchets à chaque image** (l'utilisateur a senti des saccades : ramasse-miettes) : 2 Mo/s à 144 Hz suffisent à en
  causer. Mesuré le 2026-10-30 (30 Pokémon, 2750 × 960) : calques de tuiles redessinés + ombres en `Graphics` = 50-60 Ko
  et 0,6 ms par image ; carte en `RenderTexture`, ombres en images toutes faites (`shadowOf`, adoucies, en pixels
  d'écran), profondeur changée seulement au changement de ligne, nom redessiné seulement quand il change, heure lue une
  fois par seconde, A* sur des tableaux gardés (`Ground`) = 2-5 Ko et 0,18 ms. Ne pas remettre de `Graphics` vidé et
  redessiné à chaque image, ni de calque de tuiles affiché.
- Sprites absents (images pas récupérées) : le Pokémon n'apparaît pas, sans erreur (`loadSheets` échoue sur un
  `AnimData.xml` en 404 ou sans Idle ni Walk ; avant le 2026-10-31, un 404 donnait des animations vides et `addTextures`
  plantait). Planche `Charge` absente (images d'avant le 2026-10-31) : Idle quand on le soulève.
- **`setPokemon` est asynchrone** (planches à charger) : les Pokémon retirés sont détruits **et** sortis de `pokemon`
  avant l'attente. Le 2026-10-31, ils restaient dans la liste pendant le chargement : `draw` sur un objet détruit
  (« this.scene is undefined » dans `setTexture`) arrêtait la boucle de Phaser, parc figé (Firefox, choix qui retire et
  ajoute beaucoup de Pokémon). Une liste plus récente reçue pendant le chargement l'emporte (`lists`). Essai : `setPokemon`
  sans l'attendre, puis `simulate(0.05)` pendant le chargement (le panneau masqué ne dessine pas de lui-même).

## Tester
- **Après chaque modification de `parc.js`, recharger le parc et lire la console** : `node --check` ne voit que la
  syntaxe. Une constante passée par erreur dans un commentaire (`NIGHT is not defined`, 2026-10-30) arrêtait `build` :
  carte collée à gauche, aucun Pokémon, chez l'utilisateur.
- `place(id, x, y)` (essais) : pose un Pokémon immobile ; pour vérifier un tri, en poser un derrière et un devant un arbre
  puis agrandir une partie du canvas dans un `<canvas>` en `imageSmoothingEnabled = false` (`view()` donne la caméra :
  pixel du canvas = (carte − `view().x`) × `zoom`), juste après un `requestAnimationFrame`.
- Panneau du navigateur de test souvent **masqué** : Phaser ne dessine que pendant les captures (`camera.worldView` pas à
  jour : `view()` la recalcule). Pour juger le comportement : `import("/js/parc.js?v=…")` dans la page, `create` sur un
  `div` (en `position: fixed`) avec un faux composant `{ invokeMethodAsync }`, `setPokemon`, puis `simulate(secondes,
  heure)` (état, `free` : jamais sur une case bloquée) et `view()` (caméra). Clic et glissement : `MouseEvent` sur le canvas
  (`mouseup` sur `window` = relâché dehors). Netteté : copier le canvas juste après un `game.step(...)` (pas de
  `requestAnimationFrame` : panneau masqué) et compter les blocs `k × k` uniformes. Coût et déchets : exposer `game`
  le temps de l'essai, enchaîner des `game.step(performance.now(), 16.7)` (fin par un `gl.readPixels` pour attendre le
  GPU), déchets par `performance.memory.usedJSHeapSize` relevé tous les 50 pas (après un `setTimeout`, valeurs
  grossières : répéter).
- Sans les images : faux fichiers `MonsutaPotaru/wwwroot/images/donjon/<0001…1025>/AnimData.xml` (contenu `<AnimData/>`)
  pour que le parc accepte des Pokémon ; supprimer `wwwroot/images` après (vérifier d'abord qu'il ne contient que ces
  faux fichiers). Pour `ParkServiceTests` : `PokemonSprites.Load` sur un dossier temporaire du même genre, le temps de
  l'essai, puis remettre le test (`Sprites_AndFamilies` exige les vrais fichiers).
- Fenêtre de choix : « Seulement ceux du parc » fige la liste des choisis à l'allumage (un Pokémon décoché reste affiché) ;
  « Tout retirer » ne change rien avant « Valider » ; « Vider le parc » : `ParkService.ClearAsync`, après confirmation.
