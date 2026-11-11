# Complétion (100 %)

README : « Complétion (100 %) ». Code : `Services/Completion/` ; pages `CompletionList.razor` (le nom `Completions`
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

## Ajouter une génération
- Une nouvelle table et sa lecture (`Gen1Completion` sert de modèle) ; `CompletionService.Steps` choisit selon la
  sauvegarde ; `Gen1Completion.Games` → une liste par génération. Garder les sections au même poids.
- PKHeX a des noms de drapeaux à partir de la 2e génération (`Resources/text/script/gen2/flags_*`), pas pour la 1re.
