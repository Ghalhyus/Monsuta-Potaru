# Dresseurs de la 7e génération : reprendre le travail

Fiche de reprise (2026-11-19) pour les dresseurs de Soleil, Lune, Ultra-Soleil, Ultra-Lune et Let's Go dans la complétion.
Le reste de la complétion de la 7e génération est dans `completion.md` (« Soleil / Lune / Ultra-Soleil / Ultra-Lune /
Let's Go »). Lire cette fiche en entier avant de toucher aux dresseurs : elle dit ce qui marche, ce qui a déjà échoué
(ne pas le refaire) et ce qu'il faut demander à l'utilisateur.

## Où on en est

- **Fait** : les dresseurs **des routes** (ceux qui voient le joueur), 180 en Soleil comme en Lune (mêmes données), 239
  en Ultra (Team Rainbow Rocket comprise). Ultra-Lune reprend Ultra-Soleil : vérifié le 2026-11-20 sur la ROM
  d'Ultra-Lune (`Pokemon Ultra Moon (Europe) (…).3ds`), mêmes 652 dresseurs, mêmes équipes, mêmes lieux des routes. Section `Trainers` de
  `Gen7CompletionData.cs`, condition `E:<drapeau>`, équipe affichée (comme en 6e génération), un lieu par dresseur.
- **Fait le 2026-11-20** : les combats lancés par un script, choix de l'utilisateur : au **premier de leurs lieux** dans
  l'ordre du jeu quand plusieurs zones les citent, bloc **« Lieu inconnu »** (dernier lieu de l'ordre) quand aucune ;
  variantes d'un combat en une ligne (`E:a|E:b|E:c`, étiquette « Tili (équipe selon ton premier Pokémon) » : trois
  variantes = Pokémon de départ, deux = version, plus = « ta partie »). Hors section, déjà dans l'histoire :
  `STORY_CLASSES` (Doyen, Doyenne, Capitaine, Conseil 4) et `STORY_TRAINERS` du générateur (dernier combat de la Ligue,
  Guzma au Manoir Chelou, Elsa-Mina au Paradis Æther ; le générateur vérifie leur nom). Soleil / Lune : 81 lignes (17 sans
  lieu), Ultra : 140 (23 sans lieu). Restent : les 10 dresseurs battus introuvables (voir plus bas) et les scripts sans
  lieu.
- **Pas fait, bloqué** : Let's Go (RomFS Switch chiffrée, à extraire par l'utilisateur avec les clés de sa console).
- Choix de l'utilisateur toujours valables : vrai lieu de chaque dresseur (il a refusé la « section sans lieux » le
  2026-11-19) ; recherche longue limitée dans le temps, point avec lui après 2 ou 3 impasses ; combats de scénario
  « comme en 6e » (comptés seulement si le jeu lève leur drapeau ; en 7e il le lève toujours) ; classes déjà comptées dans
  l'histoire (Doyens, Capitaines, Conseil 4, Maître) hors de « Dresseurs ».

## Fichiers

| Fichier | Rôle |
|---|---|
| `outils/completion_gen7_rom.py` | Lit une ROM (zones, mondes, aires, bloc ET, scripts d'événements et table des numéros de script, données et textes des dresseurs). Formats dans sa docstring. |
| `outils/completion_gen7.py` | Générateur de la table ; `trainer_rows` transforme les dresseurs en lignes (`STORY_CLASSES`, `STORY_TRAINERS` : combats déjà dans l'histoire). |
| `outils/completion_gen7_ordre.json` | Ordre des lieux : un lieu de dresseur absent arrête le générateur (« Lieux absents »). |
| `outils/completion_gen6_rom.py` | `RomFS`, `garc`, `lz11`, `amx` (scripts AMX), `texts` : repris par la 7e. |
| `MonsutaPotaru.Tests/CompletionGen7Tests.cs` | Comptes des tables (dresseurs compris), test sur `ultra-soleil.sav` (40 dresseurs battus : 25 des routes, 15 combats de scripts). |

## Préparer l'environnement

- **ROM déchiffrées** de l'utilisateur, dans `C:\Users\ghalh\Documents\FanGame\Jeux\3DS\` :
  `Pokemon Sun (USA) (En,Ja,Fr,De,Es,It,Zh,Ko).3ds`, `Pokemon Moon (USA) (…).3ds`, `Pokemon Ultra Sun (USA) (…).3ds`
  (toutes les langues, dont le français), et `Pokemon Ultra Moon (Europe) (…).3ds`. Dans `…\Jeux\DS\FR\` : les cartouches françaises de la
  4e et de la 5e génération. Switch (`…\Jeux\Switch\`) : `Pokémon Let’s Go, Eevee!.nsp`, `Pokemon Legends Arceus.nsp`,
  `Pokemon Sword[v1.3.2+2DLC][US].xci`, **chiffrés**. Le dossier « Divine Sword & Blessed Shield » est un hack : ses
  données de dresseurs sont modifiées, inutilisables.
- **Dossier d'entrée du générateur** : pas dans le dépôt ; le recréer comme dit `completion.md` (« Reprendre sur un
  autre PC (7e génération) »). Puis :
  `python outils/completion_gen7.py <dossier> <ROM Soleil> <ROM Lune> <ROM Ultra-Soleil> MonsutaPotaru/Services/Completion/Gen7CompletionData.cs`
  (environ 3 minutes, la lecture des aires est lente). Sortie attendue : Sun / Moon `Story 28, Trainers 261, Items 408,
  Encounters 25, Quests 134`, Ultra `30, 379, 482, 58, 140` ; `git diff` vide sur la table si rien n'a changé.
- **Désassembleur ARM** : devkitARM est installé (pour PokeBank Sync) :
  `C:\devkitPro\devkitARM\bin\arm-none-eabi-objdump.exe -b binary -m arm -D --start-address=0x… --stop-address=0x… <fichier>`.
  Les adresses d'un `.cro` sont celles du fichier (son code commence au décalage 0x180).
- Python : lancer ses propres scripts d'exploration **sans** `-I` mais avec `PYTHONPATH` sur leur dossier (sinon ils ne
  trouvent pas leurs modules) ; `PYTHONIOENCODING=utf-8` pour les noms français. Ne pas écrire de heredoc avec des
  apostrophes ou des références arrière (voir les pièges de `completion.md`) : passer par l'outil d'écriture de fichiers.

## Formats déjà décodés

En plus de la docstring de `completion_gen7_rom.py` :
- **Blocs d'événements** (« ED » d'une aire) dans l'ordre, en Soleil : EP, EM, EM, EB, ES, EA, EA, **ET**, EG, ET, EI, EB,
  FS, FS, PR, ES (16 ; 17 en Ultra) : prendre le premier « ET » par sa magie, pas par son rang. Le second « ET » n'a pas
  le même format.
- **Personnages** (premier bloc EM, enregistrements de type 1, 120 octets, u32) : mot 9 drapeau d'apparition, 11 numéro
  local (= numéro d'événement, copié par le moteur au décalage 0x2C), 12 « genre », 13 numéro de script (copié au
  décalage 0x34 : un personnage dont ce numéro vaut 1000 + n serait un dresseur, mais les dresseurs des routes sont dans
  le bloc ET, pas ici), 14 cas du script de la zone, 28 et 29 décalages vers des trajets (flottants). Le « genre » 9 n'est
  **pas** « dresseur » (la Plage Hano-Hano n'en a aucun et a six nageurs).
- **Scripts de zone** (ZS, AMX compressé) : une fonction publique aiguille par `casetbl` (opcode 0x82 : nombre, défaut,
  puis paires valeur / saut relatif) ; le cas d'un personnage = son mot 14. L'appel `(n, 1, 0, 7, 1)` (5 arguments, n =
  0 à 39) est un geste commun des personnages, pas un combat.
- **Natives AMX** : table de 8 octets (0, hachage) ; hachage du nom **inconnu** (essayés sans succès sur les noms de
  `FieldRo.cro` : FNV-1 / 1a 32 et 64 bits, djb2, sdbm, Java, CRC32, murmur3, Jenkins, ELF, en minuscules et majuscules,
  UTF-16). Natives reconnues par corrélation : combat 0x6393be9a, lever un drapeau 0x13413496, le tester 0x133daa6a,
  l'effacer 0xaccbbbd7 (les mêmes qu'en 6e).
- **Code du jeu** : `code.bin` de l'ExeFS (non compressé en Soleil ; chargé à 0x100000) et les modules `.cro` de la
  RomFS (`FieldRo.cro` = le terrain). Les noms des natives sont des chaînes de `FieldRo.cro`, rangées dans une table
  (pointeur du nom, pointeur de la fonction) que remplissent les relocations du module. Trouvé en Soleil :
  `TrainerIDGet` 0xb5a4 (numéro de script moins une base : 1000, ou une seconde base au-delà), `TrainerGetInfo` 0xcefc,
  `CallTrainerBattleCore` 0x1244c, `TrainerGetScrID` 0xd8e4 et `TrainerGetEventID` 0xf1b0 (lisent l'emplacement du
  dresseur 0 ou 1, décalages 0x268 et 0x25C), remplissage de cet emplacement à 0x64050 (copie des décalages 0x2C et 0x34
  de l'enregistrement du personnage). Les noms des scripts (`trainer.amx`, `main_event_1000.amx`… `sub_event_*.amx`)
  sont dans `code.bin` (vers 0x520d5a), avec une table de pointeurs vers eux (vers 0x55f310).

### Extraire le code (à réécrire, les scripts de la session n'ont pas été gardés)

- **ExeFS d'un .3ds** : partition 0 à `u32(0x120) * 0x200` ; dans la NCCH, en-tête étendu à +0x200 (octet 0x0D bit 0 :
  code compressé en « BLZ », LZ à rebours), ExeFS à `u32(+0x1A0) * 0x200`, en-tête de 0x200 octets : 10 entrées (nom de
  8 octets, décalage, taille), données après l'en-tête ; l'entrée `.code` est `code.bin`. Un `.cro` se lit dans la RomFS
  (`RomFS(path).read('FieldRo.cro')`).
- **Relocations d'un CRO** (3dbrew, « CRO0 » au décalage 0x80) : segments à `u32(0xC8)`, nombre à `u32(0xCC)` (12 octets
  chacun : décalage, taille, type 0 code / 1 données en lecture seule / 2 données / 3 BSS) ; relocations internes à
  `u32(0x128)`, nombre à `u32(0x12C)`, 12 octets : u32 emplacement (`(décalage << 4) | segment`), u8 type, u8 segment
  visé, 2 octets, s32 ajout. L'emplacement est `segment[tag & 0xF].décalage + (tag >> 4)`, la valeur pointée
  `segment[visé].décalage + ajout`. Pour trouver la fonction d'une native : chercher l'emplacement qui pointe vers la
  chaîne de son nom ; le mot suivant pointe vers la fonction.
- **Trouver qui écrit un champ** : chercher dans le code les instructions `str rX, [rY, #décalage]`
  (`(mot & 0x0FF00FFF) == 0x05800000 | décalage`), puis désassembler autour.

## Méthodes qui ont marché

1. **Recherche statistique d'une base** : pour chaque zone, chaque valeur u16 / u32 de chaque bloc d'événements moins
   chaque numéro de dresseur que FlagsEditorEX situe au lieu de la zone ; la base (bloc, largeur, valeur) qui revient
   dans le plus de zones gagne (bloc ET, base 1000 : 48 zones ; le suivant : 34, du bruit). À refaire pour tout champ qui
   « contient sans doute un numéro ».
2. **Contrôle par FlagsEditorEX** (Soleil / Lune : `TRAINER BATTLE`, lieu en anglais, dresseur n = drapeau − 0x81C) :
   161 sur 177 au même lieu, les autres écrits autrement. FlagsEditorEX se trompe parfois de nom (n° 77 « Cook Ernie »
   alors que la ROM a Tili).
3. **Contrôle par la sauvegarde** : `ultra-soleil.sav` (début d'Akala) a 52 drapeaux de dresseurs levés (0xBDC + n) ;
   les dresseurs bien situés doivent tous être à Mele-Mele ou au début d'Akala. Pour relire les drapeaux : un petit
   projet console avec `PKHeX.Core` (version du `.csproj`), `SaveUtil.GetSaveFile`, `((SAV7)sav).EventWork.GetEventFlag(n)`
   pour n de 0 à 4 959.

## Impasses (ne pas refaire)

- Les fichiers d'une aire ne contiennent pas le numéro des dresseurs des routes sous forme brute (u16 / u32, 3000 + n,
  5000 + n) en dehors du bloc ET ; la grille « TR » est du terrain ; les données de dresseurs (`a/1/0/5`) n'ont pas de
  lieu ; aucune table `T[local] = numéro` dans la RomFS (recherche brute, très lente en Python : plus de 10 minutes).
- « Atteint la native de combat » en suivant les appels à grande profondeur : presque toutes les fonctions y arrivent,
  faux combats.
- **Combats des scripts de zone** : les appels `push.c n ; push.c 4 ; call F` où F appelle lui-même la native de combat
  existent bien (55 dresseurs en Soleil, 59 en Ultra, dont des fictifs « {v} » avec un Manglouton niv. 5), mais n suit
  l'ordre des aires, pas le lieu du combat : Pectorius à Ekaeka, Althéo à la Plage Hano-Hano, Royal Mask à la Route 10,
  Alyxia à la Route 13, Dexio au Mont Hokulani. Un dresseur des routes (le 28, Route 1) apparaît aussi dans un script de
  la Route 4. Le sens de ces appels n'est pas compris.

## Combats lancés par un script (2026-11-20)

Méthode qui marche (scripts d'exploration perdus avec le dossier temporaire : à réécrire d'après ceci) :
- **Noms des scripts** : la table de pointeurs de `code.bin` (Soleil 0x55f310, Ultra-Soleil 0x5907e0 ; repérée par le
  pointeur vers « trainer.amx ») range les noms dans l'ordre des fichiers de `a/0/9/2` : fichier i = nom i (0
  `trainer.amx`, 1 `init_scr.amx`, 2 `common_scr.amx`…, puis `main_event_*`, `sub_event_*` ; les `debug_*` de la fin
  n'ont pas de fichier). Pour l'exploration seulement : le générateur n'en a pas besoin.
- **Numéros de script → fichier** : table de tranches, Soleil / Lune `a/1/3/9`, Ultra `a/1/4/3` (membre 0, non
  compressé ; `a/1/4/0` et `a/1/4/4` : la même avec les scripts de débogage). Enregistrements de 10 octets, u16 : premier
  numéro, dernier numéro, genre (1 ; 2 pour le débogage), fichier de textes (?), fichier de `a/0/9/2`. Par exemple
  1000–2999 et 3000–4999 → 0 `trainer.amx` (d'où la base 1000 des dresseurs des routes), Ultra-Soleil 30560–30579 → 80
  `main_event_0281`. Le moteur fait la même recherche (Ultra-Soleil `code.bin` 0x37e97c, Soleil 0x3750f0 : de la
  dernière tranche à la première, la première dont le début ≤ numéro).
- **Lieu d'un script d'événement** : les zones qui citent un numéro de sa tranche (u32 ≥ 5000) dans leurs blocs
  d'événements (surtout EP, premier bloc, et EM), dans leur script de zone (ZS) ou d'initialisation (ZI ; `push.c` ou
  `const` 32 bits `0x8a`). Un script lancé par un autre (qui cite sa tranche) hérite de ses lieux s'il n'en a pas.
- **Combat** : la native 0x549eec84 (8 arguments : 0, dresseur, partenaire, type…), appelée par de petites fonctions
  locales à 7 arguments (souvent précédée de la native 0x0343a87b) qui lui passent leurs paramètres : suivre ces
  fonctions, prendre les constantes poussées. Le rival et d'autres ont un numéro selon le Pokémon de départ : le script
  choisit (`casetbl` sur 722 / 725 / 728) et range le numéro dans une variable locale (`const.s` : opcode 0x9d, décalage,
  valeur ; ou `const.pri` 0x27 valeur puis `stor.s.pri` packé 0xb1) qui est poussée (`push.s` négatif) : prendre toutes
  les constantes rangées dans cette variable par la fonction. Trois numéros qui se suivent (Tili 9 / 10 / 11…) = un seul
  combat, « l'un ou l'autre » (les conditions acceptent `|`).
- **Résultat** : Ultra-Soleil 201 dresseurs (39 sans lieu, 18 dans plusieurs lieux), Soleil 131 (34 sans lieu, 5 dans
  plusieurs). Lieux justes partout où on peut vérifier : élèves de l'École de Dresseurs et Prof Élodie à la Route 1,
  Pectorius à Lili'i, Althéo à Ekaeka, Dexio et Sina à Ho'ohale, Tili à la Route 1, aux Routes 2 / 3, à Ohana, au Dôme
  Royal, à Malié, au Mont Lanakila et à la Ligue, Gladio à la Route 5, Alyxia aux Ruines de l'Éveil, Guzma au Parc de
  Malié et au Manoir Chelou, Fondation Æther au Paradis Æther, Team Rainbow Rocket au Château Rocket, Red et Blue à
  l'Arbre de Combat. `ultra-soleil.sav` : 17 des 27 combats scénarisés battus retrouvés, tous à Mele-Mele ou au début
  d'Akala.
- **Pas trouvés** : 10 dresseurs battus de `ultra-soleil.sav` (16 Chaz, 18 Vaurien {v}, 175 Pierrot, 192 Séraphine, 196
  Simone, 205 Kenny, 209 Roméo, 211 Sonia, 431 Chantal, 536 Faidra). Ni dans le bloc ET (ni le second ET, des trajets),
  ni à 3000 + n dans les personnages (EM mot 13 toujours < 1000), ni drapeau levé directement par un script ; leurs
  numéros dans `join_festa_*.amx` sont des numéros de personnages, pas de dresseurs. Sans doute des dresseurs à qui l'on
  parle, par un mécanisme encore inconnu.
- Impasses de la recherche (ne pas refaire) : le mot 12 des déclencheurs ES et le mot 13 des personnages EM pris comme
  fichier de `a/0/9/2` (ce sont des numéros locaux) ; la native 0x6393be9a n'est **pas** le combat en 7e génération
  (une attente ou un fondu) ; la native 0xb186d00b (7 arguments) reçoit des numéros de personnages, pas de dresseurs.

## Pistes pour la suite

- **Combats lancés par un script** : voir « Combats des scripts d'événements » ; restent les scripts sans lieu (chercher
  qui les lance : autre forme de numéro, table d'événements par étape de l'histoire ?) et les 10 dresseurs non trouvés.
- **Let's Go** : quand l'utilisateur aura extrait la RomFS (dossier `romfs`, mises à jour comprises), formats Switch
  différents de la 3DS (à chercher). FlagsEditorEX : 613 drapeaux de dresseurs à partir de 0x8B6 ; les Coachs sont une
  classe à part.
- **Saphir Alpha** (6e génération) : même situation, dresseurs déduits de Rubis Oméga.

## Après un changement

Régénérer la table, mettre à jour les comptes de `CompletionGen7Tests` (et la sortie attendue ci-dessus), lancer
`dotnet test … --filter "FullyQualifiedName~Completion"`, vérifier dans le navigateur avec `ultra-soleil.sav`
(`navigateur.md` ; section « Dresseurs » de la complétion : 40 / 379 au 2026-11-20), puis mettre à jour cette fiche, le
texte d'aide de `CompletionList.razor`, le README (« Complétion (100 %) ») et le chapitre « Complétion » du guide du
joueur.
