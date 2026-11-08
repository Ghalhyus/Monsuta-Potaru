# Living dex

README : « Living dex ». Jeux et modes : `LivingDexGames` (`Services/LivingDexService.cs`) ; global : `GlobalLivingDex`.

## Ajouter un jeu
- Son entrée dans `All` : jaquette, dossier de sprites, Pokédex régional, dernier numéro national.
- `For(SaveFile)` : quelle sauvegarde. PKHeX ne distingue pas Rouge de Bleu, Or d'Argent, Rubis de Saphir, Rouge Feu de
  Vert Feuille, ni Diamant de Perle. HeartGold de SoulSilver, si (depuis PKHeX 26 : `soulsilver.sav` est reconnue SoulSilver).
  Rouge Feu / Vert Feuille : `SaveVersion.Refine` (appelé par `StoredSaveService.TryRead` et `SaveSession.TryLoad`) met
  `sav.Version` à LG d'après les Pokémon du dresseur ; `For` propose quand même les deux. Une sauvegarde lue directement
  par `SaveUtil` (certains tests) reste FR. `SaveIdentity` force FR pour ne pas changer la clé.
- Les formes du mode Complet (`Forms`) : régionales, Méga, Gigamax, d'aspect, lettres de Zarbi ; pas le sexe ;
  **seulement celles qu'une sauvegarde du jeu peut contenir** :
  - les formes de Morphéo n'existent qu'en combat ;
  - Deoxys de 3e génération a la forme de son jeu (`DeoxysForm`, case `AnyForm`) ;
  - à partir de la 4e, le nombre de formes de chaque jeu vient de `PersonalTable` de PKHeX (`FormCount`, liste
    `AspectForms`), et les règles de légalité de PKHeX disent ce qu'une boîte peut garder (`FormVerifier` sur GitHub).
- Le premier numéro du Pokédex régional (`RegionalStart` : 0 pour Unys).
- Les images dans `outils/images.py` (`LIVINGDEX_SPRITES`, `LIVINGDEX_FORMS`, `LIVINGDEX_COVERS`, mêmes noms de dossiers
  et de fichiers que `LivingDexGames.Sprite` : « 201-b.png », « 386-speed.png »). Jusqu'à la 5e génération seulement ; à
  partir de la 6e, images de la collection (`SpriteFolder` null) : l'utilisateur trouve les sprites 3D hideux.
- Chaque forme a sa propre image (test `Complete_EachFormHasItsOwnNameAndSprite`).
- Vérifier que le mode Complet tient dans la sauvegarde (964 cases pour 966 places en Ultra-Soleil / Ultra-Lune).

## Préférences de l'utilisateur
- Ne jamais bloquer l'équipe pour une forme qui ne tient pas en boîte (Shaymin Céleste en 4e) : case `KeyItem`, remplie
  par un **deuxième** exemplaire de l'espèce et l'objet rare dans le sac (lu dans la sauvegarde stockée,
  `BagItemsAsync`), ce qui manque en infobulle (`LivingDexEntry.Note`).
- Conseil d'une case manquante : `LivingDexSlot.Hint` (saisons de Vivaldaim et Haydaim en 5e : seuls ceux de l'équipe
  changent de saison). Vérifier ce genre de règle avant de l'affirmer.
- Une seule case pour Prismillon (motifs), Arceus et Genesect. Espèce à beaucoup de formes : lui demander avant de toutes
  les compter (il a voulu les 7 noyaux de Météno ; retiré les casquettes de Pikachu et les Dominants).

## Par génération
- **6e, Méga-Évolutions** : cases `MegaItem` / `MegaMove`, remplies par un exemplaire **de plus** qui tient la gemme
  (`BankPokemon.HeldItem`) ; `EntriesAsync` leur attribue leurs exemplaires d'abord, les autres cases ne les reprennent
  pas. Gemme d'une forme : `ItemStorage9ZA.GetExpectedMegaStoneOrPrimalOrb` (formes limitées à celles du jeu,
  `FormCount` : PKHeX connaît aussi les Méga de Légendes Z-A). Méga-Rayquaza n'a pas de gemme : Draco-Ascension, lue dans
  les octets des seuls Rayquaza. Sous-Pokédex (Kalos) : `LivingDexGameInfo.Sections`, `RegionalNumber`.
- **7e** : Shaymin Céleste et Hoopa Déchaîné tiennent en boîte (`FormVerifier` : `Format <= 6`), cases ordinaires.
  Let's Go : ni objet tenu, Méga par la gemme du sac (`MegaInBag`), son Pokédex = son national
  (`LivingDexGameInfo.National`). Une sauvegarde Let's Go vide de PKHeX n'est pas reconnue par `SaveUtil` : écrire le pied
  « BEEF » et le numéro de bloc 0x13 (`LivingDexServiceTests.LetsGoWith`).
- **8e** : ce que le jeu peut contenir vient de `PersonalTable.IsPresentInGame` (`LivingDexGames.Present`), pas de
  `FormCount` seul (Épée / Bouclier : 664 espèces ; Légendes Arceus n'a que les Caninos de Hisui : `FirstForm`). Formes
  régionales par leur nom anglais chez PKHeX (« Alola », « Galar », « Hisui ») ; la casquette d'Alola de Pikachu s'appelle
  aussi « Alola ». Gigamax : case `Gigamax`, un exemplaire de plus avec le facteur (`IGigantamax`, lu dans les octets des
  seules espèces de `GigamaxSpecies`). Sauvegarde Légendes Arceus vide non reconnue par `SaveUtil` (pas de test du service
  pour ce jeu) ; Diamant Étincelant vide, si.
- **9e** : Écarlate / Violet seulement (`For` : `SAV9SV`) ; Légendes Z-A est aussi en 9e chez PKHeX, sans living dex.
  Sauvegarde vide non reconnue par `SaveUtil`, pas de vraie sauvegarde : images vérifiées par un test temporaire
  (`PokemonSprites.Load(RealSavesTests.WebRootWithImages())`, chaque case a son image), pas dans le navigateur.

## Living dex global (`GlobalLivingDex`)
- Sur le stockage interne (`BankPokemon.StoredSaveId` null), sans les chromatiques (voulu), image : Master Ball
  (`CoverItem`). Forme reconnue par son nom anglais chez PKHeX (`FormKey`, « Normal » pour la base, que PKHeX laisse vide
  dans un jeu où l'espèce n'a qu'une forme) ; chaque exemplaire pour une seule case (`Assign`).
- Complet = les cases Complet des jeux ; Ultime = en plus tout ce que les tables de PKHeX donnent par jeu, moins `Excluded`.
  Voulu : toutes les formes, même au même aspect et de couleur (pas les chromatiques), mâle / femelle, Spinda seul ; pas
  les formes de combat sans objet tenu ; pas Légendes Z-A.
- Nouveau jeu : son Complet entre tout seul dans le global ; ajouter sa table à `GlobalLivingDex.Games` pour l'Ultime.
- Chromatique (`GlobalShiny`) : les mêmes cases moins `ShinyLocked` (liste validée par l'utilisateur, d'après Bulbapedia ;
  Dominants retirés aussi), seulement les chromatiques. Image : Charme Chroma. Forme ou espèce jamais distribuée en
  chromatique : l'ajouter à `ShinyLocked`.

## Living dex chromatique d'un jeu (2026-11-08)
- Colonne `LivingDex.Shiny` (migration `ShinyLivingDex`, index unique OwnerId + Game + Shiny ; le `Down` supprime les
  chromatiques). Le global chromatique reste un **jeu à part** (`LivingDexGame.GlobalShiny`, `Shiny` à false en base) ;
  en mémoire, `LivingDexGameInfo.Shiny` est vrai pour les deux (`GlobalLivingDex.ShinyGame`, `LivingDexGames.Get(jeu, true)`,
  nom « Pokémon Platine chromatique »).
- Choix de l'utilisateur : **une sauvegarde chacun** (l'index unique sur `StoredSaveId` ne change pas) ; le normal
  continue de compter les chromatiques ; pas en 1re génération (`HasShiny`, aucun chromatique) ; Let's Go oui.
- **Un seul mode pour l'instant** : `Modes` rend le premier mode du jeu normal (Régional ; National pour Let's Go, son seul
  Pokédex). Pour ajouter National / Complet plus tard : `Modes` et `ShinyLocked` (aujourd'hui l'espèce seule, les formes
  jamais chromatiques comme Pichu Troizépi ou les casquettes n'ont pas de case en Régional), voir `GlobalLivingDex.ShinyLocked`.
- `OwnedAsync(…, shiny)` filtre `IsShiny` ; les cases de `ShinyLockedSpecies` sont retirées **après** `Entry` : les
  numéros régionaux suivent l'index de la case (Victini n° 000 en Unys : Vipélierre garde son n° 001).
- Sprites : `{SpriteFolder}chromatique/` (même noms que les normaux, `outils/images.py` les télécharge pour tous les
  dossiers sauf Rouge / Bleu et Jaune ; ceux de la 3e génération venaient déjà des combats) ; `Fallback` prend l'image
  chromatique de la collection.
- Vue générale : `?filtre=chromatiques|normaux` (`LivingDexKind`, `LivingDexGames.Matches`) ; en National / Complet, une
  carte chromatique compte dans son seul mode (« Régional seul », `ShownMode`) ; en Ultime, seul le global chromatique.
  Tri par génération : le chromatique suit le normal du même jeu dans les deux sens. Création : boutons Normal /
  Chromatique en tête (`ListCandidatesAsync(…, shiny)`, `TakenGamesAsync(…, shiny)`).
- Tests : `ShinyLivingDexTests` ; une deuxième copie d'une vraie sauvegarde est refusée (même identité, ou mêmes Pokémon :
  anti-copie) : fabriquer une autre partie (`SaveSessionTests.CreatePlatinumSave`) ou insérer la ligne en base.

## Images et jaquettes
- Les tests n'exigent **pas** ces images (la CI les copie du VPS, qui ne les reçoit qu'au déploiement) : tester le chemin,
  pas la présence.
- Jaquettes : Bulbagarden Archives (API MediaWiki, `iiurlwidth=480`). Sur ce PC, Python refuse son certificat (magasin de
  Windows) et Bulbagarden refuse Python (403) : `curl` avec l'agent « PokeBank (outils/images.py) » passe, ou un contexte
  SSL avec `cafile` (paquet de Git : `/usr/ssl/certs/ca-bundle.crt`), ou `outils\recuperer-images.cmd`. Sur le VPS, ça passe.

## Description complète (ancienne section « Living dex » du README, déplacée le 2026-11-08)

Le README n'en garde que les règles ; le mode d'emploi pour les joueurs est dans le guide du joueur (Google Doc).
Peut recouper les sections ci-dessus : en cas de désaccord, le code fait foi, corriger la fiche.

- **Vue générale** (`/livingdex`) : une carte par living dex (jaquette, sauvegarde, barre de progression, « x / y »).
  Boutons **Régional / National / Complet** pour la progression de toutes les cartes (`?mode=regional`, `?mode=complet`) :
  une carte dont le jeu n'a pas ce mode compte en National et l'indique (« National seul », 1re génération) ; un clic
  sur une carte ouvre le living dex dans le mode affiché. Même comptage que sur sa page (`LivingDexService.EntriesAsync`).
  **Tri** à côté (« Trier par » Génération / Complétion, et ↑ / ↓ pour le sens ; `?tri=completion`, `?ordre=desc`,
  `LivingDexGames.Sort`) : par génération, l'ordre des jeux ou son exact inverse ; par complétion, le pourcentage exact
  dans le mode affiché (celui de la carte, le National pour « National seul »), à égalité dans l'ordre des jeux.
- **Barres de progression** (cartes et page d'un living dex, `Shared/LivingDexProgress.razor`) : pourcentage au centre
  de la barre, avec une décimale **arrondie vers le bas** (« 5,6 % » ; 100 % seulement quand tout y est),
  `LivingDexGames.Percent`.
- **Création** : « + Créer un living dex », d'abord le **jeu** (jaquettes groupées par génération, **repliées** au
  départ : le titre de chaque génération, « ▸ 4e génération (5 jeux) », la déplie ou la replie ; repliée, celle du jeu
  choisi le montre encore ; un jeu qui a déjà son living dex est grisé ; boutons **Normal / Chromatique** en tête, voir
  « Living dex chromatique d'un jeu »), puis une de ses **sauvegardes** stockées pas encore liées. PKHeX reconnaît Jaune, Cristal,
  Émeraude, Platine, et depuis PKHeX 26 HeartGold et SoulSilver, mais pas Rouge de Bleu, Or d'Argent, Rubis de Saphir,
  Rouge Feu de Vert Feuille ni Diamant de Perle (même format) : une sauvegarde Rouge / Bleu est proposée pour Rouge comme
  pour Bleu, de même pour les autres paires (pour Rouge Feu / Vert Feuille, PKHeX répond toujours « Rouge Feu » faute de mieux ; le site devine Vert Feuille d'après les Pokémon du dresseur, voir « Rouge Feu ou Vert Feuille », mais propose toujours les deux). À partir
  de la 5e génération, chaque sauvegarde dit son jeu (Let's Go, Légendes Arceus et Écarlate / Violet compris). **Un living dex par jeu** et par compte, une
  sauvegarde pour un seul living dex (index uniques).
- **Comptage** : un Pokémon est enregistré s'il est dans l'**équipe ou les boîtes** de la sauvegarde liée (pas les œufs) ;
  le living dex suit donc chaque mise à jour de la sauvegarde (site ou 3DS). Le premier exemplaire (équipe, puis boîtes)
  remplit la case.
- **Modes** : Régional (le Pokédex de la région), National (tout le national que le jeu peut contenir) et Complet (chaque
  forme de chaque Pokémon du national : formes régionales, Méga-Évolutions, Gigamax, formes qui changent l'aspect comme
  Sancoki / Tritosor ou les lettres de Zarbi ; pas le sexe ; **seulement celles qu'une sauvegarde du jeu peut contenir**).
  En Régional et National, n'importe quelle forme d'une espèce compte ; en Complet, la forme exacte. **Un mode identique à
  un autre, ordre compris, est caché** :
  - 1re génération (une région, aucune forme) : le National seul, 151 Pokémon, sans onglets ;
  - 2e génération : **Régional** (Pokédex de Johto, `?mode=regional` : les mêmes 251 Pokémon que le national, dans l'ordre
    et avec les numéros du jeu, Germignon n° 1, Celebi n° 251 ; infobulle « Johto #001 Germignon (national #152) »),
    **National** (251) et **Complet** (276 : les 26 lettres de Zarbi, A à Z, enregistrées dans chaque Zarbi ; aucune autre
    forme dans ces jeux) ;
  - 3e génération : **Régional** (Hoenn pour Rubis, Saphir et Émeraude : 202 Pokémon, Arcko n° 1, Deoxys n° 202 ; Kanto
    pour Rouge Feu et Vert Feuille : les 151 premiers), **National** (386) et **Complet** (413 : les 28 lettres de Zarbi,
    avec « ! » et « ? »). Les formes de Morphéo n'existent qu'en combat : une seule case. **Deoxys** n'enregistre pas sa
    forme en 3e génération, c'est le jeu qui la donne : sa case (dans tous les modes) a la forme et l'image de celle du jeu
    (Normale dans Rubis / Saphir, Vitesse dans Émeraude, Attaque dans Rouge Feu, Défense dans Vert Feuille) et n'importe
    quel Deoxys la remplit. Les trois autres formes, impossibles dans la partie, ne comptent pas (une appli qui les compte,
    avec celles de Morphéo, arrive à 419) ;
  - 4e génération : **Régional** (Sinnoh de Diamant / Perle : 151, Tortipouss n° 1, Manaphy n° 151 ; Sinnoh agrandi de
    Platine : 210, Giratina n° 210 ; Johto de HeartGold / SoulSilver : 256, Celebi n° 256), **National** (493) et
    **Complet** : les 28 lettres de Zarbi, les 4 formes de Deoxys (chaque Deoxys garde la sienne en 4e), les 3 capes de
    Cheniti et de Cheniselle, les mers Occident / Orient de Sancoki et Tritosor (529 en Diamant / Perle) ; à partir de
    Platine, les 6 formes de Motisma, Giratina Originel (il tient l'Orbe Platiné, ce qui se garde en boîte) et Shaymin
    Céleste (536 en Platine) ; en HeartGold / SoulSilver, Pichu Troizépi (537 ; il ne peut pas aller dans les autres
    jeux). Les formes de chaque jeu viennent de PKHeX (`PersonalTable` : Motisma n'en a qu'une en Diamant / Perle).
    **Arceus** n'a qu'une case, quelle que soit sa Plaque ; Morphéo et Ceriflor non plus (formes de combat).
    **Shaymin Céleste** redevient Terrestre dans une boîte : pour ne pas bloquer l'équipe, sa case se remplit avec un
    **deuxième Shaymin** (n'importe quelle forme, le premier remplit la case Shaymin) **et la Gracidée** dans le sac de la
    sauvegarde (objet rare : la banque n'y touche pas) ; l'infobulle dit ce qui manque (« il faut un deuxième Shaymin et
    la Gracidée dans le sac ») ou « Gracidée dans le sac ». En Régional et National, un Shaymin suffit ;
  - 5e génération : **Régional** (Unys de Noire / Blanche : 156 Pokémon ; de Noire 2 / Blanche 2 : 301 ; numérotés **à
    partir de 0**, Victini n° 000, jusqu'à Genesect), **National** (649) et **Complet** : les formes de la 4e (sauf Pichu
    Troizépi, qui ne passe pas en 5e ; même règle pour Shaymin Céleste), Bargantua motif Bleu et les 4 saisons de
    Vivaldaim et Haydaim (699 en Noire / Blanche) ; en Noire 2 / Blanche 2, les formes Totémiques de Boréas, Fulguris et
    Démétéros, Kyurem Blanc et Noir, Keldeo Décidé (705). Seuls les Vivaldaim et Haydaim **de l'équipe** prennent la
    saison du jeu (au chargement) : en boîte, ils gardent la leur ; l'infobulle d'une saison manquante le rappelle (« à
    garder en boîte »). **Genesect** n'a qu'une case, quel que soit son Module (comme Arceus) ; Darumacho Transe et
    Meloetta Danse n'existent qu'en combat ;
  - 6e génération : **Régional** (Kalos de X / Y : trois sous-Pokédex à la suite, **Centre** 153, **Côtes** 153 et
    **Montagnes** 151, 457 Pokémon sans doublon, chacun avec son titre dans la grille et sa numérotation à partir de 1,
    infobulle « Kalos Côtes #001 Baudrive (national #425) » ; Hoenn de Rubis Oméga / Saphir Alpha : 211), **National**
    (721) et **Complet** (825 en X / Y, 847 en Rubis Oméga / Saphir Alpha) : les formes de la 5e (les saisons ne changent
    plus, sans conseil), les 5 fleurs de Flabébé, Floette (sans l'Éternelle, jamais distribuée) et Florges, les 4 tailles
    de Pitrouille et Banshitrouye. **Méga-Évolutions et Primo-Résurgences** (formes de combat) : une case chacune, remplie
    par un exemplaire **de plus** de l'espèce qui tient sa Méga-Gemme (Gemme Rouge / Bleue pour les Primo ; Méga-Rayquaza,
    sans gemme : un Rayquaza de plus qui connaît Draco-Ascension) ; la case de l'espèce en prend un autre (Dracaufeu : 3
    exemplaires) ; infobulle « il faut un Dracaufeu de plus avec Dracaufite X », ou « avec Mysdibulite ». 30 en X / Y,
    50 en Rubis Oméga / Saphir Alpha (le nombre de formes de chaque jeu vient de PKHeX). Rubis Oméga / Saphir Alpha
    seulement : **Hoopa Déchaîné** comme Shaymin Céleste (deuxième Hoopa et Vase Scellé dans le sac) et une case **Pikachu
    Cosplayeur**, remplie par n'importe quel costume (un seul par partie). Une seule case, quelle que soit la forme :
    Prismillon (et Lépidonille, Pérégrain : trop de motifs à rassembler), Couafarel (les coupes ne tiennent qu'en équipe),
    Mistigrix (sa forme est le sexe), Arceus, Genesect ; Exagide Assaut et Xerneas Déchaîné n'existent qu'en combat ;
  - 7e génération : **Régional** (Alola de Soleil / Lune : 302, Brindibou n° 1, Marshadow n° 302 ; d'Ultra-Soleil /
    Ultra-Lune : 403, Zeraora n° 403 ; une seule liste, les Pokédex des îles se recoupent), **National** (802 ; 807 en
    Ultra) et **Complet** (956 en Soleil / Lune, **964** en Ultra : ça tient, de justesse, dans les 960 places des boîtes
    et les 6 de l'équipe) : les formes de la 6e (Méga comprises, les 50 : PKHeX dit que chaque gemme peut se tenir en 7e,
    `ItemStorage7SM.GetAllHeld`), les **18 formes d'Alola** (« Rattata d'Alola »), les 4 styles de Plumeline, Lougaroc
    Diurne, Nocturne et Crépusculaire (Ultra seulement), Zygarde **50 % et 10 %** (avec ou sans Rassemblement : la même
    case), les **7 noyaux de Météno**, Necrozma Crinière du Couchant et Ailes de l'Aurore (Ultra). **Shaymin Céleste et
    Hoopa Déchaîné** tiennent en boîte à partir de la 7e (règles de légalité de PKHeX) : cases ordinaires, sans deuxième
    exemplaire ni objet. Une seule case : **Silvallié** (comme Arceus), Pikachu (les **casquettes** ne comptent pas :
    distributions terminées), Amphinobi ; un **Pokémon Dominant** (Ultra : seule la taille change) remplit la case de sa
    forme ordinaire (Rattatac Dominant : Rattatac d'Alola). Formes de combat exclues : Météno Météore, Ultra-Necrozma,
    Zygarde Parfait, Froussardine en banc, Mimiqui démasqué, Amphinobi Sacha ;
  - **Let's Go Pikachu / Évoli** : seulement les 151 de Kanto, **Meltan et Melmetal** (n° 808 et 809) : son Pokédex est
    tout ce que le jeu peut contenir, donc **National** (153) et **Complet** (186 : les 18 formes d'Alola et 15 Méga ; ni
    casquette, ni Pikachu / Évoli Partenaire). Les Pokémon n'y tiennent **pas d'objet** : une case Méga se remplit avec un
    **exemplaire de plus** de l'espèce **et sa gemme dans le sac** (`LivingDexSlot.MegaInBag` ; Dracaufeu : trois pour les
    deux Méga) ; infobulle « il faut un Dracaufeu de plus et la Dracaufite X dans le sac ». Le Parc GO (Pokémon venus
    de Pokémon GO, hors des boîtes) ne compte pas ;
  - 8e génération : seulement ce que le jeu peut contenir, d'après PKHeX (`PersonalTable.IsPresentInGame` :
    `LivingDexGames.Present`), dans tous les modes ; pas de Méga.
    - **Épée / Bouclier** : **Régional** = Galar (400), puis ce que l'**Isolarmure** (110 de plus) et la **Couronneige**
      (74 de plus) ajoutent, sans doublon, en trois sections avec les numéros du jeu (`RegionalNumbers`) : 584 ;
      **National** : les 664 espèces du jeu ; **Complet** (764) : formes d'Alola et de Galar (« Miaouss de Galar »),
      Motisma, Kyurem, Necrozma, Lougaroc, Zygarde, Pitrouille..., Salarsen Grave, Shifours Mille Poings, Sylveroy du Froid
      et d'Effroi, les **9 crèmes de Charmilly** (« Charmilly (Lait Ruby) » ; n'importe quelle décoration), et **32 cases
      Gigamax** (« Dracaufeu Gigamax ») : le facteur Gigamax n'est pas une forme, sa case se remplit avec un exemplaire
      **de plus** qui l'a (comme une Méga ; « il faut un Dracaufeu de plus avec le facteur Gigamax ») ; une seule pour
      Charmilly et Salarsen (même aspect Gigamax), deux pour Shifours. Exclus : Pikachu à casquette, Zarude Papa et
      Magearna Couleur du Passé (distributions terminées), Théffroi / Polthégeist Authentique (même aspect), les formes de
      combat (Darumacho Transe, Zacian / Zamazenta Suprême, Bekaglaçon, Morpeko, Éthernatos Infinimax...) ;
    - **Diamant Étincelant / Perle Scintillante** : **Régional** (Sinnoh, le même que Diamant / Perle : 151), **National**
      (493) et **Complet** (536) : les formes de Diamant / Perle, Shaymin Céleste en case ordinaire (il tient en boîte à
      partir de la 7e) ; Papilord, une case (sa cape ne se voit pas) ;
    - **Légendes Arceus** : **Régional** (Hisui : 242, Brindibou n° 1), **National** (les mêmes 242, dans l'ordre
      national, comme Johto) et **Complet** (291) : formes de Hisui, Goupix et Feunard d'Alola, Dialga, Palkia et Giratina
      Originels, formes Totémiques (Amovénus compris), Bargantua Blanc, Zarbi, capes, mers, Motisma, Shaymin Céleste. Une
      espèce qui n'existe qu'en forme de Hisui (Caninos, Typhlosion...) a une seule case, à cette forme (« Caninos de
      Hisui », même en National) ; pas les formes Roi (impossibles à capturer) ;
  - 9e génération, **Écarlate / Violet** : comme Épée / Bouclier, seulement ce que le jeu peut contenir.
    **Régional** = Paldea (400), puis ce que **Septentria** (99 de plus) et la **Myrtille** (165 de plus) ajoutent, sans
    doublon, en trois sections avec les numéros du jeu : 664 ; **National** : les 733 espèces du jeu ; **Complet**
    (855, pour 966 places) : formes d'Alola, de Galar, de Hisui et de **Paldea** (Axoloto, les 3 races de Tauros : « Tauros de
    Paldea (race Combative) »), Ursaking Lune Vermeille, **Famignol Famille de Quatre** et **Deusolourdo Triple** (rares,
    mais des formes d'aspect), les 4 plumages de Tapatoès, les 3 formes de Nigirigon, **Mordudor Marche** (venu de
    Pokémon GO par HOME), les 4 masques d'Ogerpon, et les formes des générations précédentes que le jeu connaît
    (Motisma, Bargantua, saisons, fleurs, noyaux de Météno, crèmes de Charmilly...). Ni Méga ni Gigamax. Une seule case :
    Koraidon et Miraidon (leurs autres formes sont celles de la monture, jamais en boîte), Prismillon, Arceus,
    Silvallié. Exclus : Pikachu à casquette, Zarude Papa, Magearna Couleur du Passé, Poltchageist / Théffroyable
    Onéreux (même aspect), formes de sexe, formes de combat (Superdofin Super, Ogerpon et Terapagos Téracristal...).
- **Page d'un living dex** (`/livingdex/{id}`) : jaquette officielle, compteur et barre de progression, filtre
  **Tous / Manquants / Possédés** avec le nombre de chacun (`?filtre=manquants`, `?filtre=possedes`, gardé en changeant
  de mode), grille dans l'ordre du Pokédex avec les **sprites du jeu** (Rouge / Bleu : couleurs du Super Game Boy, Jaune :
  Game Boy Color ; Or, Argent et Cristal ont chacun les leurs ; Rubis / Saphir, Émeraude, Rouge Feu / Vert Feuille,
  Diamant / Perle, Platine et HeartGold / SoulSilver aussi ; ceux, fixes, de Noire / Blanche pour toute la 5e génération :
  PokéAPI n'a pas ceux de Noire 2 / Blanche 2 ; **à partir de la 6e génération, les images de « Ma collection »**, les
  sprites 3D de ces jeux n'ayant pas plu : `SpriteFolder` null, `LivingDexGames.Sprite` → `PokemonSprites.Url`, la Méga
  par sa gemme comme un Pokémon qui la tient, les Gigamax par `PokemonSprites.GigamaxUrl` ; 7e et 8e génération aussi),
  **une image par forme** (Zarbi B, Deoxys Vitesse...), en couleur si le Pokémon est dans la sauvegarde, en **silhouette
  noire** sinon. Infobulle : numéro, nom (« Zarbi B » en Complet, « Deoxys (forme Vitesse) »)
  et place (« Boîte 2, case 8 ») ou « manquant » ; un clic sur un Pokémon possédé ouvre sa fiche. Nombre de colonnes au
  curseur, comme dans « Ma collection » (3 à 20, selon la largeur de la fenêtre par défaut, `?colonnes=12` ;
  `Shared/GridColumns.cs`, commun aux deux pages).
- **Suppression** : « Supprimer ce living dex » (la sauvegarde reste). Supprimer la sauvegarde supprime son living dex
  (suppression en cascade), ce que la confirmation de « Mes sauvegardes » signale.
- **Living dex global** (`Services/GlobalLivingDex.cs`, jeu `LivingDexGame.Global`) : un par compte, choisi en tête de
  « + Créer un living dex », **sans sauvegarde** (`LivingDex.StoredSaveId` null, migration `GlobalLivingDex`) : il compte
  les Pokémon du **stockage interne** (« Transfert »), pas ceux des sauvegardes stockées ni les œufs, et **pas les
  chromatiques** (ignorés même s'ils pourraient remplir une case) ; chaque exemplaire
  ne remplit qu'une case (d'abord les cases qui en demandent un de plus, puis les formes exactes, puis « n'importe quelle
  forme »), dans l'ordre des dépôts. Carte et page : la **Master Ball** (image de l'objet) à la place d'une jaquette
  (`Shared/LivingDexCover.razor`), toujours **en tête** de la vue générale, quel que soit le tri.
  Une forme y est reconnue par son **nom anglais chez PKHeX**, pas par son numéro : Pikachu 1 est Rockeur en 6e
  génération, à casquette Originale ensuite. Trois modes :
  - **National** : les 1 025 espèces ;
  - **Complet** (1 283) : les cases Complet de tous les living dex par jeu réunies (formes régionales, Méga, Gigamax,
    formes d'aspect, avec les mêmes choix : un seul Prismillon, 9 crèmes de Charmilly, pas de casquettes...), sans
    Légendes Z-A ; la forme exacte l'emporte quand un jeu accepte n'importe laquelle et un autre non (Hoopa) ; plus de
    condition de sac (Shaymin Céleste, Méga de Let's Go : la gemme tenue) ;
  - **Ultime** (1 578 : 12 lots de 50 emplacements au-delà des 1 000 de départ) : **toutes les formes** qu'une boîte
    d'un jeu pris en charge peut garder (tables de chaque jeu de PKHeX ; présence dans le jeu à partir de la 8e), même
    celles au même aspect (Pikachu Partenaire, costumes et casquettes, Dominants, Papilord, Lépidonille, Théffroi
    Authentique, Zygarde Rassemblement...), les formes de couleur (20 Prismillon, **63 Charmilly** : crème × décoration,
    lue dans le `FormArgument`), **mâle et femelle à part** quand la femelle est différente (forme de base, liste de
    PokéAPI ; « Pikachu ♀ »), les Méga, Primo, Gigamax (deux pour Salarsen) et les formes de combat dues à un objet tenu
    (Zacian / Zamazenta Suprêmes : Épée / Bouclier Rouillés, Ultra-Necrozma : Ultranécrozélite), chacune avec un
    **exemplaire de plus**. Exclues : les formes de combat sans objet (Darumacho Transe, Exagide, Mimiqui démasqué,
    Zygarde Parfait, Superdofin Super, Téracristal...), les coupes de Couafarel (en équipe seulement, et le stockage
    interne ne reçoit que des Pokémon des boîtes), les formes de monture de Koraidon et Miraidon, Floette Éternelle
    (jamais distribuée), les formes Roi de Légendes Arceus, Arceus « ??? ». **Spinda** n'a qu'une case.
  Vue générale : bouton **Ultime** (`?mode=ultime`), qui ne montre que les living dex qui ont ce mode (les globaux) ; en
  Régional, une carte globale compte en National (« National seul »).
- **Living dex global chromatique** (`LivingDexGame.GlobalShiny`) : le même, un par compte, image le **Charme Chroma**,
  juste après le global ; il ne compte **que les chromatiques** du stockage interne (images chromatiques de la
  collection). Ses cases sont celles du global **sans ce qui n'a jamais existé en chromatique** dans un jeu pris en
  charge (`GlobalLivingDex.ShinyLocked`, d'après la « List of unobtainable Shiny Pokémon » de Bulbapedia, distributions
  et cadeaux de HOME compris) : 25 espèces (Victini, Hoopa, Volcanion — le chromatique de HOME vient de Légendes Z-A et
  ne va ni dans Épée / Bouclier ni dans Écarlate / Violet —, Cosmog, Cosmovum, Magearna, Marshadow, Wushours, Shifours,
  Zarude, Blizzeval, Spectreval, Sylveroy, et Serpente-Eau à Pêchaminus), les costumes et casquettes de Pikachu (sauf la
  Partenaire), Pikachu et Évoli Partenaires, Pichu Troizépi, Amphinobi Lien Naturel, le motif Poké Ball, Ursaking Lune
  Vermeille, Melmetal Gigamax, les Pokémon Dominants. National 1 000, Complet 1 244, Ultime 1 508.

Données : table `LivingDexes` (`Data/LivingDex.cs`), jeux et modes dans `LivingDexGames` (`Services/LivingDexService.cs`).
Images : `outils/images.py livingdex` (sprites de PokéAPI, jaquettes de Bulbagarden Archives, en anglais faute de
françaises), téléchargées sur le VPS à chaque déploiement (ou `deploy\images-serveur.cmd`) ; formes : `LIVINGDEX_FORMS`
(noms de fichiers de PokéAPI ; Deoxys, Cheniselle, Motisma, Giratina, Shaymin, Bargantua, les Totémiques, Kyurem et Keldeo
d'après ses Pokémon 10001 à 10024). Sans
elles, la grille affiche l'image de la même
forme sur le site (`LivingDexGames.Fallback`).
