# Sécurité : analyses du code, des dépendances et du site

État au 2026-11-26 (SDK 10.0.403).

## Analyse statique (règles CA de sécurité)

- `<AnalysisModeSecurity>All</AnalysisModeSecurity>` dans `MonsutaPotaru.csproj` : toutes les règles de la catégorie
  Security des analyseurs .NET (CA2xxx de désérialisation, CA3xxx d'injection, CA5xxx de crypto…), dont beaucoup sont
  désactivées par défaut. Une faille repérée sort en avertissement à `dotnet build` (pas en erreur : la CI n'échoue pas).
  Seulement le projet du site, pas celui des tests.
- Premier passage : 3 avertissements, tous CA5394 (`Random`) dans `HideSeekDraw` ; écartés par un `[SuppressMessage]`
  justifié sur la classe (tirage d'un jeu, `Random` passé en paramètre pour fixer la graine dans les tests). Un nouveau
  `Random` dans un autre jeu redonnera CA5394 : même exception, avec justification, si rien de secret n'en dépend.
- Vérifier sans rien modifier : `dotnet build ... --no-incremental` (les analyseurs ne tournent pas sur une compilation
  incrémentale à jour) ; filtrer `warning CA[235]`.

## Dépendances (SCA)

- NuGet Audit actif sans réglage : `dotnet msbuild MonsutaPotaru/MonsutaPotaru.csproj -getProperty:NuGetAuditMode`
  renvoie `all` (dépendances transitives comprises, défaut du SDK 10), `NuGetAuditLevel` = `low`. Avertissements
  `NU1901`–`NU1904` à la restauration.
- Liste à la demande : `dotnet package list --project MonsutaPotaru.sln --vulnerable --include-transitive` (et
  `--deprecated`). Le 2026-11-26 : aucun paquet vulnérable ; `xunit` 2.9.3 marqué « Legacy » (remplaçant : `xunit.v3`).
- Showdown : `npm audit --omit=dev` dans `showdown/` (0 vulnérabilité le 2026-11-26).
- Images Docker (`sdk:10.0`, `aspnet:10.0`, `node:22-bookworm-slim`) : pas encore analysées (Trivy, par exemple).

## Scan dynamique (ZAP) et en-têtes

- Pas encore fait. Uniquement sur l'instance de test locale (`docs/claude/navigateur.md`), jamais sur la production sans
  l'accord de l'utilisateur ; ni Java ni Docker installés le 2026-11-26.
- L'utilisateur a refusé les scans publics de la production (securityheaders.com, Observatory, SSL Labs) : en-têtes à
  vérifier sur l'instance de test (`SecurityHeaders.cs`).

## Audit du code par Claude

- Plugin retenu : « Claude Security » (Anthropic, annuaire des plugins), installé par l'utilisateur.
