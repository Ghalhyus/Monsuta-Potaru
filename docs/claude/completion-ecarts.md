# Complétion : écarts entre les check-lists et le site (2026-11-16)

Ce que les check-lists de l'utilisateur (dossier Drive « Complétion », voir `completion.md`, « Check-lists ») listent et
que le site ne compte pas. Pas de check-list pour Rouge / Bleu / Jaune.

**Méthode** (à refaire de la même façon après un changement des tables) : étapes du site extraites par le vrai code (un
petit projet console qui appelle `GenNCompletion.Steps` sur une sauvegarde vierge, `epee.sav` pour Épée / Bouclier),
puis comparées ligne à ligne aux CSV : CT / CS / DT par leur numéro, Pokémon par leur nom (liste française de PKHeX),
le reste par les mots en commun (une ligne est couverte dès que la moitié de ses mots, ou sa récompense, est sur le
site). Relu à la main ensuite. Limites : une ligne dont le Pokémon apparaît ailleurs sur le site peut passer pour
couverte (casinos), et les étapes d'histoire « de déroulement » (choisir son starter, une scène) ne sont pas recensées
une à une.

Légende : **oubli** = cadeau ou rencontre avec un drapeau probable, à corriger ; **pas compté** = écarté par choix ou
faute de trace connue (voir `completion.md`), listé pour mémoire.

**Correction des oublis : remise à plus tard** (2026-11-16, l'utilisateur). Questions posées, encore sans réponse, à
reposer avant d'écrire du code (recommandation en premier) :
1. Ordre : génération par génération en commençant par la 4e (CS / CT offertes, HG / SS, Meneurs) ; ou tout d'un coup ;
   ou d'abord les étiquettes et les CS.
2. Pokémon sans drapeau propre (fossiles, Métamorph de la Clique des 5…) : par le Pokédex, comme les chiens errants ; ou
   seulement avec un drapeau. Pokémon de casino : de côté (déjà sauvages ailleurs).
3. CT achetées ou gagnées : par possession en 5e génération seulement (elles ne s'usent pas) ; aussi de la 2e à la 4e
   (une CT apprise s'y use) ; ou pas du tout.
4. Zarbi de Tanoby : par les Pokémon de la sauvegarde, comme les rubans ; une étape par le Pokédex ; ou de côté.
Proposé sans question : une étape par Meneur et par photo (argent, or) en Platine et HG / SS, comme la check-list.
Les sources des générateurs (clones pret, listes de FlagsEditorEX, ROM de Noire) ne sont pas dans le dépôt : les
retélécharger (docstring de chaque `outils/completion_genN.py`).

## Ce qui manque presque partout
- **CT achetées ou gagnées** (boutiques, casinos, Points Combat, PWT, Métro de Combat), de la 2e à la 5e génération :
  aucune trace d'achat ; à partir de la 6e, les CT sont comptées par possession (`K:n`) et rien ne manque. Le compter
  aussi en 2e–5e génération par possession est possible (les CT ne s'usent pas en 4e et 5e génération ; en 2e et 3e,
  elles s'usent : une CT apprise redeviendrait « à faire »).
- **Pokémon des casinos** (Or / Argent, Cristal, Rouge Feu / Vert Feuille, HG / SS) : aucun.
- **Fossiles ranimés** (Diamant / Perle, Platine, HG / SS, Noire / Blanche, Noire 2 / Blanche 2, Rubis Oméga /
  Saphir Alpha, Soleil / Lune, Ultra) : seulement par le Pokédex.
- **Combats contre le rival et les chefs d'équipe** sans drapeau (scénarisés) : pas d'étape, sauf en 2e génération.
- **Tuteurs de capacités** (ultimes des starters, tessons, Points Combat), **rubans de Claudie**, loteries, activités
  quotidiennes, Pokéwalker, transferts (Parc des Amis, RAdar Pokémon), distributions non retenues : pas comptés.
- Starters reçus : comptés par le Pokédex seulement (pas d'étape « Recevoir son premier Pokémon » à partir de la 5e).

## Or / Argent, Cristal
- **Oubli** : CT27 Retour / CT21 Frustration offerte le dimanche au Centre Commercial de Doublonville (selon le
  bonheur).
- Pas compté : CT achetées ou du casino (14, 15, 17, 21, 25, 27, 32, 33, 38, 41, 48), CT09 (tenue par les Abra venus de
  la 1re génération), Pokémon du casino (dont Qulbutoké et Embrylex de Cristal), Œuf Bizarre (impossible sur cartouche
  française), Maîtres des capacités du casino (Cristal), Concours de Capture, Show du Bon Numéro, Tour de Combat de
  Cristal, Nina, Julot, l'homme aux 8 badges de Kanto (PV Plus), CPN Radio et CPN Expan., quiz de la Tour Radio.

## Rubis / Saphir, Émeraude
- **Étiquette fausse** : en Rubis / Saphir, la rencontre de la Grotte Origine s'appelle « Groudon » (espèce 383) même
  dans Saphir (Kyogre) : l'écrire « Groudon (Rubis) ou Kyogre (Saphir) ».
- **Oublis probables** : échanges de Mérouville (Parecool → Makuhita) et de Pacifiville (Joliflor → Corayon) en Rubis /
  Saphir ; starter de Johto du Prof. Seko (Émeraude) ; Queulorior de la Grotte Atelier (Émeraude) ; CT21 Frustration de
  Pacifiville.
- Pas compté : CT achetées ou du casino (14, 15, 16, 17, 20, 25, 33, 35, 38), séries de la Tour de Combat (objets ;
  les rubans sont comptés), Tentes de Combat, tuteurs de la Zone de Combat, Mont Dresseurs (une partie), poupées
  (Lavandia, Route 114), Île Mirage, Grotte Métamo, nouvelles zones du Parc Safari, objets de l'histoire (Mouchoir Soie,
  Grelot Zen, Lunettes Sable, Passe Bateau, Méga Canne…), famille de la Route 111 (Brac. Macho).

## Rouge Feu / Vert Feuille
- **Oublis** : le **Conseil 4 un par un** (Olga, Aldo, Agatha, Peter, puis le Maître) et la nouvelle Ligue (seul le
  Panthéon compte) ; les **Ruines Tanoby** (7 chambres, 28 Zarbi : aucune étape, alors que les Zarbi sont comptés en 2e,
  4e et 8e génération) ; échange de la Route 2 (Abra → M. Mime).
- Pas compté : Raikou et Suicune listés à part (un seul chien erre, selon le starter : une étape, par le Pokédex) ; CT du casino et du toit du Centre Commercial (10, 13, 15, 16, 20, 23, 24, 30, 33, 35), Pokémon du casino,
  Tour des Dresseurs (3 modes sur 4), étapes des Îles Sevii sans drapeau (gang des motards, Grotte de Glace, Thémis, Œuf
  de Régis, Carte de Kanto…).

## Diamant / Perle
- **Oublis** : CS01 Coupe (Cynthia) ; CT64 Explosion (Voilaroc) ; fossiles du Musée de Charbourg.
- Pas compté : CT achetées, du casino ou en Points Combat (08, 15, 16, 17, 20, 22, 33, 44, 52, 54, 58, 61, 68, 73, 74,
  75, 83) ; Manaphy, Phione, Darkrai, Shaymin, Arceus (distributions non retenues pour Diamant / Perle) ; trophées de la
  Tour de Combat (20, 50, 100 victoires) ; rubans de Claudie ; tuteurs ; Baie du jour ; expressions à la mode ;
  revanches du week-end ; objets de l'histoire (Journal, Coffret Mode, Cherche VS, Explorakit, Master Ball d'Hélio).

## Platine
- **Oublis** : CS01 Coupe, CS03 Surf, CS04 Force ; fossiles du Musée de Charbourg ; **les quatre Meneurs de la Zone de
  Combat** (Usine, Castel, Scène, Arcade, argent puis or, et les Photos de Koner : aucune étape, alors qu'HG / SS en a
  une).
- Pas compté : CT achetées, du casino ou en Points Combat (08, 10, 15, 16, 17, 20, 22, 33, 44, 52, 54, 58, 61, 64, 68,
  73, 74, 75, 83) ; Manaphy, Phione, Arceus ; trophées de la Tour ; Café Combats ; tessons ; rubans de Claudie ; Défi
  des Gouvernantes ; objets de l'histoire (Flûte Noire, Master Ball d'Hélio, applis du Pokémontre…).

## HeartGold / SoulSilver
- **Oublis** : CS04 Force, CS05 Siphon, CS06 Éclate-Roc, CS08 Escalade ; CT70 Flash (Jehan, Tour Chétiflor), CT21 /
  CT27 (Doublonville, le lundi et le dimanche), CT42 Façade (Coin de la chance), CT64 Explosion (souterrain des Routes 5-6) ;
  starter de Kanto (Prof. Chen), Piafabec (Lettre Herbe), Tentacool (Irisia), Lokhlass (Caves Jumelles, vendredi), les
  trois Électrode du Repaire Rocket, fossiles du Musée d'Argenta ; **chaque Meneur de la Zone de Combat** (une seule
  étape « les quatre Photos »).
- **Étiquettes** : « Kyogre (SoulSilver) ou Groudon (HeartGold) » (la check-list dit l'inverse : Kyogre en HG, Orbe
  Bleu) ; « Lugia (SoulSilver) » et « Ho-Oh (HeartGold) » se capturent dans les deux jeux.
- Pas compté : CT achetées ou en Points Combat (04, 06, 08, 14, 15, 16, 17, 20, 22, 25, 31, 32, 33, 38, 44, 52, 53, 58,
  61, 68, 71, 73, 74, 75, 81, 90), Pokémon du casino (Abra, Sabelette, Abo, Minidraco, M. Mime, Évoli, Porygon),
  PokéAthlon en connexion et Coupe des Cracks, Concours de Capture, Fratrie des Sept (pas d'étape en 4e génération alors
  qu'elle est comptée en 2e), tuteurs, marée basse du Lac Colère, revanches du rival, objets de l'histoire (Coffret Mode,
  Boîte Jetons, Noigrumes Shaker, Carte Magnétique, Pass Train, Coupon Expansion…).

## Noire / Blanche
- **Oublis probables** : CT des champions (62 Acrobatie, 67 Vengeance, 72 ChangeÉclair, 76 Survinsecte, 78 Piétisol,
  79 Souffleglacé, 82 Draco-Queue, 83 Rengorgement), récompenses du Pokédex à Renouet (CT54 à 25 vus, CT17 à 60, CT10 à
  115), CT au sol ou offertes que le site n'a pas (01, 04, 08, 28, 31, 32, 40, 42, 45, 49, 56, 57, 69, 70, 75, 94, 95 :
  à recouper avec FlagsEditorEX, certains lieux du site diffèrent de la check-list) ; Zorua (Volucité), Zoroark (Bois des
  Illusions), Genesect (Labo P2) ; fossiles du Musée de Maillard.
- Pas compté : CT achetées ou en PC du Métro (07, 11, 14, 15, 16, 18, 20, 21, 23, 25, 27, 33, 34, 37, 38, 48, 51, 59,
  60, 64, 68, 73, 74, 77, 87, 88) ; Keldeo, Meloetta (distributions) ; famille de Vaguelone, Cynthia, lettres de la
  Route 13 ; tuteurs ; objets trouvés de Rotombourg.

## Noire 2 / Blanche 2
- **Oublis probables** : **Créhelf, Créfollet, Créfadet** (quête de la Caverne des Esprits) ; CT offertes (21 Frustration
  à Amaillide, 24 Tonnerre du rival, 27 Retour de Bianca, 55 Ébullition, 62 Acrobatie, 72 ChangeÉclair, 82 Draco-Queue
  des champions) et CT au sol absentes (35, 40, 42, 43, 44, 45, 49, 57, 70, 94, 95 : à recouper) ; Ptéra du Musée de
  Maillard ; Permis, Charme Ovale, Charme Chroma.
- Pas compté : CT achetées ou en PCo du PWT (04, 07, 08, 10, 11, 14, 15, 16, 17, 18, 20, 23, 25, 31, 32, 33, 34, 37,
  38, 48, 51, 59, 60, 64, 68, 73, 74, 75, 77, 79, 87, 88, 89) ; RAdar Pokémon, distributions ; tournois téléchargeables
  du PWT ; tuteurs à tessons ; sommet du Gratte-Ciel / de l'Arbre Creux (compté par ses médailles) ; Objet Trouvé.

## X / Y
- **Oubli probable** : échange d'Auffrac-les-Congères (Rondoudou → Scalproie).
- Pas compté : **enquêtes de Beladonis** (aucune trace trouvée, voir « Reste à faire ») ; Diancie, Hoopa, Volcanion
  (cadeaux mystère) ; séries de 100 / 200 victoires de la Maison de Combat ; tuteurs ; Rollers, Safari des Amis.

## Rubis Oméga / Saphir Alpha
- Pas compté : fossiles de Mérouville (par le Pokédex), Conseil 4 un par un (comptés ensemble), séries de 100 / 200
  victoires, tuteurs (huttes de l'Atoll), récompense du Pokédex National (Charme Chroma).

## Soleil / Lune
- Pas compté : Magearna (QR code) et Marshadow (cadeaux mystère, choix de l'utilisateur) ; combats de l'après-Ligue sans
  trace (Buffet Baston, Morimoto, Saubohne, Ernest, famille de Néphie, Cathy et Danh de l'épisode Beladonis), Miroir
  Sacré, tampons des Pokédex d'île, Fonction Juge, Bataille Royale, tuteurs.

## Ultra-Soleil / Ultra-Lune
- **Oublis probables** : Pokémon fixes (Fantominus et Baudrive de l'École la nuit, les cinq Métamorph de la Clique des 5,
  Hypnomade, Bacabouh, les deux Scarabrute de Noadkoko) ; Pokémon Dominants contre les Emblèmes (Raphaël, 4 paliers).
- Pas compté : Magearna, Marshadow, Zeraora ; épisode Rainbow Rocket (au-delà de ce que les drapeaux nomment), Vénérables
  Yamato, Jefferson, Ignace ; Studio Photo ; sept mystères de l'École ; tuteurs.

## Let's Go Pikachu / Évoli
- Pas compté : **les 19 Coachs** (aucune trace trouvée) ; combats contre le rival et le Conseil 4 un par un ; revanches
  quotidiennes des Champions et de Blue ; diplôme et Morimoto ; Fonction Juge, Tenue Assistant, Dompteur.

## Épée / Bouclier
- **Oubli probable** : Porygon d'Hortensio (Dojo Maîtrise).
- Pas compté : Cartes de Ligue rares, Charme Chroma / Charme Insigne (Pokédex complets) ; Pokémon fixes (ils reviennent : Khélocrok, Voltoutou, Lokhlass, Torgamord, Colhomard, Blancoton, Tritosor,
  Fourbelin, Wailord…), échanges de l'Isolarmure (position variable) et des Pommes de Kickenham, combats contre Nabil,
  Travis, Rosemary, la Team Yell, Shehroz, Jean-Fleuret / Jean-Targe ; Tour de Combat (rang : aucune trace) ; enquêtes
  de Prosper Spicace ; livraisons ; couronnes du Dojo, recettes du NigoMix ; Tournoi des Stars ; Expéditions Dynamax.

## Diamant Étincelant / Perle Scintillante
- Pas compté : Darkrai, Shaymin, Arceus (cadeaux mystère) ; combats de rival et d'Admins ; missions de l'Homme des
  Souterrains ; Club Ruban ; diplôme ; 5 étoiles de la Carte de Dresseur ; revanches du week-end et de la Ligue ; Canne,
  Super Canne, Charmes.

## Légendes Arceus
- Pas compté : **les 149 missions** (27 principales, 122 secondaires ; aucune trace connue, voir « Reste à faire »),
  revanches des Monarques, 20 Vieux Poèmes, Barons uniques des cavernes des lacs, Voies du Dojo, améliorations de la
  Sacoche, Mégapparitions, Charme Chroma et pages Pokédex « Parfait » (compté par espèce dans la section Pokédex) ; une
  partie de l'histoire (épreuves, confections, rangs étoilés hors du rang Galaxie).

## Écarlate / Violet
- Pas compté : **27 échanges du Club de la Ligue** (sans trace), Miaouss de Galar, Œuf de Jacq, Caninos de Hisui,
  Goinfrex chromatique ; Milobellus du Lac Cristallin ; combats contre Menzi, Pepper, Roseille, Kassis, Taro ; épreuves
  d'Arène (comptées par le badge) ; enchère du portefeuille de Kombu ; Friandises de Jeffry Andise.
