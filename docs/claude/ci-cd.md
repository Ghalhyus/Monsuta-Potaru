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
- Un nouveau fichier nécessaire au déploiement s'ajoute à l'archive des deux côtés (`publier.ps1` et le workflow ; rappelé
  dans `CLAUDE.md`).
