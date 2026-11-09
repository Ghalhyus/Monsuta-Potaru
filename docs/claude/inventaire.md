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

## CT
- Un même numéro n'apprend pas la même capacité selon le jeu : stockage par capacité (`SaveBag.MachineKey`).
- Correspondances dans `SaveBag` ; Épée/Bouclier et Écarlate/Violet ont un ordre particulier (commentés), vérifiés par
  des valeurs connues dans les tests (Séisme : CT26, DT10, CT149).
- Les CS ne sont jamais touchées.

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
- Glisser : `wwwroot/js/transfert-objets.js` (module, `init(root, dotnet)` → `dispose`), appelle `Drop(colonne, objet)`
  (`[JSInvokable]`, revérifié). Champ du compteur recréé à chaque saisie (`@key` avec `typed`) : sinon un nombre ramené
  dans les bornes à la même valeur qu'avant reste affiché tel que tapé (Blazor ne réécrit pas un attribut inchangé).
- Le glisser au doigt n'a pas pu être essayé sur un vrai téléphone (l'émulation du navigateur envoie des clics de souris).
