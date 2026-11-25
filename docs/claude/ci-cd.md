# CI/CD

`.github/workflows/ci.yml` et `cd.yml` (README, « CI/CD »), séparés :
- CI : tests sur Ubuntu à chaque push, avec l'analyse SonarCloud (organisation `ghalhyus`, clé `ghalhyus_monsuta-potaru`,
  secret `SONAR_TOKEN` ; pour information, jamais bloquante : `continue-on-error`). Réglages passés à
  `dotnet sonarscanner begin` (pas de `sonar-project.properties`) ; règles écartées pour `outils/**` par
  `sonar.issue.ignore.multicriteria` : `python:S2245` (aléatoire à graine fixe voulu) et `pythonsecurity:*` (chemins de
  la ligne de commande : scripts lancés à la main). Le reste (complexité...) se corrige dans le code.
- Problèmes à corriger : l'utilisateur colle le JSON de l'API SonarCloud (`issues` : règle, fichier, ligne). Une
  refonte de `outils/generer-parc.py`, `generer-plage.py`, `generer-volcan.py` ou `parc_carte.py` se vérifie en comparant ses sorties avant/après (`cmp` : cartes et aperçus
  identiques octet pour octet, graine fixe).
- **Couverture** : objectif 70 % (environ 88 % fin 2026-11, 51 % avant les tests de pages), pour information (rien ne
  bloque). Chiffre de SonarCloud = (lignes couvertes + conditions couvertes) / (lignes + conditions), hors `Data/Migrations`,
  `wwwroot`, `outils`, `deploy` ; `showdown/combat.js` y compte (51 lignes à 0 %, aucun outil ne mesure le JavaScript).
  Mesure par fichier : API `api/measures/component_tree?component=ghalhyus_monsuta-potaru&metricKeys=coverage,uncovered_lines,uncovered_conditions&qualifiers=FIL`
  (projet public, sans jeton). En local : `dotnet test ... --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover`,
  puis compter `SequencePoint` (lignes) et `BranchPoint` (conditions) du `coverage.opencover.xml` avec la même formule.
  Pages Blazor : tests bUnit (`PageTestContext`) ; `Program.cs`, `SyncEndpoints`, pages du compte : `SiteFactory`.
- CD : bouton « Run workflow » de « CD » ; ne relance pas les tests, lit le résultat de la CI du commit (case « Forcer »
  pour passer outre) ; vérifie que `blazor.web.js` répond.
- Image construite **sur le VPS** à chaque déploiement : `cd.yml` et `publier.ps1` vident ensuite les images inutiles
  et le cache de construction de plus de 3 jours (`docker builder prune -f --filter until=72h`). Sans ce nettoyage, le
  cache montait à 20 Go (`docker system df`, ligne « Build Cache »).
- Le CLI `gh` n'est pas installé : l'utilisateur copie le message d'un échec.
- Les tests y tournent sous **Linux** (rappelé dans `CLAUDE.md`) : noms de fichiers sensibles à la casse, séparateur `/`,
  culture `fr-FR` par ICU.
- **Tests instables** (verts sur le PC, rouges de temps en temps en CI) : la CI est plus lente (machine partagée,
  couverture du code qui instrumente tout) et l'ordre des classes y change (xUnit les lance en parallèle). Règles :
  - Attentes bUnit : `DefaultWaitTimeout` vaut 15 s dans `PageTestContext` (1 s chez bUnit). Ne pas redescendre ;
    une attente plus longue se donne en argument (`BattlesPageTests.Long`).
  - Pages qui se redessinent seules (minuterie de Combats, Échecs, Cache-cache ; fil du simulateur) : tout clic ou
    changement de valeur passe par `Click`, `ClickOn`, `ChangeOn`, `InputOn` (`PageTestExtensions`), qui trouvent
    l'élément et agissent d'un seul tenant. Jamais `Find(...).ClickAsync(...)` : la page peut se redessiner entre les
    deux (« There is no event handler with ID … »).
  - État statique (`PokemonSprites`, `ParkHabitats`) : chargé une fois pour tous au démarrage (`TestAssemblySetup`), pour
    qu'un test ne dépende pas de l'ordre des classes. Un nouvel état statique chargé au démarrage du site s'y ajoute.
  - Base de test : une seule connexion SQLite (`TestDatabase`), que deux fils ne doivent pas utiliser en même temps.
    Un service qui écrit en arrière-plan se guette par un signal (`BattleHubTests.SignalingFactory`), jamais en lisant
    la base pendant qu'il écrit.
  - Hasard : graine fixe (`new Random(1)`, `BattleHub(..., seed:)`), ou un scénario qui marche quel que soit le tirage
    (voir `combats.md`, « Tests »). Heure : l'horloge manuelle des tests des hubs (`ManualTime : TimeProvider`) plutôt
    que l'horloge réelle.
  - Pour reproduire en local : lancer la suite sur un ou deux cœurs, avec la couverture, plusieurs fois de suite
    (`start "" /affinity 1 /wait /b dotnet test ... --collect:"XPlat Code Coverage"` dans `cmd`).
- **Qualité des tests**, outils locaux de `dotnet-tools.json` (racine ; `dotnet tool restore` sur un autre poste), lancés à
  la main, hors CI (rapports ignorés par Git) :
  - **ReportGenerator** : score CRAP de chaque méthode (complexité² × (1 − couverture)³ + complexité ; à garder sous
    30, viser 10). Il ne le calcule que d'après le format **Cobertura** (celui de coverlet par défaut : la couverture
    ci-dessus sans `Format=opencover` ; l'OpenCover de coverlet n'a pas le score) :
    `dotnet test ... --collect:"XPlat Code Coverage" --results-directory <dossier>`, puis
    `dotnet reportgenerator "-reports:<dossier>/**/coverage.cobertura.xml" -targetdir:coveragereport -reporttypes:Html "-classfilters:-System.Text.RegularExpressions.Generated*"`
    (le filtre écarte le code généré des `[GeneratedRegex]`), puis `coveragereport/index.html`, bloc « Risk Hotspots »
    (les 20 pires). Une méthode toute couverte a un score égal à sa complexité : un grand `switch` de données
    (`Ribbons.Has`, `LivingDexGames.Name`) y figure sans être un risque ; regarder celles dont le score dépasse nettement la
    complexité (peu couvertes : `BattleView.EndText` 480 pour 90 fin 2026-11).
  - **Stryker.NET** (test de mutation : il modifie le code, un mutant « survivant » est un changement qu'aucun test ne
    voit) : une passe sur tout le site prendrait des heures, viser un fichier ou un dossier. Le lancer dans une **copie**
    du dépôt (il compile dans `bin`, verrouillé par l'appli lancée), depuis `MonsutaPotaru.Tests` :
    `dotnet stryker --project MonsutaPotaru.csproj --mutate "**/Services/CollectionCapacity.cs" --reporter html --reporter progress`,
    rapport dans `StrykerOutput/<date>/reports/mutation-report.html`. Compter 7 à 8 min pour un petit fichier : il
    compile tout le site avec ses mutants (environ 16 400) et lance une fois toute la suite pour savoir quel test couvre
    quoi, avant de n'essayer que les mutants du fichier visé. Les avertissements « Safe Mode! … compile error » sont
    normaux (mutants qui ne compilent pas, écartés). Essai du 2026-11-25 sur `CollectionCapacity.cs` : 37 mutants,
    score de 67,5 % (10 survivants, 3 non couverts : `IsFull`, `Exceeds` quand le total ne change pas, message de
    `SaveFullMessage` pour une nouvelle version d'une sauvegarde déjà stockée).
  - Les doublons sont mesurés par SonarCloud (onglet « Duplications »).
  - Les outils de ub-mode (crapper, dryer, mutator, de Robert C. Martin) ne lisent pas le C# : ceux-ci les remplacent.
- Un nouveau fichier nécessaire au déploiement s'ajoute à l'archive des deux côtés (`publier.ps1` et le workflow ; rappelé
  dans `CLAUDE.md`).
