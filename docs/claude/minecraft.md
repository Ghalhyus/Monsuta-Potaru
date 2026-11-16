# Minecraft : All the Mons (Cobblemon)

README : « Minecraft (All the Mons, Cobblemon) ». Code : `MonsutaPotaru/Services/Cobblemon/`,
`Components/Shared/WorldFolderPicker.razor`, `wwwroot/js/app.js` (`watchWorldFolder`).

## Format des fichiers (Cobblemon 1.8.4, NeoForge 1.21.1)
- Réglage `storageFormat: "nbt"` (`config/cobblemon/main.json`, par défaut) : `pokemon/playerpartystore/<2 premiers
  caractères de l'UUID>/<UUID du joueur>.dat` (équipe) et `pokemon/pcstore/.../<UUID>.dat` (PC), NBT compressé en gzip.
  Le format JSON est refusé (message clair).
- Équipe : `SlotCount` (6), `Slot0`…`Slot5`. PC : `BoxCount` (100 dans All the Mons), `BoxCountLocked`, `Box0`… avec
  `BoxWallpaper`, `BoxName` (facultatif) et `Slot0`…`Slot29` ; `BackupStore` (Pokémon en trop après une réduction du PC),
  `UnlockedWallpapers`, `UnseenWallpapers` : tout est gardé tel quel.
- Pokémon : `UUID` (4 entiers), `Species` (`cobblemon:fennekin`), `FormId` (`normal`, `alola`…), `Nickname` (texte, ou
  composant de texte), `Level`, `Gender` (`MALE` / `FEMALE` / `GENDERLESS`), `Shiny`, `Nature` / `MintedNature`
  (`cobblemon:quirky`), `Ability.AbilityName`, `MoveSet[].MoveName`, `IVs.Base` / `EVs` (`cobblemon:hp`…),
  `HeldItem` (pile d'objets : `id`), `CaughtBall`, `PokemonOriginalTrainer` (UUID du joueur, ou nom pour un dresseur non
  joueur : `PokemonOriginalTrainerName`), `TetheringId` (dans un enclos), `cobblemon:data_version`.
- `level.dat` (NBT gzip) : `Data.LevelName`, `Data.WorldGenSettings.seed` (`Data.RandomSeed` avant la 1.16),
  `Data.Player` (le joueur solo). `stats/<UUID>.json` : `stats["minecraft:custom"]["minecraft:play_time"]`, en ticks
  (20 par seconde), écrit quand le monde est enregistré.
- Noms des champs retrouvés dans le mod (`com/cobblemon/mod/common/util/DataKeys.class`, `PCStore`, `PCBox`,
  `PartyStore`) : chaînes du pool de constantes des `.class` (un petit script Python suffit, pas de décompilateur).
- Identifiants de Cobblemon = identifiants de Showdown : `CobblemonPokemon.Key` les retrouve dans les noms anglais de
  PKHeX (minuscules, sans accents ni ponctuation, ♀/♂ → f/m). Formes : noms anglais de `FormConverter` en 9e génération
  (`CobblemonPokemon.FormContext`). Inconnu de PKHeX (objet de Minecraft, espèce d'un autre mod) : 0, et l'identifiant
  affiché (`ShortId`).

## Zip ou dossier
- `CobblemonSave.TryRead(zip)` : les fichiers de l'équipe et du PC sont cherchés sous `<racine>/pokemon/…` (racine
  vide, ou le dossier du monde : « Kantalia/… », `\` de Windows acceptés) ; `level.dat` et `stats/<joueur>.json` sous
  la même racine. Tout le reste est ignoré et n'est pas gardé (`Bundle` : pokemon, level.dat, stats, chemins depuis le
  monde). Zip piégé : 20 000 entrées, 64 Mo décompressés au plus, taille annoncée vérifiée (`CopyAtMost`).
- `Write()` : le zip rendu au joueur, seulement `pokemon/…` (avec les `.dat.old`). `level.dat` et `stats` ne sont jamais
  rendus : le joueur a pu rejouer, ses fichiers seraient remplacés par une vieille copie.
- **Dossier choisi** : `<input type="file" webkitdirectory>` (pas `InputFile` : Blazor enverrait au serveur le nom et la
  taille de chaque fichier du dossier). `watchWorldFolder` (app.js) : racine = premier segment de `webkitRelativePath` ;
  pas de `<racine>/level.dat` → code « pas-un-monde » sans rien lire ; sinon seuls les chemins attendus (même expression
  que `CobblemonSave.FolderFile`) sont ouverts (`arrayBuffer`), au plus 32 fichiers, 4 Mo chacun, 8 Mo en tout (code
  « trop-gros »). Envoi : « MPW1 », longueur de l'en-tête (petit-boutiste), en-tête JSON `[{n, s}]`, contenus. Le
  composant appelle `take` (flux `IJSStreamReference`, 8 Mo au plus), puis `ZipFromFolder` revérifie tout (chemins exacts,
  doublons, tailles, longueur totale) et en fait un zip ; le reste du site ne voit qu'un zip. `FolderFileName` : nom du
  dossier réduit aux lettres, chiffres, espaces, `-`, `_`, `.` (60 caractères), + `.zip`.
- Les refus venus du navigateur sont des codes fixes (`OnWorldRefused`), traduits côté serveur : jamais un texte de la page.

## Choix
- **Mondes séparés** (décision de l'utilisateur) : aucune conversion entre Minecraft et les jeux.
- Le NBT du Pokémon est gardé intact en base (`BankPokemon.Data`, sans gzip) ; les colonnes d'affichage et de filtres sont
  remplies à l'enregistrement (`BankService.CreateEntry(info, CobblemonPokemon…)`), `IsLegal` vrai (pas d'analyse : ne
  pas le compter « illégal » dans les filtres), `PokemonView.HasLegality` faux (ni « Légal » ni « Illégal » sur la fiche).
  Pas de migration : la marque est `Extension = "cobblemon"`, `Context` = Gen9 (noms des formes pour les images).
- `BankService.ToPkm` rend null pour eux : tout code qui lit les octets avec PKHeX les ignore. Écartés explicitement
  (requêtes) : GTS (`PickProblem`, `OwnedRowsAsync`), rubans (`UnavailableAsync`, `AddAsync`), parc (`Eligible`), living
  dex global (`GlobalCopiesAsync`). Un nouveau service qui parcourt `BankPokemon` doit en faire autant.
- Dresseur affiché : `PokemonOriginalTrainerName`, sinon le pseudo du compte si l'UUID est celui du joueur du monde,
  sinon « Joueur Minecraft » (`BankService.TrainerName`).
- Pages de `SaveSession` : 0 = équipe, puis les boîtes ; un retrait sans case choisie commence au PC
  (`SaveSession.DefaultStart`). `Bank.razor` passe par `SlotAt` (case d'un format ou de l'autre) et `SlotIndex` /
  `SlotAtIndex` (boîtes de tailles différentes pour Maj+clic).
- `.dat.old` : copie de secours que Cobblemon relit si le `.dat` est illisible ; réécrite identique au `.dat`.

## Mondes stockés (« Mes sauvegardes »)
- `StoredSaveService.AddAsync` / `UpdateAsync` aiguillent un zip vers `AddWorldAsync` / `UpdateWorldAsync`. Identité
  `cobblemon:<joueur>:<graine>` (`CobblemonSave.IdentityPrefix`, `StoredSaveService.IsWorld`), `Generation` 0
  (`StoredSaveInfo.IsMinecraft`), `TrainerName` = nom du monde, `TrainerId` vide (la page n'affiche pas « () »).
- Jamais de trace `SaveModification` (le serveur ne modifie pas un monde) : l'anti-copie repose sur les UUID des Pokémon
  (doublons) et le temps de jeu (confirmation).
- PKHeX ne lit jamais un zip (`StoredSaveService.TryRead`). Listes qui l'écartent explicitement : inventaire
  (`ListMoneySourcesAsync`, `ListItemSourcesAsync`), échange interne (`InternalSavesAsync`, génération 0), 3DS
  (`ListForDeviceAsync`). Un nouveau service qui parcourt `StoredSaves` doit en faire autant.
- `ForPlayer` : « Télécharger » et la suppression rendent `Write()` (dossier pokemon seul) ; la copie de sécurité garde
  le paquet entier.

## Tests
- `MonsutaPotaru.Tests/Sauvegardes/all-the-mons.zip` : vrai monde (Kantalia) : `pokemon` (un Feunnec dans l'équipe,
  PC vide, pris à 18 h 33), `level.dat` et `stats` (pris à 19 h 02 : 29 598 ticks). `RealSavesTests` ignore les `.zip`.
  `CobblemonTests.Mon(espèce, n, modification)` fabrique d'autres Pokémon (UUID propre à `n`) ; `World(...)` les met
  dans la première boîte (paquet complet) ; `WorldVariant(graine, temps de jeu, sans level.dat, sans stats)` ;
  `InWorld(zip rendu)` refait ce que fait le joueur (décompresser dans le monde) ; `FolderPayload` / `RealFolderPayload`
  : ce qu'envoie le navigateur.
- Aller-retour NBT à l'octet près sur les vrais fichiers (`Nbt_RoundTrip_IsByteExact`).
- `StoredWorldTests` (service, exclusions), `StoredWorldPageTests` (pages ; le JavaScript est simulé en appelant
  `WorldFolderPicker.ReceiveAsync` et `OnWorldRefused`). Le vrai script : voir `navigateur.md`.
