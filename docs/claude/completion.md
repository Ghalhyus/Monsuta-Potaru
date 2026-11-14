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
1. **Dresseurs** (**en pause**, choix de l'utilisateur, 2026-11-13 : lieux non reliés ; ne pas proposer la couverture partielle de Soleil / Lune sans lui redemander) avec les ROM, comme la 6e (`outils/completion_gen6_rom.py` : RomFS, GARC, zones, scripts AMX, textes ;
   la méthode y est décrite). Soleil, Lune et Ultra-Soleil **déchiffrés** sont déjà dans
   `C:\Users\ghalh\Documents\FanGame\Jeux\3DS\` (vérifié le 2026-11-13 : en-têtes NCCH sans chiffrement) ; Let's Go :
   seulement `…\Jeux\Switch\Pokémon Let’s Go, Eevee!.nsp`, **chiffré** (il faut les clés de sa console) : à
   l'utilisateur d'en extraire la RomFS (pas de dresseurs de Let's Go d'ici là, son choix du 2026-11-13). Les scripts
   d'exploration de cette session (lecture des aires, désassemblage AMX, `casetbl`) n'ont pas été gardés : les refaire
   avec `completion_gen6_rom.py` (`RomFS`, `garc`, `lz11`, `amx`, `texts`). Les numéros des archives et les hachages des natives changent sûrement : les
   retrouver comme en 6e (textes : chercher une classe connue ; natives : corréler avec des numéros connus).
   **Trouvé le 2026-11-13** (avec `completion_gen6_rom.py`, qui lit déjà ces RomFS) :
   - dresseurs : Soleil / Lune `a/1/0/5` (données, 485 de 20 octets : classe à l'octet 0) et `a/1/0/6` (équipes,
     32 octets par Pokémon : niveau u16 au décalage 14, espèce u16 au 16) ; Ultra `a/1/0/6` (653) et `a/1/0/7` ; classes
     `a/1/0/4` / `a/1/0/5` ; textes français `a/0/3/3` (Soleil / Lune : fichier 105 les noms, 106 les classes ; Ultra :
     110 et 111 ; même chiffrement qu'en 6e) ;
   - **drapeau = base + numéro** : Soleil / Lune 0x81C (183 descriptions de FlagsEditorEX sur 268 tombent juste ; les
     autres, `FE_SUB…_TR…`, sont des combats à part), Ultra 0xBDC (`TR_FLAG_000`) ;
   - lieux : aires dans `a/0/8/2` (2 761 membres, 11 par aire : `ED`, `BG`, `TR`, `AS`, `aa`, `AE`, `ZS`, `ZI`, `EA`,
     `AC`…) ; `ED` = conteneur d'événements par zone (`EP`, `EM`, `EB`, `ES`, `EA`, `ET`, `EG`, `EI`, `FS`, `PR`, chacun
     un conteneur d'un bloc ; conteneurs de l'aire : u16 magie, u16 nombre, u32 décalages, alignés sur 0x80) ;
   - **impasses** : les valeurs 3004, 3008… des `ED` sont des décalages internes (toutes multiples de 4), pas des
     dresseurs ; `TR` est un bloc de même taille partout (terrain ?) ; aucune native des scripts de zones (`ZS`, 284
     scripts AMX par aire, compressés comme en 6e) ne reçoit de numéro de dresseur en argument constant (corrélation avec
     les 234 numéros sûrs : 12 au mieux, pour 0x133daa6a / 0x13413496, les mêmes natives de drapeaux qu'en 6e) ;
   - **à poursuivre** : `EM` = personnages, enregistrements de longueur variable selon le type (u32 nombre, puis pour le
     type 1 : 30 mots — type, x, y, z flottants, rotation, identifiant 6xx, modèle, script local du type 30096, deux
     décalages vers des données en fin de bloc ; le type 3 est plus court) : y chercher le numéro du dresseur (sans doute
     dans les données pointées) ; puis la zone d'un `ED` et son lieu (en-têtes de zones : `a/0/1/2`, `a/0/1/4` à `a/0/1/9`
     examinés sans résultat net) ; scripts AMX communs dans `a/0/9/2` (500) et `a/0/8/4`.
   - **combats lancés par les scripts de zones** (`ZS`) : par une fonction locale qui atteint la native `0x6393be9a`
     (premier argument = numéro du dresseur) : 115 dresseurs en Soleil, 126 en Ultra-Soleil (rival, Capitaines, Team
     Skull, Prof. Samia…), ~63 recoupent les lieux de FlagsEditorEX. Les dresseurs des routes n'y sont pas : sans doute
     lancés par le moteur à partir des personnages (`EM`), numéro pas encore trouvé ;
   - **personnages des aires** (`EM`, enregistrements de type 1, 120 octets : u32 type, x, y, z, rotation, puis mot 9
     drapeau d'apparition, 11 numéro local, 12 « genre » (9 = 390 personnages en Soleil, presque autant que les 391
     dresseurs nommés : sans doute les dresseurs), 13 modèle, 14 numéro du cas dans le script de l'aire, 28 / 29
     décalages vers deux blocs de 28 octets (trajets, flottants)). Le script `ZS` de l'aire n'a qu'une fonction publique
     qui aiguille par `casetbl` (AMX : nombre, défaut, puis paires valeur / saut relatif) ; ex. aire 11, cas 13 et 15 :
     appel d'une fonction à 5 arguments (37 ou 35, 1, 0, 7, 1) = numéro du dresseur en premier, qui délègue à une autre
     (profondeur > 3 : penser à suivre les appels loin) ;
   - les 500 scripts de `a/0/9/2` (759 en Ultra) ont bien plus de numéros de dresseurs en premier argument d'appels
     locaux (312 constantes, 142 connues) : scripts par zone ? Mais la forme d'appel (5 arguments, 1, 0, 7, 1) n'en
     couvre que 39, et la native la plus atteinte (0xb13a389) est générique (480 valeurs) : filtre à trouver ;
   - **lieu d'une zone : toujours inconnu** (aucune table de ~500 / 961 entrées avec un numéro de lieu ; `a/0/1/7` =
     éclairage, `a/0/1/3` = rencontres) ; voter par les dresseurs situés par FlagsEditorEX donne des scripts à plusieurs
     lieux (`a/0/9/2` 0 : Routes 1, 2, 3) : inutilisable tel quel.
   - **couverture possible sans décoder les zones** (2026-11-13) : Soleil 391 dresseurs nommés, 218 situés par
     FlagsEditorEX, 269 en ajoutant ceux des scripts situés par les autres dresseurs de la même aire ; Ultra : 519 nommés,
     **7** situés par FlagsEditorEX seulement (inutilisable sans les zones).
   - Check-lists de la 7e génération : liens Drive de l'utilisateur (2026-11-13) : Soleil / Lune
     `1hj84TPacIYE7Zo5rM4o6mcJjmXJ9dSvp`, Ultra `17WvuVZBM0qLbIDdaZe0dBXZ-bl4RTUjg`, Let's Go
     `1-nDB1WhUGwCPAkanJH8Y9fh0WFe5jHpg` (partagés par lien : `https://drive.google.com/uc?export=download&id=<ID>`,
     `curl -L --ssl-no-revoke`). La table régénérée avec elles est identique à celle du dépôt.
   Pistes :
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
   Coach, finir une épreuve), et comparer les drapeaux et variables qui changent (marche à suivre : « Trouver de
   nouveaux drapeaux : paires de sauvegardes », plus bas).
   Aussi : l'ordre de `FE_SIREN_COMPLETE_01` à `08` (les épreuves restent comptées par leur cristal Z en attendant).
3. Check-lists : sur le Drive de l'utilisateur, ID notés au point 1.

## Épée / Bouclier / Diamant Étincelant / Perle Scintillante / Légendes Arceus (2026-11-13)
- Choix de l'utilisateur : les trois jeux d'un coup ; **DLC d'Épée / Bouclier toujours comptés** (Isolarmure et
  Couronneige dans les sections normales, à leur lieu) ; Pokédex d'Épée / Bouclier = **Pokémon présents dans le jeu**,
  c'est-à-dire les trois Pokédex régionaux (584 espèces, une forme suffit : `Gen8Completion.SwordShieldPokedex`, d'après
  `PokeDexIndex`, `ArmorDexIndex` et `CrownDexIndex` de PKHeX ; pas ceux qui ne viennent que de HOME, comme Mew) ;
  DÉ/PS : national de 493 ; Légendes Arceus : les 242 de Hisui, **trois étapes par espèce** (capturé, recherche au
  niveau 10, Parfait : `PokedexSave8a.IsComplete` / `IsPerfect`), soit 726 étapes dans la section Pokédex ; **pas de
  dresseurs** ; **CT et DT par possession** (une DT apprise sort du sac et redevient « à faire » : choix assumé) ;
  missions d'Arceus laissées de côté en attendant une sauvegarde ; quêtes : Tour de Combat, collections (Taupiqueur,
  Flammes Étranges, Zarbi), concours et souterrains de DÉ/PS, rangs et Monarques d'Arceus, **quand une trace existe**.
- Check-lists (Drive de l'utilisateur, d'après Pokébip, partagées par lien) : Épée / Bouclier
  `1tXJnDv3i9B3JD-XWTwgBft0Rev8dCY3c`, DÉ/PS `1I8NBfz0IOYVVo3LdQTYaaV4w8m67LZ2Y`, Légendes Arceus
  `1G3DSoGfgdZlTERJVW3jrBx5eLhIkuHAT` (`https://drive.google.com/uc?export=download&id=<ID>`, `curl -L --ssl-no-revoke`),
  à renommer `suivi_completion_pokemon_epee_bouclier.csv`, `suivi_completion_pokemon_diamant_etincelant_perle_scintillante.csv`
  et `suivi_completion_pokemon_legendes_arceus.csv`.
- `Gen8CompletionData.cs` : `python outils/completion_gen8.py <dossier> MonsutaPotaru/Services/Completion/Gen8CompletionData.cs`
  (contenu de `<dossier>` : docstring du script : FlagsEditorEX `flagslist/flags_gen8*_en.txt` ; de PKHeX
  `Resources/text/locations/gen8`, `gen8b`, `gen8a`, `text/items`, `text/other/en` et `fr`,
  `text/game/underground_bdsp/text_ug_item_fr.txt`, `Saves/Substructures/Gen8/BS/UgItemUtil.cs`). Cinq tables (Sword,
  Shield, BrilliantDiamond, ShiningPearl, LegendsArceus) ; sortie attendue : Épée / Bouclier `Story 30, Items 525,
  Encounters 71, Quests 149`, DÉ/PS `20, 562, 34, 469`, Arceus `16, 0, 17, 145` (comptes repris par `CompletionGen8Tests`).
  Ordre des lieux : ceux des lignes « Histoire » de la check-list (`story_order`) et `ANCHORS` du script (un lieu sans
  rang arrête le générateur). Les fichiers du `<dossier>` ne sont pas dans le dépôt.
- **Lecture** :
  - Épée / Bouclier et Arceus : sauvegarde en **blocs** (`SCBlockAccessor`) ; un drapeau de FlagsEditorEX (clé de 64 bits)
    est le bloc des 32 bits du bas, booléen (`Bool2` levé, `Bool1` baissé) : condition `X:clé` ; valeur numérique
    `XV:clé:n` (rang du Groupe Galaxie `0x50FE632A`, Flammes Étranges par région `0x8B18ADE5`… : `SaveBlockAccessor8LA.cs`
    de PKHeX). Une **sauvegarde vierge de PKHeX n'a pas ces blocs** (et ses blocs numériques sont sans type :
    `ChangeStoredType` dans les tests) : le test d'Épée part de `epee.sav` ;
  - objets cachés d'Épée / Bouclier : **trois blocs de 512 × 4 octets** (`0x6148F6AC` jeu, `0xE479EE37` Isolarmure,
    `0xE579EFCA` Couronneige ; repris de `FlagsGen8SWSH.cs` de FlagsEditorEX), état au premier octet (2 : ramassé,
    reviendra ; 3 : pour de bon) : `HI:n`. Exclus : « (recurring) », « random », sans lieu. `epee.sav` vient d'une version
    sans DLC : les deux autres blocs manquent (jamais faits), et son Pokédex ne connaît que Galar ;
  - **badges d'Épée / Bouclier** : `SAV8SWSH.Badges` est un **masque de bits** (`epee.sav` : 7 = 3 badges, comme
    `FSYS_GYM_CLEAR_FIRST` à `THIRD`) ;
  - DÉ/PS : `FlagWork8b` (`E:n` = `GetFlag`, `SF:n` = `GetSystemFlag`, numéros de FlagsEditorEX) ; **badges** = drapeaux
    système 124 à 131 (`FlagWork8b.BadgeCount`), supposés dans l'ordre des arènes de Diamant / Perle ; concours Master
    `SYS_FLAG_CON_*_MASTER` (9 à 13) ; Tour de Combat : `FE_TOWER_MASTER_WINED` et `BattleTowerWork8b.MasterRankMax` ≥ 10
    (`BTM:10`) ; Zarbi : `Zukan8b.GetHasFormFlag(201, forme)` (`UF:n`) ; statues : `UndergroundItemList8b.ReadItems()`
    (`UG:n`, 434 statues d'après `UgItemUtil.cs`) ; Pokédex National : `Zukan8b.HasNationalDex` (`N`) ;
  - Arceus : Zarbi par le Pokédex (`PokedexSave8a.HasAnyPokeObtainFlags(201, forme)`, `LF:201:n`) ; Flammes Étranges
    comptées par région (Rusti-Cité 7, chaque région 20) : l'ordre des 107 drapeaux `MKRG_ID_*` n'est pas sûr, la n-ième
    étape d'une région est faite dès que n Flammes y sont trouvées.
- CT : Épée / Bouclier, `Tm` 1 à 100 = CT00 à CT99 (`PersonalInfo8SWSH.MachineMovesTechnical`), 101 à 200 = DT00 à DT99
  (`MachineMovesRecord`) ; DÉ/PS : CT01 à CT100 = `Gen4CompletionData.TmMoves` (les CS de Diamant / Perle devenues CT93 à
  CT100, dans le même ordre ; `PersonalInfo8BDSP.MachineMoves` est interne à PKHeX). En DÉ/PS, une même CT peut être au
  sol à deux endroits (route et Grands Souterrains) : deux étapes.
- Pièges :
  - **caractère invisible** dans une expression régulière écrite par un heredoc Python (le `\1` d'un remplacement devenu
    l'octet 0x01) : sous-lieux des Grands Souterrains vides ; et un heredoc avec des apostrophes a cassé la commande :
    écrire ces textes avec l'outil d'écriture ou d'édition de fichiers ;
  - objets d'une seule version dans FlagsEditorEX : « (Sw) », « (Sh) », « (BD) », « (SP) » en fin de description ;
    « (fossil spot) » (fossile au hasard) exclu ; apostrophe typographique dans les noms d'objets de PKHeX (« King’s Rock ») ;
  - échanges d'Épée / Bouclier : `FSYS_NPC_TRADE_01` à `11` rapprochés des lignes E001 à E011 de la check-list
    (**ordre supposé**, aucune description ; les lignes d'une seule version ont chacune leur drapeau) ; ceux de
    l'Isolarmure (position variable, sans drapeau) laissés de côté ; DÉ/PS : les 4 drapeaux `IN-GAME TRADE` dans l'ordre ;
  - drapeaux **« z_… »** de FlagsEditorEX (« KReceivedGiftCharmander », « KCapturedVirizion ») : apparition d'un objet
    ou d'un Pokémon sur la carte, pas le cadeau ni la capture (celui de Salamèche est levé dans `epee.sav` au 3e badge) :
    ces rencontres passent par le Pokédex ; les objets au sol (« z_r0101_ItemNormal_0 ») restent justes (ramassés) ;
  - montures d'Arceus : `FSYS_RIDE_OPEN_01` (Cerbyllin), `03` (Ursaking), `05` (Paragruel) et `07` (Farfurex) ont un nom
    dans FlagsEditorEX ; Gueriaigle (sans doute `09`) pas compté ;
  - Pokémon débloqués par la sauvegarde d'un autre jeu (Pikachu / Évoli Gigamax de Let's Go, Jirachi de DÉ/PS, Darkrai et
    Shaymin d'Arceus) comptés ; cadeaux mystère de DÉ/PS (Darkrai, Shaymin, Arceus) et Mew de la Poké Ball Plus non
    (`MYSTERY_GIFT` du générateur) ;
  - **pas de trace trouvée** : rang de la Tour de Combat d'Épée / Bouclier (seulement victoires et séries), missions
    d'Arceus (11 drapeaux `FEVE_SUB*` seulement), revanches des Monarques (bloc `KNobleRematchRecords` au format inconnu),
    Pokémon fixes d'Épée / Bouclier (ils reviennent), dresseurs (pas de lieu).
- Vérifié sur `epee.sav` dans le navigateur (3 badges, sans DLC : 18,6 %, aucun raté ; histoire 10/30, 202 Pokémon sur
  584, 117 objets, 2 rencontres) ;
  Bouclier, DÉ/PS et Arceus seulement en mémoire.
- Guide du joueur (chapitre « Complétion (100 %) ») à jour pour la 8e génération (2026-11-14).

### Reste à faire (8e génération, au 2026-11-13)
1. **Sauvegardes** de DÉ/PS et de Légendes Arceus à demander (`diamant-etincelant.sav`, `legendes-arceus.sav`…) : y
   vérifier les badges (ordre des drapeaux 124 à 131), les concours, les statues (une statue posée dans la base
   quitte-t-elle la liste ? l'étape redeviendrait « à faire »), et pour Arceus les drapeaux de l'histoire, les montures,
   les missions (deux sauvegardes juste avant et juste après une mission, comparer les blocs) et les revanches des
   Monarques.
2. Épée / Bouclier : une sauvegarde **avec les DLC**, pour les blocs des objets cachés de l'Isolarmure et de Couronneige,
   le Pokédex à trois régions et l'ordre des échanges.
3. Dresseurs : seulement avec la RomFS (`.xci` chiffré de l'utilisateur : il faut les clés de sa console), comme en 6e.

## Trouver de nouveaux drapeaux : paires de sauvegardes (toutes générations, 2026-11-14)
Quand FlagsEditorEX ne nomme pas le drapeau d'une étape (ou le nomme mal), la méthode sûre est une **paire de
sauvegardes** : l'une juste avant l'action, l'autre juste après, puis comparer tout ce qui a changé.

### Ce que fait l'utilisateur, dans le jeu
1. Se placer juste avant l'action (devant le PNJ, à l'entrée de l'arène, avant de rendre la mission), **sauvegarder**, et
   copier le fichier : `<jeu>-avant-<action>.sav` (ex. `legendes-arceus-avant-mission-12.sav`).
2. Faire **une seule** action (rendre une mission, battre un Coach, monter d'un rang), sans rien d'autre : pas de
   détour, pas de nouveau lieu visité, pas d'objet ramassé, pas d'heure qui change (chaque à-côté lève d'autres drapeaux
   qui brouillent la comparaison).
3. **Sauvegarder** tout de suite après, et copier le fichier : `<jeu>-apres-<action>.sav`.
4. Pour une action qui se répète ou monte par paliers (rang du Groupe Galaxie, Tour de Combat), une troisième
   sauvegarde après le palier suivant aide à distinguer un compteur d'un drapeau.
5. Déposer les fichiers dans `MonsutaPotaru.Tests/Sauvegardes/` (ou ailleurs : seule la partie de référence y reste,
   sous un nom clair, les paires peuvent être supprimées une fois exploitées) et dire à Claude quelle action sépare
   chaque paire. Récupérer une sauvegarde : émulateur (fichier brut, souvent sans extension), console 3DS (Checkpoint),
   Switch (JKSV : le fichier `main`).

### Paires utiles (au 2026-11-14)
- **Légendes Arceus** : avant / après une **mission secondaire** rendue, puis une **mission principale** ; avant / après
  la **revanche d'un Monarque** (bloc `KNobleRematchRecords`) ; avant / après la monture **Gueriaigle** (`FSYS_RIDE_OPEN_09` ?).
- **Épée / Bouclier** : avant / après une **montée de rang à la Tour de Combat** (Poké Ball → Super Ball…) ; une partie
  **avec les DLC** (blocs des objets cachés `0xE479EE37` et `0xE579EFCA`, Pokédex à trois régions) ; avant / après un
  **échange du jeu** (ordre de `FSYS_NPC_TRADE_01` à `11`).
- **Diamant Étincelant / Perle Scintillante** : avant / après un **badge** (drapeaux système 124 à 131) ; avant / après
  avoir **posé une statue** dans la base secrète (reste-t-elle dans `UndergroundItemList8b` ?).
- **X / Y** : avant / après une **enquête de Beladonis**.
- **Soleil / Lune, Let's Go** : avant / après une **épreuve** (ordre de `FE_SIREN_COMPLETE_01` à `08`), un **Coach**, un
  **échange contre une forme d'Alola** (ordre de `FE_FIELD_TRADE_FIRST_01` à `08`), un **titre d'Expert** (`TF:n`).

### Ce que fait Claude : comparer
- Un petit projet console dans un dossier temporaire (scratchpad), avec le paquet `PKHeX.Core` de la version de
  `MonsutaPotaru.csproj` ; `SaveUtil.GetSaveFile(octets)` sur les deux fichiers, puis lister **tout ce qui diffère** :
  - 8e génération, Épée / Bouclier et Arceus : chaque bloc de `sav.Accessor.BlockInfo` (même clé) dont le type
    (`Bool1` → `Bool2`) ou les octets changent ; DÉ/PS : `FlagWork.GetFlag(n)` (0 à `CountFlag`), `GetSystemFlag(n)`,
    `GetWork(n)`, `BattleTrainer.GetIsWin(n)`, et `UndergroundItemList8b.ReadItems()` pour les statues ;
  - 5e à 7e génération : `EventWork.GetEventFlag(n)` et `EventWork.GetWork(n)` (Let's Go : `EventWork.GetFlag(n)`) ;
    1re à 4e génération : drapeaux d'événements et variables de script, comme dans les générateurs.
- Rapprocher chaque changement de la liste de FlagsEditorEX du jeu (même clé ou même numéro, colonne du nom interne) ;
  écarter le bruit : temps de jeu, coordonnées, date et heure, graines aléatoires, compteurs de pas, drapeaux d'arrivée
  dans un lieu (`FSYS_ARRIVE_*`), apparitions d'objets ou de PNJ (`z_…`, `FV_…`).
- Un drapeau qui ne change qu'avec l'action est le bon : l'ajouter au générateur (`c.f('NOM')`, ou `X:clé` / `E:n`
  sans nom, avec un commentaire qui dit d'où il vient), régénérer la table, ajouter un test sur la sauvegarde « après »
  (et « avant » : l'étape n'y est pas faite), noter le résultat ici et retirer la ligne de « Paires utiles ».

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
