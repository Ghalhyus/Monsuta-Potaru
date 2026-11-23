# Comptes, connexion, rôles, thèmes

README : « Comptes », « Administration », « Thèmes ».

## Pseudo et adresse
- Pseudo = `UserName` d'Identity ; connexion par l'adresse (`AccountService.FindForLoginAsync`).
- Ancien compte : son adresse est encore son `UserName`. « @ » est interdit dans un pseudo : c'est le repère (`HasPseudo`).
- Ne jamais afficher l'adresse d'un compte à la place du pseudo.

## Connexion protégée
- `FindForLoginAsync` compte les échecs : 5 bloquent le compte 5 minutes. `AccountLimiter` (singleton) limite les envois
  par adresse IP (connexion, inscription).
- Tout échec de connexion a le même message (`LoginFailedMessage`) : ne jamais dire qu'une adresse a un compte ni qu'il est bloqué.
- Toute vérification du mot de passe passe par `AccountService.CheckPasswordAsync`, jamais `UserManager.CheckPasswordAsync`
  (il ne compte pas les échecs).
- En test : débloquer un compte en passant `LockoutEnd` dans le passé dans la base de test ; `localhost` et `127.0.0.1`
  ont chacun leur compteur d'IP.

## Pages du compte (rendu statique)
- Depuis .NET 10 (`BlazorDisableThrowNavigationException` dans le `.csproj`), `IdentityRedirectManager.RedirectTo` ne lance
  plus d'exception : le code qui suit s'exécute. Toujours `return` juste après une redirection.
- `UserAccessor.GetRequiredUserAsync` renvoie null pour un compte supprimé (page redirigée) : le vérifier dans
  `OnInitializedAsync` **et** dans la méthode d'envoi du formulaire (elle s'exécute aussi).
- Identity modifie l'objet du compte avant de valider (`SetUserNameAsync`, `SetEmailAsync`) : un refus laisse la valeur
  refusée dessus. D'où les vérifications d'unicité faites avant, dans `AccountService`.

## Rôles et réglages
- Rôles exclusifs Standard / Administrateur (`SiteRoles`). `AdminService` revérifie le rôle en base à chaque action (le
  cookie peut le garder 30 minutes).
- Code d'invitation en base (`SiteSettings`) : `Bank:InviteCode` ne sert qu'au premier démarrage.
- Mode debug (`DebugService`, README « Administration ») : choix de l'utilisateur, **session seulement** (en mémoire,
  `DebugMode` singleton ; `Account/Logout` le coupe). Toute nouvelle action debug passe par `DebugService.IsActiveAsync`
  (revérifie le rôle) et prend des valeurs fixes côté serveur (`MoneyAmounts`), jamais un montant libre venu du navigateur.
  En test : `--Bank:AdminPseudo=<pseudo>` sur une base neuve, puis « Activer » dans « Administration ».
- EF ne traduit pas `Guid.NewGuid()` dans un `ExecuteUpdate` : calculer la valeur avant.

## Thèmes
- Tout le style d'un thème sous `[data-theme="<clé>"]` (posé par `App.razor`) ; le Classique n'a rien.
- Défaut : `SiteThemes.Default` (Ardoise), jamais `SiteThemes.Classic` en dur. Le défaut est enregistré à null : changer
  `Default` change le thème de tous les comptes qui ne l'ont pas choisi (et de ceux qui avaient choisi l'ancien défaut).
- `app.css` fixe des couleurs en dur (`.btn-primary`, `.btn-link`, focus) : les écraser explicitement. Le CSS isolé des
  composants (menu) se charge après : sélecteurs en `html[data-theme=...]`.
- Le thème Pokédex couvre tout le site (contenu sous `main`) : un nouvel élément coloré en dur dans `app.css` (bleu,
  orange) doit y recevoir sa version.
- L'utilisateur veut du **sobre** : thème trop chargé refusé (fond rayé, pastilles et bordures épaisses partout) ; rouge
  du Pokédex `#c2323d` (`#dc0a2d` trop vif).
- **Ardoise** : inspiré de l'interface d'un logiciel de gestion, mais **aucune référence à ce logiciel** nulle part (code,
  commentaires, textes, README). Seul thème à cadre propre (`ArdoiseLayout`, choisi par `MainLayout` via la valeur en
  cascade `SiteTheme` de `Routes.razor`) : un lien ou une rubrique ajoutés au menu vont dans `NavMenu` **et**
  `ArdoiseLayout`, une nouvelle page dans `Breadcrumbs.For` (sinon fil d'Ariane vide), un compteur dans `SiteMenuBase`.
- **Rubriques du menu** (2026-11-09) : Mes Pokémon, Inventaire, Défis, Multijoueurs, Sauvegardes, dans cet ordre, entre
  Accueil et Administration. Une page ajoutée va dans sa rubrique des deux menus, dans la propriété « ouverte »
  (`pokemonOpen`, `inventoryOpen`, `challengesOpen`, `playersOpen`, `savesOpen` : `IsOn` compare le premier segment de
  l'adresse, donc `joueurs/x/inventaire` ouvre Multijoueurs, pas Inventaire), dans `Breadcrumbs.For` (« Rubrique › Page »,
  la rubrique sans lien) et dans `SiteMenusTests`. Une rubrique sans page à elle n'a pas de lien.
- Noms des pages = libellé du menu = titre (`PageTitle`, `PageHeader`) = dernière étape du fil d'Ariane : « Dépôt /
  Retrait » (`/transfert`), « Banque d'objets » (`/inventaire` ; « Inventaire » sur la page d'un autre joueur, onglet de
  `PlayerHeader`), « Échange » (`/echanges`), « Combat » (`/combats`), « Living dex » (typo française, comme « Ma
  collection »). Les adresses n'ont pas suivi les noms (choix de l'utilisateur) ; un texte qui cite une page reprend son nom.
- Deux pages sous une même adresse de base (`inventaire`, `inventaire/transfert`) : `NavLinkMatch.All` sur la première,
  sinon les deux s'allument (en .NET 10, `All` ignore la chaîne de requête).
- Porte-monnaie : `WalletLink.razor` dans la barre du haut, dans tous les thèmes (Ardoise : `ardoise-wallet` ; Classique et
  Pokédex : `top-wallet`, seul dans la `top-row` de `MainLayout`, à droite même sur téléphone). Le menu ne l'affiche
  plus. Pokédex : la règle des liens de `main` doit l'exclure (`:not(.top-wallet)`), sinon il passe en bleu sur le rouge.
- Icônes des rubriques : `bi-pokeball-nav-menu` (dessin du site, traits blancs), `bi-bag-nav-menu`, `bi-trophy-nav-menu`.
- Le cadre Ardoise sert aussi aux pages du compte (rendu statique) : aucun `@onclick` dedans, tout par `app.js`. La
  classe `ouvert` d'une rubrique est posée par Blazor (page de la rubrique) puis changée par `app.js` : Blazor ne la
  réécrit que si sa propre valeur change.
- Icônes des menus : dans `app.css` (globales), plus dans `NavMenu.razor.css` ; blanches, `filter: invert(...)` sur fond clair.
- Toute page commence par `<PageHeader>` (jamais un `<h1>` écrit à la main) et ses parties titrées sont des `<Block>` :
  c'est ce qui leur donne le bandeau et les panneaux d'Ardoise. Classique et Pokédex doivent rester comme avant : un
  style de `PageHeader` / `Block` pour Ardoise va sous `[data-theme="ardoise"]`, la base (`app.css`) reste sans cadre.
- Une page d'un autre joueur : `PlayerHeader` (bandeau, onglets), puis le `PageHeader` de la page avec `Small`, sans
  `Icon` (simple titre, pas de second bandeau).
- Razor colle `@Title` et la pastille qui suit (blancs supprimés) : espacer par une classe (`ms-1`), pas par un blanc.
- Le compte est à deux places dans Ardoise : pastille de la barre du haut (`details` `.ardoise-account`) et rubrique en
  bas du menu latéral (`.ardoise-side-account`, au-dessus de « Replier le menu » ; ouverte d'elle-même par `accountOpen`
  de `SiteMenuBase`). Une entrée ajoutée au compte va dans les deux, et dans `NavMenu`. Menu replié, ses pages s'ouvrent
  vers le haut (`bottom: 0`), pas sous le bord de la fenêtre. Tests : `ArdoiseLayoutTests`.
- Test du menu replié : cookie `menu=replie` ; le survol d'une rubrique ne se vérifie qu'avec `computer` `hover` + capture.

## Préférences : tris par défaut (2026-11-08)
- Page `Manage/Preferences.razor` (« Préférences », après Thèmes), formulaire statique : des `<select>` HTML simples
  (`tri-<clé>`, `ordre-<clé>`, clés `objets`… `ct`, `collection`, `livingdex`, `completion`, `rubans`) relus dans `HttpContext.Request.Form`, deux
  boutons d'envoi (`Action` = `save` / `reset`). Le sens s'y écrit toujours en clair (`asc` / `desc`).
- Tout passe par `SiteSorts` (`Services/SortPreferences.cs`) : noms dans l'adresse, lecture d'une adresse ou du formulaire
  (`ItemFromQuery`, `CollectionFromQuery`, `LivingDexFromQuery`, `CompletionFromQuery`, `RibbonFromQuery` ; null = l'adresse n'en dit rien), écriture (`AddressFor` :
  rien quand c'est le tri du compte, sinon critère et sens en entier). Un nouveau tri par défaut : ses noms et sa lecture
  là, une propriété de `SortPreferences` (JSON, pas de migration), une ligne du formulaire.
- Attention au sens de « ordre » : **réel** pour la collection (`asc` / `desc`, l'arrivée est décroissante d'elle-même,
  `CollectionSort.Reversed` est relatif au critère), « desc » seul pour l'inventaire, les living dex, les complétions et les rubans.
- Les pages lisent les tris **du compte connecté** (`PreferencesService.SortsAsync`, même en visite chez un autre joueur),
  dans `OnInitializedAsync` (`OnParametersSetAsync` pour `LivingDexes`, `CompletionList` et `RibbonList`, une seule fois).
- Choix de l'utilisateur : une colonne JSON ; l'adresse puis le compte (l'inventaire a abandonné `localStorage` pour son
  tri) ; un tri par onglet ; ceux du visiteur ; pas de lien depuis les pages vers ce réglage.
- Dans un test de `SiteFactory`, les accents de la page sont encodés (`&#xE9;`) : lire le texte par AngleSharp
  (`QuerySelector(".alert").TextContent`), pas dans le HTML brut.

## Préférences : écrans d'un combat (2026-11-14)
- Même page, même colonne JSON (`"combat":"cote"`, `"combatAgrandi":true` ; `SortPreferences.Battle`, `BattleLayout`,
  `SortPreferences.BattleEnlarged`), mais **son propre bloc et son propre formulaire** (`@formname="battle"`, liste
  `combat` : `empile` / `cote` / `hybride`, case `agrandi` de valeur `oui`) : le formulaire des tris recopie les valeurs
  enregistrées, « Revenir aux tris d'origine » ne les change pas.
- **Défaut : hybride** (`SiteSorts.DefaultBattleLayout`, jamais écrit ; une valeur inconnue donne l'hybride). Avant le
  2026-11-14, le défaut (non écrit) était « Empilés » : la migration `HybridBattleDefault` (SQL avec `json_set`) a écrit
  `"combat":"empile"` dans les comptes qui n'avaient pas de disposition, pour qu'ils ne changent pas (choix de
  l'utilisateur : seuls les nouveaux comptes passent en hybride). Un défaut qui change encore : même méthode.
- Écriture : `PreferencesService.SetBattleScreensAsync` (formulaire : les deux), `SetBattleLayoutAsync` et
  `SetBattleEnlargedAsync` (boutons de la page « Combat ») ; chacune relit les tris et ne change que ce qu'elle reçoit.
  Interface du combat : `docs/claude/combats.md`.

## Préférences : affichage des étapes d'une complétion (2026-11-17)
- Même colonne JSON (`"completionVue":"grille"` ; absent ou inconnu : liste), `SortPreferences.CompletionView`
  (`CompletionStepsView`), noms `SiteSorts.CompletionViewChoices` / `CompletionViewFrom`. Son propre bloc et son propre
  formulaire (`@formname="completion-view"`, liste `completionVue`) ; « Revenir aux tris d'origine » le garde.
- Écriture : `PreferencesService.SetCompletionViewAsync`, appelée aussi par le bouton « Liste / Grille » de la page d'une
  complétion (réglage du joueur connecté, même chez un autre joueur ; rien pour un compte absent). Pour l'instant seule la
  complétion All the Mons s'en sert (essai demandé par l'utilisateur avant de l'étendre).

## Préférences : Pokémon des échecs au repos (2026-11-22)
- Même colonne JSON (`"echecsRepos":false` ; absent ou autre : animés), `SortPreferences.ChessIdle` (vrai par défaut :
  seul le refus est écrit). Son propre bloc « Échecs » et son propre formulaire (`@formname="chess"`, case
  « Désactiver les animations » `figes` de valeur `oui` = figés ; décochée, rien n'est envoyé = animés ; d'abord une case
  `repos` « Animer les Pokémon au repos » de sens inverse) ; « Revenir aux tris d'origine » le garde.
- Écriture : `PreferencesService.SetChessIdleAsync`, appelée aussi par la case de la page d'une partie (rien pour un
  invité sans compte). Interface : `docs/claude/echecs.md`.
- **Contraste élevé** (2026-11-23) : `"echecsContraste":true` (absent ou autre : teintes et boucliers),
  `SortPreferences.ChessContrast` (faux par défaut : seul le choix est écrit). Case `contraste` (valeur `oui`) dans le
  même formulaire « Échecs » : `SetChessDisplayAsync` enregistre les deux cases ensemble (message « Réglages des échecs
  enregistrés. ») ; la case de la page d'une partie appelle `SetChessContrastAsync`. « Revenir aux tris d'origine » le
  garde.

## Tests
- `AccountServiceTests` monte Identity comme `Program.cs` (`AccountService.ConfigureIdentity`) sur la base de test.
- Les comptes de `TestDatabase` sont créés sans Identity (pas d'adresse) : n'y appeler aucune méthode qui les revalide.
- Tests de migration : comptes ajoutés en SQL (`MigrationsTests.AddUserAsync`), pas par `db.Users` : le modèle actuel a
  des colonnes (`Theme`) qu'une base migrée à une étape plus ancienne n'a pas.
