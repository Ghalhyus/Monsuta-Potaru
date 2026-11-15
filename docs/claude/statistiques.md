# Statistiques

README : « Statistiques ». Page `Components/Pages/Statistics.razor` (deux adresses : `/statistiques` et
`/joueurs/{pseudo}/statistiques`), service `Services/StatisticsService.cs`, blocs `Shared/LivingDexStatsBlock.razor`,
`Shared/RibbonStatsBlock.razor`, `Shared/CompletionStatsBlock.razor`.

## Choix de l'utilisateur (2026-11-09)
- Onglets à gauche comme « Mon compte » (`nav-pills flex-lg-column`, `col-lg-3 col-xxl-2`), en ligne au-dessus sous
  992 px CSS. Pourcentages en barres (`LivingDexProgress`, `Owned` = dixièmes de pour cent, `Total` = 1 000, `ValueText`
  pour le lecteur d'écran), nombres en tuiles (`.stat-tiles`). Pas « tous les living dex » dans le titre du général.
- **Argent déposé = solde + dépensé** (sa règle contre « je retire, je redépose »), pas la somme des retraits. Dépensé :
  `Amount < 0` dans `WalletOperations` (tout achat futur y entre tout seul). Les crédits du mode debug entrent dans le
  solde, donc dans le déposé.
- Living dex : **moyenne** des pourcentages (pas le total des cases), **chromatiques à part**, le général dans le
  **plus grand mode** (`Modes[^1]`). Un mode caché parce qu'identique à un autre (Complet de la 1re génération) ne compte
  pas : même règle que les modes affichés.
- Visible sur le profil : `PublicPages.Statistics` (16). Migration `StatisticsVisibility` : `PublicPages | 16` pour
  **tous** les comptes existants (pas seulement ceux qui montraient tout). Les trois onglets pour un visiteur (le solde se
  voit déjà dans son Inventaire).
- **Rubans et complétion dans « Défis »** (demandés le 2026-11-15, « comme pour les living dex ») :
  - rubans sur les **Pokémon suivis** (pas toute la collection) : rubans gagnés, **chaque ruban d'un souvenir compte**
    (Souvenir Concours à 12 = 12, comme la complétion sur 88), rubans par Pokémon, complétion moyenne ; tuile « Pokémon
    complets » ajoutée (non demandée, proposée) ;
  - complétion : moyenne générale, puis les six sections, chacune **sur les complétions qui l'ont** (Rouge / Bleu / Jaune
    n'entrent pas dans « Quêtes annexes ») ; tuile « Complétions à 100 % » ajoutée (proposée) ;
  - visiteur : la case « Statistiques » suffit (les moyennes se voient même si ses pages Rubans ou Complétion sont
    cachées), sans lien.
- Pas de cache : chaque onglet calcule à sa première ouverture, gardé pour la visite (`??=` dans `OnParametersSetAsync`,
  remis à zéro si le joueur affiché change).

## Pièges
- Une seule moyenne exacte (`StatisticsService.Mean`, fractions `BigInteger`) pour les living dex, les rubans et les
  complétions ; le pourcentage d'une complétion vient de `CompletionProgress.AverageFraction` (celle du tri de la vue
  générale), pas de `Tenths` (déjà arrondi). Résultats réutilisés : `LivingDexAverage` (nombre, complets, dixièmes).
- Une sauvegarde illisible donne une complétion sans section : 0 % dans le général, **jamais complète**
  (`CompletionProgress.Complete` est vrai sur une liste vide).
- `RibbonService.ListAsync` remet à jour les suivis du joueur affiché (`RefreshAsync`), même pour un visiteur : voulu,
  ce sont les mêmes chiffres que sa page.
- Blazor retire les espaces en fin de texte avant un `@if` : « pour l'instant.Crée-en » ; l'espace s'écrit `@(" ")` au
  début du `<text>` (test `Challenges_RibbonsAndCompletions`).
- La moyenne se calcule en `BigInteger` (fractions exactes) : en `double` ou `decimal`, 1/3 + 2/3 donnait 49,9 %.
- `LivingDexGames.Percent` met une espace insécable avant « % » : dans les tests, comparer à `LivingDexGames.Percent(…)`,
  pas à une chaîne tapée.
- `LivingDexService.ProgressAsync` lit chaque sauvegarde une fois pour tous ses modes (`EntriesAsync` à plusieurs modes ;
  le sac seulement si un mode en a besoin) ; le global, ses exemplaires une fois (`GlobalCopiesAsync`). Tout changement
  du comptage d'un living dex passe par `Entries` / `GlobalEntries` : la vue générale, la page et les statistiques
  restent d'accord (test `LivingDex_SameCountsAsTheOverview_ShinyApart`).
- Le champ injecté ne peut pas s'appeler `Statistics` (nom du composant) : `Stats`.
- `MigrationsTests` : un compte ajouté en SQL après la migration `Friends` échoue (`Audience` sans valeur par défaut) ;
  l'ajouter à `ProfileVisibility`, puis migrer.
