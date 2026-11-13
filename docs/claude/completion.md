# Complétion (100 %)

README : « Complétion (100 %) ». Code : `Services/Completion/` (`Completion.cs` commun aux générations) ; pages `CompletionList.razor` (le nom `Completions`
entrerait en conflit avec le service injecté) et `CompletionPage.razor`.

## Choix de l'utilisateur (2026-11-11)
- Sections au **même poids** : le pourcentage global est la moyenne des sections (le Pokédex et ses 151 étapes
  n'écrasent pas l'histoire). Sections de la 1re génération : Histoire, Pokédex, Dresseurs, Objets, Rencontres uniques.
- **Pikachu de Jaune dans l'Histoire** (une section d'une seule étape pèserait trop lourd).
- **Pokédex sur 151**, Mew compris (pas le diplôme sur 150).
- **Choix exclusifs** (fossile, Kicklee / Tygnon) : une seule case, faite dès que le choix est fait.
- **Ratés** (plus faisables dans la partie) : comptés, marqués « raté » ; le 100 % reste strict, même s'il devient
  impossible.
- Pages publiques (`/joueurs/{pseudo}/completion`, case « Complétion (100 %) » de « Visibilité », `PublicPages.Completion`,
  cochée pour tous par la migration `CompletionChallenge`). Pas dans les statistiques ni dans les tris par défaut (pas
  demandé pour l'instant).

## Tables de Rouge / Bleu / Jaune
- `Gen1CompletionData.cs` est **généré** : `python outils/completion_gen1.py <dossier> MonsutaPotaru/Services/Completion/Gen1CompletionData.cs`,
  où `<dossier>` contient les clones de https://github.com/pret/pokered et https://github.com/pret/pokeyellow
  (`git clone --depth 1`). Les étapes choisies à la main (histoire, rival, cadeaux, rencontres), les lieux en français
  (`PLACES`) et l'ordre de l'aventure (`ZONES`) sont dans le script ; le reste est lu dans les dépôts.
- Les numéros des drapeaux **diffèrent entre Rouge / Bleu et Jaune** : deux tables.
- Conditions : `E:n` drapeau d'événement (`SAV1.GetEventFlag`), `T:n` objet à interrupteur caché (`EventSpawnFlags` :
  ramassé, Pokémon battu), `R:octet:bit` octet de la sauvegarde (objets cachés `0x299C`, jetons cachés `0x29AA`, cannes
  `0x29D4`, Lokhlass `0x29DA`, échanges `0x29E3` ; mêmes adresses dans Jaune, versions internationales), `B:n` badge,
  `D:n` Pokédex capturé, `F` Panthéon, `P` amitié de Pikachu à 255 ; `+` toutes, `|` une seule.
- Pièges vus en écrivant le générateur :
  - les macros (`MACRO add_tm … ENDM`) contiennent des `const` : ne pas les compter (sinon CT décalées de 2) ;
  - Jaune coupe certains scripts en deux fichiers (`BillsHouse_2.asm`) ;
  - drapeaux du **Conseil 4** remis à zéro après le Panthéon (`HallOfFame.asm`) : pas dans « Dresseurs », le Panthéon
    compte dans l'Histoire ;
  - **dresseurs d'arène** : le champion battu lève les drapeaux de tous ses dresseurs (`SetEventRange`) : faits d'office ;
  - Arène de Cramois'Île (quiz) : pas d'en-tête de dresseur, drapeaux `EVENT_BEAT_CINNABAR_GYM_TRAINER_0` à `6` dans
    l'ordre des objets ;
  - **rival du S.S. Anne** : aucun drapeau ; il barre l'escalier du capitaine, donc fait avec la CS01 ;
  - Scope Sylphe et Clé Ascenseur : objets cachés au départ (`OFF`), ramassés = cachés **et** l'événement qui les fait
    apparaître ;
  - carte inutilisée (`UNUSED_MAP_6F`) : son objet caché ne compte pas.
- **Ratés** connus (1re génération) : les 30 Rockets de la Sylphe SARL (disparus après Giovanni au 11e étage, `SilphCo11F.asm`), le premier
  combat contre le rival sur la Route 22 (plus proposé après le badge de Pierre, `PewterGym.asm`). Un nouveau raté : son
  `Missed` dans le générateur.
- Textes : équipe des dresseurs (« Rattata, Abo (niv. 11) » ; pas de noms de classes : ni PKHeX ni un site lisible ne les
  donnent en français), objets (noms français de PKHeX, `ItemFixes` pour ses fautes : « Acienne Canne »), CT / CS avec leur
  capacité (`TmMoves`).
- Vérifié sur `rouge.sav` (4 badges, 35,8 %) et `bleu.sav` ; Jaune seulement en mémoire (pas de vraie sauvegarde).

## Or / Argent / Cristal (2026-11-11)
- Choix de l'utilisateur : les 5 sections de la 1re génération **plus « Quêtes annexes »** ; rien à cocher à la main
  (une étape sans trace dans la sauvegarde est laissée de côté : Casino, Concours de Capture, Show du Bon Numéro, Nina,
  Tour de Combat, Maître des capacités, Écaillerouge contre Multi Exp) ; **tout** (chaque dresseur, chaque objet), comme en
  1re génération ; **26 étapes** pour les Zarbi. Ses check-lists (Or / Argent, Cristal ; CSV sur son Drive, d'après
  Pokébip) ont servi pour l'histoire, les rencontres et les quêtes ; exclu d'office : l'Œuf Bizarre (impossible sur
  cartouche française). **GS Ball et Celebi de Cristal comptés** (2026-11-12, choix de l'utilisateur, comme les
  distributions de la 3e génération) : la GS Ball du Centre Pokémon de Doublonville (`EVENT_GOT_GS_BALL_FROM_GOLDENROD_POKEMON_CENTER`,
  celle que Fargas rend est le même objet) dans les objets ; Celebi dans les rencontres par le Pokédex (`D:251` : le script
  de l'autel ne garde aucun drapeau). PKHeX nomme l'objet « GS Ball* » : astérisque retiré (`Gen2Completion.ItemName`).
- `Gen2CompletionData.cs` : `python outils/completion_gen2.py <dossier> MonsutaPotaru/Services/Completion/Gen2CompletionData.cs`,
  avec les clones de pokegold et pokecrystal. Deux tables : Or / Argent (même source, PKHeX ne les distingue pas) et
  Cristal (autres drapeaux, autres objets).
- Bien plus simple qu'en 1re génération : chaque objet au sol (`itemball`), objet caché (`hiddenitem`) et dresseur
  (`trainer CLASSE, ID, EVENT_BEAT_…`) a son drapeau ; les cadeaux : `verbosegiveitem` puis le `setevent` qui suit
  (corrigés à la main : Arcenci'Aile ; PP Plus de Kenji sans drapeau durable, laissé de côté). Équipes : `party_pointers`
  donne le groupe de chaque classe, l'identifiant (`JOEY1`) son rang. Rematchs : même drapeau, une seule étape.
- Adresses (versions internationales) : drapeaux d'événements Or / Argent `0x261F`, Cristal `0x2600` (`GetEventFlag` de
  PKHeX) ; badges `0x23E4` / `0x23E5` (Johto) et l'octet suivant (Kanto), ordre du jeu : Zéphyr, Essaim, Plaine, Brume,
  **Minéral, Choc**, Glacier, Lever ; Pokédex des Zarbi juste après le Pokédex « vu » (`0x2A8C` / `0x2A67`, 26 octets :
  forme + 1 dans l'ordre des captures, vérifié sur `argent.sav` par `UnownFirstSeen`) ; échanges (`wTradeFlags`) et
  scènes des cartes (`wXxxSceneID`) comptés depuis `wEventFlags` dans la WRAM (`before_events` de l'extracteur).
- Pièges :
  - **le rival** n'a pas de drapeau durable (son objet est caché avant comme après) : Ville Griotte, Écorcia et le
    souterrain se lisent par l'étape qu'il barre (Œuf rendu, CS01, Carte Magn.) ; Tour Cendrée et Route Victoire par la
    **scène de la carte** (`S:octet:1`, 0 = combat à venir) ; Mont Sélénite a son drapeau ;
  - **Red** : son drapeau (`EVENT_RED_IN_MT_SILVER`) est levé au départ, effacé au Panthéon (accord du Prof. Chen),
    relevé quand il part : battu = drapeau levé **et** accord du Prof. Chen. Repasser au Panthéon le fait revenir (et
    l'étape redevient à faire) ;
  - **Conseil 4** : chaque membre ou la Ligue déjà gagnée (`EVENT_BEAT_ELITE_FOUR`), leurs drapeaux servent aux revanches ;
  - Lokhlass revient chaque vendredi, les chiens légendaires errent (Or / Argent) : comptés par le Pokédex (`D:n`) ;
  - drapeaux levés au départ : `InitializeEventsScript` (`engine/events/std_scripts.asm`) ;
  - Or / Argent : on peut passer à côté du sous-sol de la Tour Cendrée (chiens légendaires) ;
  - les macros de `item_constants.asm` (`add_tm`) : celles de la 1re génération, même lecteur.
- **Ratés** : dresseur dont l'objet est visible au départ puis caché (Puits Ramoloss, repaire d'Acajou, premier voyage
  de L'Aquaria) ; Rockets de la Tour Radio : cachés au départ, visibles pendant l'occupation, partis ensuite (raté =
  caché et Tour Radio libérée). Les passagers des voyages suivants de L'Aquaria, les dresseurs de l'Arène d'Azuria et du
  Lac Colère apparaissent plus tard : jamais ratés.
- Vérifié sur `argent.sav` (8 badges de Johto, Ligue battue : 34,7 %) ; Or et Cristal seulement en mémoire.

## Rubis / Saphir / Émeraude / Rouge Feu / Vert Feuille (2026-11-12)
- Choix de l'utilisateur : les 6 sections de la 2e génération, **concours et installations de combat dans les quêtes
  annexes** ; **Pokédex national de 386** ; **distributions comptées** (Ticket Éon, Mystique, Aurora, Vieille Carte ; la GS Ball
  de Cristal aussi, depuis) ; **Maîtres des capacités** à usage unique comptés.
- `Gen3CompletionData.cs` : `python outils/completion_gen3.py <dossier> MonsutaPotaru/Services/Completion/Gen3CompletionData.cs`
  (clones de pokeruby, pokeemerald, pokefirered). Trois tables : Rubis / Saphir, Émeraude, Rouge Feu / Vert Feuille.
  À côté du script : `completion_gen3_lieux.json` (noms PKHeX des sections de carte) et `completion_gen3_ordre.json`
  (lieux des étapes d'histoire des check-lists de l'utilisateur, dans leur ordre : celui des zones ; voir « Ordre des
  lieux »).
- Projets en C : `completion_gen3_extract.py` évalue les `#define` et les `enum` des en-têtes (`c_constants`), lit
  chaque `data/maps/*/map.json` (objets `ITEM_BALL` → `finditem` du script ; `hidden_item` ; dresseurs par
  `trainerbattle_* TRAINER_X`, drapeau = `TRAINER_FLAGS_START` + numéro) et les cadeaux (`giveitem` / `giveitem_msg` /
  `additem` suivis d'un `setflag`). Drapeaux lus par PKHeX (`SAV3.GetEventFlag` : 2 304, 2 400 en Émeraude).
- Pièges :
  - **numéros d'espèces internes** (Arcko : 277) : convertis par le nom (`SPECIES_X` → `NATIONAL_DEX_X`) ;
  - Rouge Feu / Vert Feuille : **objets cachés renouvelables** (`src/renewable_hidden_items.c`) levés d'office puis tirés
    au hasard : exclus ; Émeraude : **cadeaux quotidiens** (drapeaux `DAILY`) exclus ;
  - Rubis / Saphir n'a pas de drapeau « vaincu » pour les légendaires : leur drapeau `FLAG_HIDE_…` (levé après le combat,
    pas au départ : vérifié) ;
  - les noms PKHeX des sections ont des précisions de jeu (« (RSE) », « Tour de Combat (RS) / Zone de Combat (E) ») :
    retirées (`clean`) ; les sections de Rubis / Saphir se lisent dans la liste d'Émeraude (même numérotation) ;
  - champions, Conseil 4 et Maîtres (classes `LEADER`, `ELITE_FOUR`, `CHAMPION`) : dans l'histoire, pas dans les
    dresseurs ; le rival sous plusieurs identifiants (Pokémon de départ, Flora / Brice) : une étape ;
  - `trainer_parties.h` de Rouge Feu / Vert Feuille a des équipes vides sur une ligne (`{DUMMY_TRAINER_MON};`) : lire
    chaque équipe jusqu'au premier `};` sans traverser de `;`, sinon l'équipe vide prend la suivante et le vrai dresseur
    s'affiche « Dresseur » (`CompletionGen3Tests` vérifie que chaque dresseur a son équipe) ;
  - un seul chien légendaire erre dans Rouge Feu / Vert Feuille (selon le starter) : une étape, par le Pokédex ;
  - un caractère invisible (retour arrière) s'est glissé dans une expression régulière écrite par un heredoc : écrire
    ces scripts avec l'outil d'écriture de fichiers ; les heredocs avec des apostrophes cassent aussi la commande.
- Concours : seuls les **tableaux du musée** (rang Master, 5 catégories) laissent une trace ; les rangs Normal à Hyper
  sont sur les rubans des Pokémon, pas dans la sauvegarde.
- Vérifié sur `rouge-feu.sav` (début de partie, 2,5 %) et `vert-feuille.sav` (12,6 %) ; Rubis, Saphir et Émeraude
  seulement en mémoire.

## Diamant / Perle / Platine / HeartGold / SoulSilver (2026-11-12)
- Choix de l'utilisateur : les 6 sections ; **Pokédex national de 493** ; **distributions réelles seulement** (Carte
  Membre et Lettre en Platine, GS Ball et Arceus d'HG / SS ; pas la Flûte Azur ; Darkrai et Shaymin pas en Diamant /
  Perle, jamais distribués pour eux) ; quêtes annexes **seulement ce qui laisse une trace** (concours Master, 28 Zarbi,
  formes de Motisma, Giratina, Shaymin, puzzles et salles des Ruines d'Alpha, Photos des Meneurs ; pas les rubans de
  Claudie, tuteurs à tessons, loterie, Pokéwalker). Check-lists (CSV sur son Drive, d'après Pokébip) : histoire,
  rencontres, ordre des lieux (`completion_gen4_ordre.json`, lieux des étapes « Histoire » : voir « Ordre des lieux »).
- `Gen4CompletionData.cs` : `python outils/completion_gen4.py <dossier> MonsutaPotaru/Services/Completion/Gen4CompletionData.cs`
  (clones de pokediamond, pokeplatinum, pokeheartgold). Trois tables : Diamant / Perle (PKHeX ne les distingue pas),
  Platine, HG / SS. Lieux : numéro de section → `completion_gen4_lieux.json` (PKHeX), noms abrégés du jeu corrigés
  (`PLACE_FIXES`).
- **Platine** (`extract_pt`) : constantes dans `generated/*.txt` (énumérations), cartes `res/field/maps/*/` (`events.json` :
  objets `VisibleItems_*` → `visible_items.s`, dresseurs `trainer.id`, objets cachés `hidden_item` →
  `include/data/field/hidden_items.h` ; `scripts.s` : combats, cadeaux « SetVar VAR_0x8004, ITEM_X » puis `SetFlag`) ;
  équipes `res/trainers/data/*.json` ; lieu : `header.json` → rang dans `location_names.json`.
- **HG / SS** (`extract_hgss`) : `#define` de `include/constants/`, cartes `files/fielddata/eventdata/zone_event/*.json`
  (`std_trainer(TRAINER_X)`, `std_itemball_*` = script 7000 + n → `scr_seq_0141_n`, `std_hiddenitem_*` →
  `FLAG_HIDDENITEM_*` et `src/data/fieldmap/hidden_items.h`), scripts `scr_seq_*.s`, équipes `trainers.json`, lieu par
  `src/data/map_headers.h`. Départ : `scr_seq_0149.s`.
- **Diamant / Perle** (`extract_dp`) : non désassemblé, **fichiers binaires** : `arm9/src/map_header.c` (pour chaque carte :
  fichier de scripts, d'événements, section) ; événements `zone_event_release/narc_XXXX.bin` (u32 nombre puis décors de
  20 octets, objets de 32 octets, structures de `pokeplatinum/include/struct_defs/events.h`) ; numéros de script :
  3000 + n dresseur n+1 (5000 en duo), 7000 + n objet au sol (n-ième script de `scr_seq_release/narc_0370.bin`,
  « SetVar 0x8008/0x8009 »), 8000 + n objet caché (drapeau 730 + n, table `UNK_020F2DB4` de `arm9/asm/unk_02038C78.s` ;
  `sub_02039694` le montre) ; scripts compilés : décalages u32 jusqu'à 0xFD13, commandes u16 (SetFlag 0x1E, CheckFlag
  0x20, SetVar 0x28, combat 0xE5, SetTrainerFlag 0x23 : mêmes numéros qu'en Platine, liste dans
  `arm9/asm/unk_02038C78.s`, commentaires de `arm9/src/scrcmd.c`). Cadeaux : « SetVar 0x8004, objet » puis le premier
  SetFlag dans les 60 octets. **Drapeaux de l'histoire** : ceux de Platine de même nom (même numéro pour 201 des 215
  drapeaux que PKHeX nomme dans les deux jeux), vérifiés : un script de Diamant / Perle doit le lever ou le tester, sinon
  le générateur s'arrête ; écarts dans `DP_FLAGS` (Dialga / Palkia 351, Mars aux Éoliennes 452, trouvé par le premier
  SetFlag après le combat). Aire de Combat : pas de drapeau connu, étape absente en Diamant / Perle.
- Pièges :
  - **combats lancés par un script** (champions, Team Galaxie, rival, Conseil 4) : le jeu ne lève pas le drapeau du
    dresseur (vérifié sur `perle.sav`) ; pas dans « Dresseurs » sauf si un script le lève (`SetTrainerFlag`, les
    champions le font pour leurs dresseurs d'arène) ; l'histoire passe par les drapeaux et variables qui suivent ;
  - **raté** : drapeau qui cache le dresseur levé, sauf s'il l'est au départ ou si un script l'efface quelque part
    (`cleared` : dresseurs qui ne viennent qu'à certaines heures, comme les policiers de la Route 212 ; sans cette
    règle, 9 faux ratés dans `perle.sav`) ;
  - **badges** : un masque de bits (`Badges` = 7 → 3 badges) ; HG / SS : Kanto dans `Badges16`, 8 bits malgré son nom ;
  - drapeaux 1 à 64 : propres à la carte (exclus) ; objets cachés au départ (Aile Lune, Clé Stockage) et cadeaux suivis
    d'un drapeau `FLAG_HIDE_*` levé au départ (CS01 d'HG / SS) : exclus ;
  - PKHeX : une sauvegarde vierge (`new SAV4Pt()`) a des zéros au Pokédex des Zarbi (Zarbi A « vu ») et
    `SAV4.SetCaught` n'y fait rien : dans les tests, `Dex.ClearUnownForms()` et `Dex.SetCaught` ;
  - conditions nouvelles : `B:n`, `N` (Pokédex National), `W:var:v` (variable, 0x4000 retiré), `Z:n` (Zarbi), `M:espèce:forme`.
- Vérifié sur `perle.sav` (3 badges) et `soulsilver.sav` (2 badges) ; Platine seulement en mémoire.

## Noire / Blanche / Noire 2 / Blanche 2 (2026-11-12)
- Choix de l'utilisateur : les 6 sections ; **Pokédex national de 649** ; **chaque médaille** de Noire 2 / Blanche 2 (254 :
  la 255e de PKHeX, « Hint Medal », n'en est pas une) ; quêtes : Chefs du Métro de Combat, chaque tournoi du PWT
  (`PWTRecordID` de PKHeX : 17 types, Port Yoneuve, Champions…, Location, Mix et leurs versions Expert), Gratte-Ciel /
  Arbre Creux et Pokéwood **par leurs médailles** (« Tower Master », « … Movie Master ») ; distribution réelle : Victini
  (Pass Liberté) ; **les 100 parures du Music-Hall** dans les quatre jeux (2026-11-12, toutes, même celles qui ne
  s'obtenaient que par le Global Link ou une distribution). Check-lists (CSV sur son Drive : `suivi_completion_pokemon_noir_blanc.csv`, `_noir2_blanc2.csv`, d'après
  Pokébip) : histoire, rencontres, ordre des lieux (`completion_gen5_ordre.json`, lignes « Histoire »).
- `Gen5CompletionData.cs` : `python outils/completion_gen5.py <dossier> <ROM de Noire> <ROM de Blanche 2> MonsutaPotaru/Services/Completion/Gen5CompletionData.cs`
  (contenu de `<dossier>` : docstring du script). Quatre tables (Black, White, Black2, White2) : objets, dresseurs et
  rencontres diffèrent selon la version.
- Sources :
  - **Noire 2 / Blanche 2** : désassemblage en cours [pokebw2](https://github.com/fuddlesworth/pokebw2) (pas pret) :
    `res/field/zones` (lieu : `place` → rang dans `generated/places.txt` = liste des lieux de PKHeX
    `text_bw2_00000_fr.txt`), `res/field/events` (`order.json` : zone n), scripts `.s` lisibles, `res/trainers/data` ;
    drapeaux presque tous **sans nom** (`EVENT_FLAG_0x03db`) ;
  - **Noire / Blanche** : aucun désassemblage ; la **ROM de l'utilisateur** (Pokémon Version Noire française, code IRBF,
    dans son dossier de jeux DS : lui demander le chemin), lue par `completion_gen5_rom.py` : en-têtes des zones
    `a/0/1/2` (48 octets : [3] scripts, [5] textes, [13] & 0x3FF lieu), événements `a/1/2/5` (indice = zone ; décors de
    20 octets, personnages de 36 : type, drapeau d'apparition, script), scripts `a/0/5/7` (commandes de pokebw2 :
    `include/asm/field_script.inc`, mêmes numéros pour les commandes de base), dresseurs `a/0/9/2` / `a/0/9/3`, textes
    `a/0/0/2` (89 : lieux, 191 : classes) et `a/0/0/3` (chiffrés : clé de 16 bits tournée de 3 bits, en partant du
    dernier caractère). La ROM contient aussi la Forêt Blanche (inaccessible dans Noire) ;
  - **Blanche 2** : ROM française de l'utilisateur (code IRDF, même dossier, sous-dossier `FR`), seulement pour les
    noms : médailles (textes système, fichier 83 ; la 255e, « Médaille Aide », n'en est pas une) et parures (fichier
    93, lignes 3 à 102 = parures 0 à 99, les mêmes que dans Noire : fichier 79) ;
  - **les deux** : listes de drapeaux de [FlagsEditorEX](https://github.com/fattard/FlagsEditorEXPlugin) (noms internes
    du jeu : `SYS_FLAG_BIGFOUR_GHOSTWIN`, `FE_LEGEND1_GET`…, et contenu des objets au sol / cachés en anglais, rapproché
    des constantes `ITEM_` de pokebw2).
- Numéros : dresseur = script 3000 + numéro (5000 + : second d'un duo), drapeau 0x58C (Noire / Blanche) ou 0x5F0 + numéro ;
  objet au sol = personnage au script 7000 + n, son drapeau d'apparition ; objet caché = décor au script 8000 + n,
  drapeau 0x384 / 0xB04 + n ; cadeau = `WorkSet 0x8000, objet` puis `RTCallGlobal` 2800 à 2811, et le `FlagSet` suivant ;
  jour (`TMFLG`) : 0xAA0 à 0xB03 (exclus), 1 à 0x5F : propres à la carte.
- Histoire : **badges**, drapeaux système nommés (`SYS_FLAG_FIRST_POKE_GET`, `ZUKAN_GET`, `BIGFOUR_*WIN`, `GAME_CLEAR`,
  `CHAMPION_WIN`), Pokédex National (`Zukan.IsNationalDexUnlocked`), et **objets rares dans le sac** (`K:n` : CS,
  Bicyclette, Boîte Médailles… ne se jettent pas). Les combats scénarisés (champions, rival, N) ne lèvent **ni le drapeau
  du dresseur ni un drapeau juste après** (vérifié sur `noir.sav`) : pas d'étape « battre le rival ».
- Pièges :
  - **dresseurs d'une seule version** : le script de la carte teste la version (`GameGetVersion`, puis
    `VMStackPushConst 23` = Noire 2, `VMJumpIf`) et cache ceux de l'autre (`FlagSet`) : `version_flags` les retire de la
    table de cette version (sans cette règle, 5 faux « ratés » à la Route Victoire de Noire 2) ; Ville Noire, Forêt
    Blanche, Gratte-Ciel, Arbre Creux : `EXCLUSIVE_PLACES` ; objets propres à une version : « (B2) », « (W) » dans
    FlagsEditorEX, sur le nom ou sur le lieu (« White Forest (W) ») ;
  - un objet que les cartes ne montrent pas (Forêt Blanche, Couronne Relique posée par un script) : lieu de
    FlagsEditorEX traduit par la liste des lieux de PKHeX (anglais → français) ;
  - cadeaux exclus : lettres de la Route 13 (`ITEM_GRAM_*`, rendues), Vokit (selon le héros) ; choix selon le starter ou
    exclusif (Graine Miracle / Eau Mystique / Charbon, fossiles) : une étape « A ou B » ;
  - échange de Maillard (Noire / Blanche) et de la Route 4 (Noire 2 / Blanche 2) : Chlorobule reçu dans les versions
    Noire, Doudouvet dans les Blanche ;
  - Trompignon et Gaulet déguisés : `FV_FLDMONS_n`, zone du personnage qui a ce drapeau (FlagsEditorEX n'en nomme qu'une
    partie en Noire 2 / Blanche 2) ;
  - médailles et parures : certaines demandaient le Wi-Fi, le Global Link ou une distribution (non triées, choix de
    l'utilisateur : toutes comptent) ; parures : bit n du bloc Musical (`Musical5.GetHasProp`), ordre vérifié sur
    `noir.sav` (20, les premières de la Boîte Parure) et `noire-2.sav` (16) ;
  - Gratte-Ciel Noir / Arbre Creux Blanc : les drapeaux `FE_INF_AREA*_ARRIVAL` sont des scènes d'arrivée, pas une
    progression fiable : comptés par les médailles ; Pokémon de N : aucune trace trouvée (seulement `SYS_FLAG_NPOKE_OPEN`).
- Conditions nouvelles : `K:n`, `SB:champ:v` (Métro de Combat : `SuperSingle`… ouvert = Chefs battus au 21e combat ;
  `SuperSingleRecord`… ≥ 49 = Chefs du mode Super battus), `PW:n`, `MD:n`, `PR:n` (parure) ; badges `Misc.Badges`, drapeaux
  `EventWork.GetEventFlag` ; CT par `PersonalInfo5BW.MachineMoves` de PKHeX.
- Vérifié sur `noir.sav` (partie finie : 52,2 % avec les parures, aucun raté) et `noire-2.sav` (2 badges : 21,0 %) ; Blanche et Blanche 2
  seulement en mémoire.

## X / Y / Rubis Oméga / Saphir Alpha (2026-11-13)
- Choix de l'utilisateur : Pokédex **national de 721** ; **Méga-Gemmes dans « Objets »**, à leur lieu, distributions
  réelles comptées (Braségalite de X / Y, Diancite) ; **pas les Pokémon reçus par cadeau mystère** (2026-11-13 :
  Diancie, Hoopa, Volcanion, et en Rubis Oméga / Saphir Alpha les 14 de sa check-list, retirés des « Rencontres » ;
  toujours dans le Pokédex) ; pas la Latiasite ni la Latiosite de X / Y (échange seulement) ;
  quêtes : **Maison de Combat** (Châtelaine au 20e combat, puis au 50e en Super : `FE_BTLHOUSE_CLEAR_*`,
  `FE_BTLHOUSE_MONUMENT_*`), **Super Training** (les 30 épreuves de la cartouche, noms `trainingstage` de PKHeX ; réussie =
  `SuperTrainBlock.GetTime1(n) > 0`), **Concours Live** (Rubis Oméga / Saphir Alpha : variable `WK_SCENE_CONTEST`, 7 à 21) ;
  enquêtes de Beladonis (X / Y) voulues mais **aucune trace trouvée** (variables sans nom dans FlagsEditorEX : à chercher
  sur une sauvegarde de X / Y qui les a faites) ; **ratés** : petite liste à la main (aucun pour l'instant).
  Check-lists : `suivi_completion_pokemon_x_y.csv`, `suivi_completion_pokemon_rubis_omega_saphir_alpha.csv` (Drive de
  l'utilisateur, d'après Pokébip).
- `Gen6CompletionData.cs` : `python outils/completion_gen6.py <dossier> <ROM de X ou Y> <ROM de Rubis Oméga ou Saphir Alpha> MonsutaPotaru/Services/Completion/Gen6CompletionData.cs`
  (contenu de `<dossier>` : docstring du script). Quatre tables (X, Y, OmegaRuby, AlphaSapphire). ROM de l'utilisateur
  (2026-11-13) : `C:\Users\ghalh\Documents\FanGame\Jeux\3DS\Pokemon X.3ds` et
  `…\Pokemon Omega Ruby (Europe) (En,Ja,Fr,De,Es,It,Ko) (Rev 2).3ds`, déjà **déchiffrées** (Y, Soleil, Lune, Ultra-Soleil
  aussi, même dossier ; Rubis Oméga aussi installé dans Citra).
- **Pas de désassemblage pour la 3DS**, scripts compilés illisibles : tout vient des listes de **FlagsEditorEX**
  (`flags_gen6xy_en.txt`, `flags_gen6oras_en.txt` : drapeaux et variables, objets au sol `FV_FLD_ITEM_n`, cachés
  `FLAG_HIDEITEM_n`, cadeaux décrits en X / Y « Received … from … ») et des check-lists. Lieux anglais → PKHeX
  (`text_xy_00000_en/fr.txt`, une liste pour les quatre jeux) ; apostrophe typographique de PKHeX remplacée.
- **Dresseurs** (2026-11-13) : lus dans les ROM par `outils/completion_gen6_rom.py` (formats dans sa docstring : RomFS,
  GARC, LZ11, conteneur « ZO » des zones, scripts Pawn / AMX compressés, textes chiffrés). Drapeau = 0x6CC + numéro du
  dresseur (`TRID_NULL` de FlagsEditorEX, le même dans les quatre jeux). Comptés :
  - les **personnages des cartes** dont le script est 3000 + numéro (5000 + : second d'un duo) : le jeu lève le drapeau
    (287 dans rubis-omega.sav) ;
  - ceux dont un **script de zone lève le drapeau** : une fonction locale fait `add.c 0x6CC` puis appelle la native
    `0x13413496` (lever un drapeau ; `0x133daa6a` le teste, `0xaccbbbd7` l'efface : déduit des usages et de la
    sauvegarde) : arènes de Rubis Oméga (lancées par script), familles de la Route 111, et en X / Y les dresseurs d'arène
    (levés d'office par le champion, comme en 1re génération ; quatre Kinésistes de l'Arène de Flusselles n'existent
    qu'ainsi) ;
  - ceux que FlagsEditorEX situe et que la ROM ne montre pas : Dresseurs Aériens, dresseurs d'Illumis, Pat & Joe ;
  - **pas les combats lancés par script sans cet appel** (natives de combat `0x179b019f`, `0x0b144ec1`, qui ne lèvent
    rien elles-mêmes : les familles de la Route 111 sont combattues par `0x0b144ec1` puis levées une à une) : rival,
    champions de X / Y, Lysandre, Team Flare, Amaryllis, sbires de Poivressel ; pas les classes Champion(ne)(s),
    Conseil 4, Maître (Histoire), ni les revanches (« Rematch » dans FlagsEditorEX).
  Lieu : en-tête de la zone (lieu de PKHeX) ; arène reconnue à sa musique (17) → « Arène de … » de la liste d'ordre, rangée
  avec son badge. X et Y : mêmes données (comparées) ; Saphir Alpha : la ROM de Rubis Oméga, sbires Aqua et Magma échangés
  (`OTHER_TEAM` ; 29 de chaque, aucun Aqua battu dans rubis-omega.sav). Combats multiples (7 arguments) : le premier est
  le partenaire. Équipe affichée, pas le nom (comme les autres générations), bien que la ROM donne classes et noms
  français.
- Arène de Lem (X / Y) : ses dresseurs (quiz) n'ont pas de drapeau levé : aucun dresseur à la Tour Prismatique.
- Histoire : badges (`SAV6.Badges`), objets rares du sac (`K:n`), drapeaux nommés (`SYS_FLAG_GAME_CLEAR`,
  `SYS_FLAG_CAPTURE_*` en Rubis Oméga / Saphir Alpha), **variables de scène** nommées de FlagsEditorEX (`W:n:v`,
  `EventWork6.GetWork`, numéro direct). X / Y : **Conseil 4** par les drapeaux `FE_PLR0302_BIGFOUR2_01` à
  `FE_PLR0602_BIGFOUR3_01` (0x177 à 0x17A, levés par le script de chaque salle : 251 Brasier / Malva → 0x177, 252
  Barrage / Narcisse → 0x178, 253 Alliage / Thyméo → 0x179, 254 Guivre / Dracéna → 0x17A), « ou Panthéon » (sans doute
  effacés pour les revanches) ; la plupart des autres drapeaux de la seconde moitié n'ont pas de nom ; Rubis Oméga / Saphir Alpha : `SYS_FLAG_ZENKOKU_SET` n'est pas levé dans
  `rubis-omega.sav` (Panthéon et Delta faits) : pas d'étape « Pokédex National ».
- Objets : **chaque CT et CS** (boutiques, Maison de Combat, cadeaux sans drapeau) par **possession** (`K:n`, elles ne se
  jettent pas), sauf celles qu'un drapeau d'objet au sol compte déjà ; Méga-Gemmes par possession **au sac ou tenue**
  (`H:n` : la plupart n'ont pas de drapeau nommé ; les 12 `FE_FLD_MEGASTONE_GET_n` de X / Y ne disent pas laquelle).
  Exclus : objets cachés « (recurring) », cadeaux quotidiens « [Daily] », diplômes, échanges d'objets.
- Pièges :
  - un nom d'objet repris d'une génération précédente (Clé Hangar, Passe Bateau, Météorite) a plusieurs numéros : le
    générateur prend le dernier jusqu'à 775 (sinon « Clé du Hangar (DPPt) ») ;
  - planques : Magma seulement en Rubis Oméga, Aqua seulement en Saphir Alpha (`HIDEOUTS`) ;
  - Lati@s : `SYS_FLAG_CAPTURE_RATHIOSU` levé par le cadeau de l'Île du Sud (vérifié sur `rubis-omega.sav`) ;
  - Rayquaza : `SYS_FLAG_CAPTURE_REKKUUZA` pas levé dans `rubis-omega.sav` alors que Deoxys l'est : `|D:384` ;
  - Badge de Norman : lieu « Arène de Clémenti-Ville » (Clémenti-Ville est déjà un lieu du début : la page regroupe par lieu) ;
  - X / Y : la Bicyclette a deux numéros (450, 713) ; l'oiseau légendaire errant dépend du starter : une étape à trois.
- Vérifié sur `rubis-omega.sav` (partie finie, Delta : 41,0 %, aucun raté) dans le navigateur ; X, Y et Saphir Alpha
  seulement en mémoire (pas de sauvegarde, promise par l'utilisateur).

- **Ratés** des dresseurs : aucun (choix de l'utilisateur, 2026-11-13). Les personnages cachés au départ ne se savent pas
  (scripts compilés) : des sbires cachés après l'histoire donneraient de faux ratés en début de partie. Dans
  rubis-omega.sav, 2 sbires Magma de la planque non battus et cachés (drapeau d'apparition 537) restent « à faire ».
- Vérifié sur `rubis-omega.sav` : 291 dresseurs sur 375, toutes les arènes ; et sur `y.sav` (2026-11-13, début de
  partie : 1 badge, 11 h) : 50 dresseurs sur 261, l'Arène de Neuvartault complète (levée par le jeu), **aucun** drapeau
  pour Violette, le rival, Sannah, Tierno ni la Team Flare (la règle des combats lancés par script tient), Conseil 4 pas
  encore. Rien d'autre de levé hors de la table, sauf le Château de Combat.
- **Château de Combat** (X / Y, choix de l'utilisateur, 2026-11-13 : dans les **quêtes annexes**) : ses nobles lèvent
  leur drapeau de dresseur (20 dans `y.sav`) sans être sur une carte ni dans un script de zone. Une étape par noble
  (classes `NOBLES` du générateur, sans lieu dans la ROM) : 75, les doublons de même titre et même nom (champions en
  Marquis, deux équipes chacun) en « l'un ou l'autre », faute de savoir si les deux viennent. Restaurants d'Illumis et
  Maison de Combat : aucun drapeau de dresseur vu.
- Guide du joueur (chapitre « Complétion ») à jour pour la 6e et la 7e génération (2026-11-13).
- **Méga-Gemmes de Rubis Oméga / Saphir Alpha** : Dracaufite Y et Mewtwoïte Y gardées (choix de l'utilisateur,
  2026-11-13), bien que sa check-list les note absentes de la liste des Méga-Gemmes et que Mewtwo soit introuvable dans
  ces jeux (`ORAS_MEGA` du générateur).

### Reste à faire (6e génération, au 2026-11-13)
1. **Partie de X / Y plus avancée** (`y.sav` s'arrête au premier badge) : y chercher les **enquêtes de Beladonis**
   (variables `WK_SCENE_*` sans nom qui changent quand on les fait), vérifier le Conseil 4 (0x177 à 0x17A) et les
   Marquis du Château (l'un ou les deux de chaque doublon).
2. **Saphir Alpha** : dresseurs déduits de Rubis Oméga (pas de ROM ni de sauvegarde) ; à vérifier si l'une arrive.
3. Fichiers du `<dossier>` du générateur : pas dans le dépôt, à retélécharger sur un autre PC (sources et noms exacts :
   docstring de `outils/completion_gen6.py` ; check-lists sur le Drive de l'utilisateur, ID
   `1AKULRv48UUOCjAOyXuyghTTbk1PotVQA` pour X / Y et `1vVW-VMqtcIwATZSc5-4djXeGKw5jzNBn` pour Rubis Oméga / Saphir
   Alpha).

## Soleil / Lune / Ultra-Soleil / Ultra-Lune / Let's Go (2026-11-13)
- Choix de l'utilisateur : les trois paires de jeux ; **pas encore de dresseurs** (il faut les ROM ; voir « Reste à
  faire ») ;
  **cristaux Z dans « Objets »**, à leur lieu, par possession (ceux des épreuves comptent déjà dans l'histoire, pas en
  double) ; quêtes : **Arbre de Combat**, **Zygarde** (Boîte, Cœurs, chaque Cellule), **Scanner des Îles** (déconseillé :
  seule trace, le Pokédex ; voulu quand même), **Experts et Coachs de Let's Go** (Coachs : aucune trace trouvée, laissés
  de côté) ; Pokédex national (802, 807 ; Let's Go : les 151, Meltan, Melmetal) ; distributions réelles comptées
  (cristaux Z distribués, Meltan / Melmetal de Pokémon GO) ; **pas les Pokémon reçus par cadeau mystère** (2026-11-13 :
  Magearna et le Pikachu Casquette Partenaire par code QR, Marshadow, Zeraora ; `MYSTERY_GIFT` du générateur), **ni Mew
  de la Poké Ball Plus** (Let's Go, même jour) ; tous toujours dans le Pokédex. Ces retraits ont été faits dans
  `Gen7CompletionData.cs` à la main (14 lignes) faute des check-lists pour relancer le générateur, modifié pour donner le
  même résultat. Check-lists : `Completion_Pokemon_Soleil_Lune.csv`, `_UltraSoleil_UltraLune.csv`,
  `_LetsGo_Pikachu_Evoli.csv` (fournies par l'utilisateur dans le chat, à déposer sur son Drive ; d'après Pokébip).
- `Gen7CompletionData.cs` : `python outils/completion_gen7.py <dossier> MonsutaPotaru/Services/Completion/Gen7CompletionData.cs`
  (contenu de `<dossier>` : docstring du script). Six tables (Sun, Moon, UltraSun, UltraMoon, LetsGoPikachu,
  LetsGoEevee). `completion_gen7_ordre.json` : **tous** les lieux dans l'ordre de l'aventure, écrits à la main (un lieu
  absent arrête le script) ; les quêtes sont groupées par activité (« Arbre de Combat », « Zygarde », « Scanner des Îles »,
  « Experts Pokémon », « Après la Ligue »), le lieu dans la précision.
- Sources : listes de **FlagsEditorEX** (`flags_gen7sm_en.txt`, `flags_gen7usum_en.txt`, `flags_gen7blgpe_en.txt`) ;
  lieux et noms de PKHeX ; lieux des cellules Zygarde dans l'éditeur de PKHeX (`PKHeX.WinForms/.../SAV_ZygardeCell.cs`,
  anglais, seul le lieu principal est traduit, plus jour / nuit).
- Numéros : Soleil / Lune et Ultra : `SAV7.EventWork.GetEventFlag(n)`, n = numéro de FlagsEditorEX (4 000 drapeaux en
  Soleil / Lune, 4 960 en Ultra : numéros différents entre les deux paires). Let's Go : `EventWork7b.GetFlag(n)`, n brut
  de FlagsEditorEX (`FSYS_GAME_CLEAR` = 0x80, début des drapeaux système).
- Histoire : les **épreuves par leur cristal Z** dans le sac (`K:n`, poche des cristaux, 807 à 835 et 927 à 932 ; le
  numéro « tenu », 776 et suivants, n'est jamais dans le sac) : `FE_SIREN_COMPLETE_01` à `08` existent mais leur ordre
  (Feu / Plante ?) n'est pas sûr. Grandes épreuves : cristal **ou** tampon du Passe Dresseur (`PS:n`, `Misc7.Stamps`,
  `Stamp7` de PKHeX : 1 Mele-Mele à 4 Poni) ; Poké Montures (`SYS_FLAG_*_RIDE_OPEN`), Bracelet Z, flûtes, Canne, Ligue
  (`SYS_FLAG_GAME_CLEAR`), Necrozma (Ultra : `SYS_FLAG_CAPTURE_NECROZMA`). Let's Go : badges `FSYS_GYM_CLEAR_*`,
  Techniques Secrètes `FSYS_HIDEN_OPEN_*`, Scope Sylphe et Poké Flûte (sac), `FSYS_GAME_CLEAR`.
- Objets : au sol (« … item ball disappeared ») et cachés de FlagsEditorEX, cadeaux décrits de Let's Go (pas les tenues) ;
  exclus : `[unused?]`, « (recurring) », cadeaux quotidiens, nectars ; CT restantes par possession (lieux de la
  check-list) ; Méga-Gemmes de Let's Go par possession (poche Combat).
- Conditions nouvelles : `PS:n`, `BT:mode:super:n` (`BattleTree7.GetTreeStreak`, meilleure série : 0 Solo, 1 Duo,
  2 Multi), `ZC:n` (cellule ramassée : `GetZygardeCell(n) == 2` ; 1 = seulement visible), `TF:n` (titres de Let's Go,
  `EventWork7b.GetTitleFlag` : 1 à 153 les Experts dans l'ordre du Pokédex de Let's Go, 154 Grand Maître, 155 Maître de
  Combat ; d'après le commentaire de PKHeX, pas vérifié sur une sauvegarde).
- Pièges :
  - des descriptions de FlagsEditorEX sont fausses : `FV_SUB423_LUNASOL` « Solgaleo/Lunala Captured » est levé dans
    `ultra-soleil.sav` en début de partie (pas utilisé) ; « Found Found Max Elixir » ; noms anglais à corriger
    (`ITEM_FIXES` : « Health Wing », « Leach Life »…) ;
  - PKHeX ajoute des précisions aux noms en double (« Normazélite [Z] », « Passe Bateau (GP/GE) ») : retirées
    (`Gen7Completion.Clean`) ; ses noms de capacités ont l'apostrophe typographique (« CT01 Coup d’Boule ») ;
  - Ultra : les cellules Zygarde se ramassent **avant** la Boîte (20 dans `ultra-soleil.sav`, sans la Boîte) ;
  - Let's Go : échanges contre une forme d'Alola, drapeaux `FE_FIELD_TRADE_FIRST_01` à `08` sans description, **ordre
    supposé** (celui du voyage) ; Cœurs de Zygarde d'Ultra : lieux inconnus (« Zygarde » sans précision).
- Vérifié sur `ultra-soleil.sav` (début d'Akala : 13,1 %, aucun raté, 80 objets tous à Mele-Mele et au début d'Akala) dans
  le navigateur ; Soleil, Lune, Ultra-Lune et Let's Go seulement en mémoire.

### Reprendre sur un autre PC (7e génération)
- **Régénérer la table** : rien du `<dossier>` n'est dans le dépôt. Le recréer (un dossier temporaire) avec :
  - `flags_gen7sm_en.txt`, `flags_gen7usum_en.txt`, `flags_gen7blgpe_en.txt` :
    `https://raw.githubusercontent.com/fattard/FlagsEditorEXPlugin/main/flagslist/<fichier>` (sous Windows, `curl` a
    besoin de `--ssl-no-revoke`) ;
  - de PKHeX (`git clone --depth 1 --filter=blob:none --sparse https://github.com/kwsch/PKHeX.git`, puis
    `git sparse-checkout set PKHeX.Core/Resources/text "PKHeX.WinForms/Subforms/Save Editors/Gen7"`) :
    `text/locations/gen7/text_sm_00000_en.txt`, `_fr.txt`, `text_gg_00000_en.txt`, `_fr.txt`,
    `text/items/text_Items_en.txt`, `_fr.txt`, `text/other/en/text_Species_en.txt`, `text/other/fr/text_Species_fr.txt`,
    `PKHeX.WinForms/Subforms/Save Editors/Gen7/SAV_ZygardeCell.cs` ;
  - les trois check-lists de l'utilisateur, **renommées** : `Completion_Pokemon_Soleil_Lune.csv` →
    `suivi_completion_pokemon_soleil_lune.csv`, `Completion_Pokemon_UltraSoleil_UltraLune.csv` →
    `suivi_completion_pokemon_ultra_soleil_ultra_lune.csv`, `Completion_Pokemon_LetsGo_Pikachu_Evoli.csv` →
    `suivi_completion_pokemon_lets_go.csv` (dans ses Téléchargements le 2026-11-13, à mettre sur son Drive ; lui
    demander l'ID ou le fichier).
  - Puis `python outils/completion_gen7.py <dossier> MonsutaPotaru/Services/Completion/Gen7CompletionData.cs` : la table
    régénérée doit être identique à celle du dépôt (`git diff` vide) ; sortie attendue : Sun / Moon `Story 28, Items 408,
    Encounters 25, Quests 134`, Ultra `30, 482, 58, 140`, Let's Go `17, 331, 29, 158` (comptes repris par
    `CompletionGen7Tests`).
- **Explorer une sauvegarde** : un petit projet console dans un dossier temporaire, avec le paquet `PKHeX.Core` de la
  même version que `MonsutaPotaru.csproj` (26.10.18) ; `SaveUtil.GetSaveFile(octets)`, puis lister
  `EventWork.GetEventFlag(n)` levés (SAV7) ou `EventWork.GetFlag(n)` (SAV7b) avec les noms de FlagsEditorEX (colonne 7),
  `EventWork.GetWork(n)`, le sac (`Inventory.Pouches`), `Misc.Stamps`, `GetZygardeCell(n)`, `BattleTree.GetTreeStreak`.
- **Tester** : `dotnet test … --filter "FullyQualifiedName~Completion"` (66 tests au 2026-11-13) ; suite complète : 821
  réussis, 47 échecs tous dus à `showdown/node_modules` absent (`npm ci --prefix showdown` les fait passer).

### Reste à faire (7e génération, au 2026-11-13)
1. **Dresseurs** avec les ROM, comme la 6e (`outils/completion_gen6_rom.py` : RomFS, GARC, zones, scripts AMX, textes ;
   la méthode y est décrite). Soleil, Lune et Ultra-Soleil **déchiffrés** sont déjà dans
   `C:\Users\ghalh\Documents\FanGame\Jeux\3DS\` (vérifié le 2026-11-13 : en-têtes NCCH sans chiffrement) ; Let's Go :
   à demander (extraction de sa cartouche). Les numéros des archives et les hachages des natives changent sûrement : les
   retrouver comme en 6e (textes : chercher une classe connue ; natives : corréler avec des numéros connus). Pistes :
   - données des dresseurs (équipes, classes, noms : GARC de trdata / trpoke sur 3DS, d'après pk3DS ; fichiers de la
     RomFS de Let's Go, format à trouver) et textes des classes et des noms ;
   - **drapeau = base + numéro du dresseur ?** En Ultra, `TR_FLAG_001` = 0x0BDD : vérifier sur les 52 `TR_FLAG_n` levés
     de `ultra-soleil.sav` (les dresseurs de Mele-Mele et du début d'Akala) ; en Soleil / Lune les drapeaux ont des noms
     `FE_SUB…_TR…` et 252 ont un lieu ; en Let's Go, 613 drapeaux de dresseurs à partir de 0x8B6 ;
   - **lieu** : FlagsEditorEX quand il le donne, sinon les données des zones de la ROM (personnages de chaque zone et leur
     script) ; c'est la partie difficile (scripts compilés) ;
   - **Coachs de Let's Go** : une classe de dresseur à part ; si leur drapeau suit leur numéro, les trouver par la ROM ;
   - ensuite : section Dresseurs dans `CompletionGen7Tests` (comptes des tables), retirer « pas encore de dresseurs en 7e
     génération » de `CompletionList.razor` et du README, docstring du générateur, cette fiche.
2. **Sauvegardes** de Soleil / Lune et de Let's Go à demander (à ranger dans `MonsutaPotaru.Tests/Sauvegardes/` :
   `soleil.sav`, `lune.sav`, `lets-go-pikachu.sav`… ; `RealSavesTests`, ligne « Jeux testés » du README, chapitre « Jeux
   pris en charge » du guide ; un test `Real…` comme `RealUltraSun_ProgressReadInItsFlags`). Y vérifier : titres des
   Experts (`TF:n`, ordre supposé d'après PKHeX), ordre des échanges de Let's Go (`FE_FIELD_TRADE_FIRST_01` à `08`),
   Coachs, objets rares de Let's Go gardés après usage (Scope Sylphe, Poké Flûte), lieux des Cœurs de Zygarde d'Ultra.
   **Méthode la plus sûre** pour un drapeau sans nom : deux sauvegardes juste avant et juste après l'action (battre un
   Coach, finir une épreuve), et comparer les drapeaux et variables qui changent.
   Aussi : l'ordre de `FE_SIREN_COMPLETE_01` à `08` (les épreuves restent comptées par leur cristal Z en attendant).
3. Check-lists à déposer sur le Drive par l'utilisateur (puis noter leurs ID ici).

## Ordre des lieux (3e à 6e génération, 2026-11-12 ; 7e : liste complète écrite à la main, voir plus haut)
- Dresseurs et objets : groupés par lieu, rangés par `completion_gen3.anchored_rank` : première mention du lieu dans la
  liste d'ordre **en mot entier** (« Route 1 » ne prend plus la place de « Route 10 », « Route 3 » celle de « Route 30 ») ;
  s'il manque, son voisin désigné (`ANCHORS` de chaque générateur, suivi en chaîne) ; sinon, pour une route ou un chenal,
  le numéro le plus proche. Le nom du lieu départage deux lieux au même rang : chaque lieu reste d'**un seul bloc**
  (avant : en Rubis / Saphir, les dresseurs de 49 lieux éclatés en 83 blocs). Un lieu sans rang arrête le générateur.
  Vérifié par les tests des tables (`CompletionGen3Tests`, `CompletionGen4Tests`).
- 4e génération : `completion_gen4_ordre.json` ne reprend que les lieux des étapes **Histoire** des check-lists ; les
  lignes du Pokédex ou des CT citent des lieux de capture et rangeaient des routes en fin de liste (Route 201, Route 29).
  Les routes de Kanto d'HG / SS ont une ville voisine (sinon rangées par numéro près de la Route 30).
- 6e génération : `completion_gen6_ordre.json` (lignes « Histoire » des deux CSV, sans Bourg Croquis : sa
  seule étape vient après la Ligue et rangeait la ville de départ à la fin ; rangé avec Quarellis dans `ANCHORS`) ; les mirages de Rubis Oméga / Saphir Alpha avec « Hoenn » (après l'Épisode Delta).
- 3e génération : `completion_gen3_ordre.json` ne reprend que les lignes « Histoire » et « Arène / Ligue » des CSV de
  l'utilisateur (`Suivi_Pokemon_Rubis_Saphir.csv`, `_Emeraude.csv`, `_RougeFeu_VertFeuille.csv`, sur son Drive, dans
  l'ordre des lignes). Routes de Kanto et lieux annexes : voisin dans `ANCHORS` (la Route 1 avec Jadielle, les Routes 14
  et 15 avec Parmanie, l'Île 7 avec l'Île 6…). La table régénérée a les mêmes lignes qu'avant, seul l'ordre change.

## Ajouter une génération
- Une nouvelle table et sa lecture (`Gen2Completion` sert de modèle ; sans désassemblage : `Gen6Completion`, `Gen7Completion`
  pour un Pokédex qui n'est pas « 1 à n », comme celui de Let's Go) ; `CompletionGames.For` et `Steps` choisissent
  selon la sauvegarde. Garder les sections au même poids ; `CompletionProgress.Of` ne montre que celles du jeu.
- PKHeX a des noms de drapeaux à partir de la 2e génération (`Resources/text/script/gen2/flags_*`), mais incomplets
  (152 pour Or / Argent) : les désassemblages restent la source.
