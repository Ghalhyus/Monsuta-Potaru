# Inventaire : sacs, objets, CT

README : « Inventaire ». Code : `SaveBag`, `InventoryService`.

## Sac (PKHeX 26)
- `var bag = sav.Inventory` (un `PlayerBag`, copie du sac), modifier `bag.Pouches` (ou `bag.GetPouch(type)`,
  `pouch.GiveItem(bag, ...)`), puis **`bag.CopyTo(sav)`** ; `sav.Inventory` n'est plus affectable.
- PC d'objets (`InventoryType.PCItems`, 1re à 3e génération) : lu et modifié seulement comme `BagPlace.Pc`.
- Après un retrait, `ClearCount0()` seulement sur les poches vidées.
- Les poches changent d'un jeu à l'autre (Balls avec les objets en Noir 2, baies de Let's Go avec les Balls...) : chercher
  un objet dans toutes les poches avec `CanContain`, jamais par type de poche.
- Or/Argent/Cristal : PKHeX **n'écrit pas une quantité de CT tombée à zéro** (poche CT/CS, un octet par numéro) ;
  `SaveBag.ClearEmptiedMachines` remet ces octets à zéro (position de la poche lue par réflexion, champ interne `Offset`).

## Numéros d'objets
- 3e génération : `ItemConverter.GetItemOld3` / `GetItemFuture3` ; 2e : `GetItemFuture2` (128 : sans équivalent) /
  `GetItemOld2` ; Rouge/Bleu : table `SaveBag.Gen1Items` (PKHeX n'a rien).
- Depuis PKHeX 26, la 2e génération sort directement dans la numérotation des jeux suivants (Noigrumes Bleue/Jaune : 486/487).
- **Objet tenu** : `PKM.HeldItem` est le numéro du jeu du Pokémon (139 : Baie Oran en 3e génération, Lettre Invit
  ensuite ; 173 : la « Baie » d'Argent). Pour le nommer ou l'afficher, passer par `SaveBag.HeldItem(pk)` (numéro de 4e
  génération et suivantes, 0 sans équivalent) : `PokemonInfo.Describe` (fiche d'un Pokémon, choix de l'équipe d'un
  combat) et `BattleTeam.ToSet`. Bug corrigé le 2026-11-14 : la fiche affichait « Lettre Invit » pour une Baie Oran.
  Restent au numéro du jeu, exprès : `BankPokemon.HeldItem` et `RibbonTracker.HeldItem` (ils ne servent qu'à l'image
  d'une Méga-Évolution ou d'une Primo-Résurgence, objets de la 6e génération et suivantes, numéros au-delà de ceux des
  vieux jeux), l'évolution par échange (`TradeService.Everstone`, `evo.Argument` : numéros du jeu).

## CT
- Un même numéro n'apprend pas la même capacité selon le jeu : stockage par capacité (`SaveBag.MachineKey`).
- Correspondances dans `SaveBag` ; Épée/Bouclier et Écarlate/Violet ont un ordre particulier (commentés), vérifiés par
  des valeurs connues dans les tests (Séisme : CT26, DT10, CT149).
- Les CS ne sont jamais touchées.

## Tri d'un onglet
- Dans l'adresse (`?tri=nom&ordre=desc`, pour l'onglet affiché), sinon le tri par défaut du compte pour cet onglet
  (« Préférences », voir `comptes.md`) ; plus de `localStorage` (l'ancienne clé `monsuta-potaru.inventaire.tris` n'est
  plus lue). Un tri changé reste pour la visite (`sorts`) ; changer d'onglet réécrit `tri` / `ordre` pour le nouvel onglet.
- `ItemSortBy` / `ItemSort` sont dans `Services/SortPreferences.cs` ; les noms des onglets dans l'adresse :
  `SiteSorts.InventoryTabs` (aussi les clés des préférences).

## Ajouter une catégorie d'objets
Même modèle que les Pokéballs, médicaments, baies et CT :
- `ItemCategory` et la liste d'objets dans `SaveBag` (`CategoryOf`, `KeyOf`, `Capacity`) ;
- `ItemCategories.All` (carte et onglet), `BagSummary` du dialogue de transfert ;
- des tests dans `InventoryServiceTests`, des objets dans la sauvegarde d'exemple Platine, le README.
- « Objets » est la catégorie fourre-tout (tout ce qui n'est dans aucune autre, limité aux poches `SaveBag.ItemPouches`) :
  une nouvelle catégorie (les bonbons, envisagée) se teste avant elle dans `CategoryOf`.
- Objets rares, Lettres, Cristaux Z et CS : jamais touchés.
- Pas de migration : la table `OwnedItems` stocke une clé par objet.

## Transfert entre deux sauvegardes (`Pages/InventoryTransfer.razor`)
- Service : `InventoryService.TransferBetweenSavesAsync(owner, gauche, droite, SaveItemMove[])`, un déplacement par
  (sauvegarde de départ, endroit de départ, endroit d'arrivée, objet). Dans chaque sauvegarde : `SaveBag.Withdraw` de tous
  les endroits, puis `SaveBag.Deposit` (un objet envoyé libère sa place), fichier relu (`Written`), puis
  `SaveServerChangeAsync` avec **les deux** changements (liste) : une transaction, deux traces `SaveModification`.
- Page : les objets en attente sont gardés par (colonne, endroit de départ, endroit d'arrivée, objet). Renvoyer un objet
  vers l'endroit d'où il vient annule d'abord l'envoi inverse. Le maximum d'une ligne (`Sendable`) reprend
  `ItemSource.CapacityIn` et `LockedMachines` de l'autre sauvegarde, sur l'état après les envois en attente ; ce que la page
  ne voit pas (20 objets différents dans le sac de Rouge / Bleu, seuil de 80 CT avec l'autre endroit) est refusé par le
  service à la validation, avec son message.
- **Cacher les grisés** (demandé le 2026-11-15) : interrupteur au-dessus des colonnes, commun aux deux, dans l'adresse
  (`?invalides=caches`, absent : montrés). Cache **toutes** les lignes grisées (`Row.Problem` : inconnu de l'autre jeu,
  CT absente, refus du PC, sac plein, seuil de 80 CT : choix de l'utilisateur), **sauf celles qui bougent**
  (`Hideable` : badge + ou −, sinon la ligne envoyée disparaîtrait quand le sac d'en face se remplit). Compteur des
  grisés / cachés à côté (endroits affichés, tous onglets) ; pastilles des onglets sur les seules lignes visibles ;
  onglet vide par ce filtre : « Tous les objets de cet onglet sont cachés… » ; à l'ouverture, premier onglet qui a des
  objets visibles.
- Tri : un par colonne et par onglet, pour la visite seulement (pas dans l'adresse : deux colonnes), au départ celui du
  compte (`PreferencesService`, `SortPreferences.InventoryFor`), comme la page « Banque d'objets ».
- Glisser : `wwwroot/js/transfert-objets.js` (module, `init(root, dotnet)` → `dispose`), appelle `Drop(colonne, objet)`
  (`[JSInvokable]`, revérifié). Champ du compteur recréé à chaque saisie (`@key` avec `typed`) : sinon un nombre ramené
  dans les bornes à la même valeur qu'avant reste affiché tel que tapé (Blazor ne réécrit pas un attribut inchangé).
- Le glisser au doigt n'a pas pu être essayé sur un vrai téléphone (l'émulation du navigateur envoie des clics de souris).

## Tester
- Test de mutation du 2026-11-26 (Stryker, voir `ci-cd.md`) sur `InventoryService.cs` et `SaveBag.cs`, avec les seuls tests
  de l'inventaire (`test-case-filter` `FullyQualifiedName~MonsutaPotaru.Tests.Inventory`, 17 min) : 68,9 % (service
  63,5 %, `SaveBag` 76 %). Les mutants non couverts de `BuyCollectionSlotsAsync` viennent du filtre (testé dans
  `CollectionCapacityTests`).
- Trous trouvés ce jour-là, bouchés par un test chacun dans `InventoryServiceTests` : crédit d'un objet déjà dans
  l'inventaire (`+` → `-` survivait), requêtes des sources et du transfert sans le filtre du propriétaire (`&&` → `||` :
  un autre joueur n'avait jamais de sauvegarde dans ces tests, ou le transfert en voyait trois), retrait d'un objet réparti
  sur plusieurs emplacements, octets des CT de la 2e génération au-delà de la CT01 (indice 0 : `offset + i` =
  `offset - i`), dépôt dans le PC jugé d'après le nombre du sac, débit concurrent et conflit de version jamais provoqués.
  Puis les refus vérifiés avec leur message exact (`Assert.Equal(new ItemTransferResult(false, "…"), …)` : avec `Ok ==
  false` seul, le refus pouvait venir d'une autre vérification) et les tests de pages qui passaient toujours.
- Seconde passe, le même jour : 75,0 % (service 71,7 %, `SaveBag` 79,5 %) ; 80,1 % sans `BuyCollectionSlotsAsync`, contre
  74,6 % avant. 43 mutants en `Timeout` cette fois (1 la première), sur des lignes sans effet (journalisation, textes) :
  la machine, pas les tests ; comparés mutant par mutant avec la passe précédente, ceux qui survivaient y sont comptés
  survivants. Rapports JSON : `StrykerOutput/<date>/reports/mutation-report.json` (`files[].mutants[].status`).
- Le test de Rouge Feu ne vérifiait le PC que s'il avait des objets : il est vide dans `rouge-feu.sav` (début de partie),
  le test y met donc 2 Potion. Son ancien `if` attrapait par hasard un autre mutant (objets rares de Cristal pris pour ceux
  d'Émeraude : Super Bonbon, Plongée Ball et Baie Ceriz cachés) : `SaveBag_ThirdGeneration_HidesOnlyItsOwnKeyItems`.
- Survivants restants, laissés exprès : textes (messages de réussite, onglets vides, libellés), filtres `> 0` / `>= 0`
  sur des listes déjà sans zéro, vérifications faites deux fois (PC refusé par le service puis par `SaveBag`), bornes des
  conversions de numéros, tables des CT de Noir / Blanc, Rubis Oméga, Let's Go et Légendes Z-A (libellé « CT », réutilisable).
- Autre onglet au milieu d'une opération : `_db.Inventory(intercepteurs)` (`TestDatabase`), avec un `DbCommandInterceptor`
  qui vide l'inventaire juste avant le débit, ou un `SaveChangesInterceptor` qui change la `Version` juste avant
  l'écriture ; les deux agissent dans la transaction du service, annulée avec elle.
- Mutants sans doute équivalents : `reusable` des CT de la 5e à la 7e génération (Noir 2 et Soleil / Lune sont vérifiés à
  1 par sac et le mutant survit : la poche de PKHeX doit déjà s'arrêter à 1, non vérifié), `Version--` au lieu de `++`
  (le jeton change quand même), journalisation. Celui de Légendes Z-A n'est pas équivalent : aucun test ne lit ses CT.
