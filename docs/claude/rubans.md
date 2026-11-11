# Rubans (« Défis » → « Rubans », 2026-11-11)

README : « Rubans ». Rubans comptés : `Services/Ribbons.cs` ; suivi : `Services/RibbonService.cs`, table `RibbonTrackers`.

## Choix de l'utilisateur
- Liste de référence : Pokébip, « Tous les rubans ». Comptent les rubans que **n'importe quel Pokémon** peut obtenir en
  jouant ; pas ceux des distributions (Classique, Premier, Évènement, Anniversaire, Souvenir, Souhait, Spécial, Champion
  du Monde, Vainqueur Championnat, Partenaire), ni des anciens tournois (Marine, Ciel, Record, Pays, Monde, Champion
  Régional / National / Combat). **Colosseum / XD comptent** (National, Terre). Les Marques ne comptent pas (Pokébip ne
  les liste pas). **Exceptionnel ne compte pas** (retiré à sa demande le 2026-11-11 : dû au hasard d'un Échange Miracle).
- **Souvenirs seuls** : les rubans de concours (20 de Hoenn, 20 de Sinnoh) et de la Tour de Combat (Gagnant, Victoire, six
  Aptitude) de 3e / 4e génération ne sont pas listés un par un : Souvenir Concours (sur 40) et Souvenir Combat (sur 8),
  un ruban chacun, obtenu dès que le compteur dépasse 0, image dorée (`…2.png`) au complet. 42 cases en tout.
- **Complétion sur 88** (demandée le 2026-11-11) : chaque ruban réuni dans un souvenir compte (40 rubans simples + 40 de
  concours + 8 de combat), d'après le compteur, connu même quand le détail ne l'est pas (`RibbonState.Earned`, `Worth`,
  `Ribbons.Total`). Les filtres comptent des cases : un souvenir entamé est dans « Manquants » (`Missing`) et
  « Possédés » (`Owned`).
- **Fenêtre du détail** : **au plus 4 cases par ligne** (demandé le 2026-11-11) ; largeur `min(100vw - 4rem, 88rem)`
  (`.collection-dialog.ribbon-dialog` : deux classes, sinon les 28rem de `.collection-dialog`, plus bas dans `app.css`,
  l'emportent). Cases d'au moins 17rem et d'au moins le quart de la ligne (`minmax(max(17rem, (100% - 3 gaps) / 4), 1fr)`) :
  4 colonnes sur l'écran de l'utilisateur, 3 à 1 000 px CSS, une sur téléphone.
- **Même liste pour tous**, sans mention « plus obtenable » : un ruban impossible pour ce Pokémon (né trop tard) reste
  simplement manquant. L'utilisateur envisage de renvoyer des Pokémon dans les anciens jeux (hackroms) : ne pas ajouter
  de calcul « encore possible » sans lui demander.
- **Pokémon parti** : le suivi reste (« Plus dans ta collection », derniers rubans connus) et se remet à jour s'il revient.
- **Détail d'un souvenir** : un clic sur Souvenir Concours ou Souvenir Combat ouvre une fenêtre avec chacun de ses rubans
  (40 : Hoenn Normal / Super / Hyper / Master, Sinnoh Normal / Méga / Ultra / Master, par catégorie ; 8 : Gagnant,
  Victoire, six Aptitude), obtenu ou manquant.
- Images : celles de PKHeX (`PKHeX.Drawing.Misc/Resources/img/ribbons`, 40 × 40), `outils/images.py rubans`.

## Lecture des rubans (`Ribbons`)
- Par les interfaces de PKHeX (`IRibbonSetCommon3` à `9`, `IRibbonSetEvent3`, `IRibbonSetMemory6`…), pas par
  `RibbonIndex` : PK3, CK3 / XK3, PK4 / PK5 n'implémentent pas `IRibbonIndex`. Un format qui ne connaît pas le ruban répond
  non (PK1, PK2 : aucun ruban).
- Souvenirs avant la 6e génération (`MemoryCounts`) : 3e génération et Colosseum / XD, un rang de 0 à 4 par catégorie
  (`RibbonCountG3Cool`…) ; 4e et 5e, chaque ruban de Hoenn et de Sinnoh à part (`IRibbonSetUnique4`) ; Gagnant / Victoire
  (`IRibbonSetUnique3`) et les six Aptitude ajoutés au combat. À partir de la 6e : `RibbonCountMemoryContest` / `Battle`.
- Un ruban ajouté à `Ribbons.All` : sa clé dans `Has` (sinon exception), son nom dans la liste `RIBBONS` de
  `outils/images.py` (test `RibbonsTests.ImagesScript_DownloadsEveryRibbon`), les totaux « 42 » (cases) et « 88 » (complétion) des tests et du guide.
- Détail des souvenirs (`ContestParts`, `BattleParts`, images dans `RIBBON_PARTS`) : en 3e génération et Colosseum / XD,
  le rang atteint par catégorie (`RibbonCountG3Cool` = 2 : Normal et Super) ; en 4e et 5e, un booléen par ruban, lu par
  son nom chez PKHeX (réflexion sur `Ribbon` + clé) ; à partir de la 6e (`IRibbonSetMemory6`), seulement le compteur :
  `Owned` null (« Inconnu ») sauf à 0 (tous manquants) ou au complet (tous obtenus), et la fenêtre le dit.
- Façons de les obtenir : réécrites d'après Pokébip (ne pas recopier son texte).

## Suivi (`RibbonService`)
- Retrouvé par `BankPokemon.Fingerprint` (index unique compte + empreinte) : les lignes d'une sauvegarde stockée sont
  effacées et recréées à chaque mise à jour (`StoredSaveService`, `ExecuteDeleteAsync`). Plusieurs exemplaires de même
  empreinte : le plus récent (`DepositedAt`). Pas d'œufs, pas d'empreinte vide.
- Chaque `ListAsync` / `GetAsync` recopie le Pokémon dans le suivi quand ses octets ont changé (`RefreshAsync`) : le
  dernier état connu sert quand il est parti. Pas de lien vers `BankPokemon` (il disparaît à chaque mise à jour).
- 1re / 2e génération : empreinte IV + dresseur + famille ; elle change au transfert par la Banque Pokémon (nouveau PID) :
  le suivi le perd (accepté).
- Pas de page publique chez les autres joueurs pour l'instant (pas de réglage de visibilité).
- Ajout : `ListCandidatesAsync` rend les 60 premiers par nom (`CandidatePage`), la recherche affine (LIKE, comme « Ma
  collection »). La recherche de la vue générale est en mémoire, sans accents (`BankPokemon.SortKey`).
- Surnom : comparé à l'espèce sans majuscules ni accents (`IsNickname`) : « PIKACHU » de 3e génération n'est pas un surnom.
- Nom de la page de liste : `RibbonList` (une classe `Ribbons` dans `Components.Pages` cacherait `Services.Ribbons`).

## Tests
- `RibbonsTests` (lecture par format, souvenirs, liste de `images.py`), `RibbonServiceTests` (sauvegarde mise à jour,
  Pokémon parti puis revenu, refus), `RibbonPagesTests` (bUnit). Pokémon avec rubans pour le navigateur :
  `rubis-omega.sav` (Pyrane, 4 rubans) ; les autres vraies sauvegardes n'en ont aucun.
