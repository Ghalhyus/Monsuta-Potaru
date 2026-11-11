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
- **Ratés** connus : les 30 Rockets de la Sylphe SARL (disparus après Giovanni au 11e étage, `SilphCo11F.asm`), le premier
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
  Pokébip) ont servi pour l'histoire, les rencontres et les quêtes ; exclus d'office : GS Ball et Celebi de Cristal,
  Œuf Bizarre (impossibles sur cartouche française) ; Celebi reste dans le Pokédex (251), comme Mew.
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

## Ajouter une génération
- Une nouvelle table et sa lecture (`Gen2Completion` sert de modèle) ; `CompletionGames.For` et `Steps` choisissent
  selon la sauvegarde. Garder les sections au même poids ; `CompletionProgress.Of` ne montre que celles du jeu.
- PKHeX a des noms de drapeaux à partir de la 2e génération (`Resources/text/script/gen2/flags_*`), mais incomplets
  (152 pour Or / Argent) : les désassemblages restent la source.
