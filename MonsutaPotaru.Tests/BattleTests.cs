using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using MonsutaPotaru.Services;
using PKHeX.Core;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Combats : noms français, équipes tirées des sauvegardes, protocole de Showdown traduit pour chaque joueur, et vrais
/// combats dans le simulateur (<c>showdown/combat.js</c>, installé par <c>npm ci --prefix showdown</c>).
/// </summary>
public sealed class BattleTests
{
    private static readonly GameStrings French = GameInfo.GetStrings("fr");

    // ---------- Noms ----------

    [Fact]
    public void Names_FromShowdown_InFrench()
    {
        Assert.Equal(French.movelist[237], BattleNames.Move("Hidden Power Dark 70")); // nom affiché par Showdown
        Assert.Equal(French.movelist[85], BattleNames.Move("Thunderbolt"));
        Assert.Equal(French.itemlist[221], BattleNames.Item("King's Rock")); // « King’s Rock » chez PKHeX
        Assert.Equal(French.itemlist[259], BattleNames.Item("Stick")); // « Leek » chez PKHeX
        Assert.Equal(French.itemlist[4], BattleNames.Item("Poke Ball"));
        Assert.Equal(French.abilitylist[22], BattleNames.Ability("Intimidate"));
        Assert.Equal(French.types[12], BattleNames.MoveType("Thunderbolt")); // Électrik
        Assert.Equal(French.types[0], BattleNames.MoveType("Tackle")); // Normal : numéro 0
        Assert.Equal(French.types[16], BattleNames.MoveType("Hidden Power Dark 70")); // Ténèbres
        // Puissance écrite au bout du nom (Retour, Frustration), identifiants de la requête (« moves » d'un Pokémon de l'équipe)
        Assert.Equal(French.movelist[216], BattleNames.Move("Return 102"));
        Assert.Equal((216, (string?)null, 102), BattleNames.ParseMove("return102"));
        Assert.Equal((237, "dark", 70), BattleNames.ParseMove("hiddenpowerdark70"));
        Assert.Equal("dark", BattleNames.MoveTypeKey("hiddenpowerdark70"));
        Assert.Equal((0, (string?)null, 0), BattleNames.ParseMove("recharge"));
        // Pas d'objet : pas le premier numéro sans nom (« ??? ») de la liste de PKHeX
        Assert.Equal(0, BattleNames.ItemNumber(""));
        Assert.Equal(234, BattleNames.ItemNumber("leftovers"));
        Assert.Equal(39, BattleNames.AbilityNumber("innerfocus"));

        Assert.Equal((29, "", French.specieslist[29]), BattleNames.SpeciesOf("Nidoran-F, L50, F"));
        Assert.Equal((250, "", French.specieslist[250]), BattleNames.SpeciesOf("Ho-Oh, L70")); // tiret dans le nom
        Assert.Equal((386, "Speed", "Deoxys"), BattleNames.SpeciesOf("Deoxys-Speed, L50"));
        Assert.Equal((351, "Rainy", French.specieslist[351]), BattleNames.SpeciesOf("Castform-Rainy"));
        Assert.Equal((201, "Question", French.specieslist[201]), BattleNames.SpeciesOf("Unown-Question, L20"));
        for (byte form = 0; form < 28; form++) // lettres de Zarbi : A (sans forme), B à Z, « ! », « ? »
            Assert.Equal(form, BattleNames.FormIndex(201, BattleNames.UnownForme(form)));
        Assert.Equal(["", "B", "Z", "Exclamation"], new byte[] { 0, 1, 25, 26 }.Select(BattleNames.UnownForme));
    }

    // ---------- Équipes ----------

    private static SaveFile Save(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Sauvegardes", file);
        Assert.True(SaveUtil.TryGetSaveFile(File.ReadAllBytes(path), out var sav, file));
        return sav!;
    }

    [Fact]
    public void Team_FromALeafGreenSave()
    {
        var sav = Save("vert-feuille.sav");
        var ivysaur = BattleTeam.ToSet(sav.GetPartySlotAtIndex(0), sav);
        Assert.Equal(("Bulbidou", 2, 30), (ivysaur.Name, ivysaur.Species, ivysaur.Level)); // surnom, Herbizarre
        Assert.Equal([33, 79, 73, 75], ivysaur.Moves); // Charge, Poudre Dodo, Vampigraine, Tranch'Herbe
        Assert.Equal([0, 0, 0, 0], ivysaur.PpUps);
        Assert.Equal(65, ivysaur.Ability); // Engrais
        Assert.Equal("Coconfort", BattleTeam.ToSet(sav.GetPartySlotAtIndex(3), sav).Name); // pas surnommé : pas « COCONFORT »

        Assert.Equal(27, BattleTeam.Candidates(sav).Count()); // équipe et boîtes, sans œufs
        Assert.Equal("gen3customgame", BattleTeam.Format(3));
        Assert.Null(BattleTeam.Format(4));
        Assert.Equal(["Speed", "Attack", "Defense", ""],
            new[] { GameVersion.E, GameVersion.FR, GameVersion.LG, GameVersion.R }.Select(BattleTeam.DeoxysForme));
    }

    // ---------- Protocole ----------

    [Fact]
    public void Protocol_SecretLinesForTheirOwnerOnly()
    {
        const string chunk = "update\n|t:|123\n|move|p1a: Pika|Surf|p2a: Geo\n|split|p2\n|-damage|p2a: Geo|51/115\n|-damage|p2a: Geo|45/100\n|debug|x";
        var p2 = BattleProtocol.Lines(chunk, "p2");
        Assert.Equal(["move", "-damage"], p2.Select(l => l.Kind));
        Assert.Equal("51/115", p2[1].Arg(1));
        Assert.Equal("45/100", BattleProtocol.Lines(chunk, "p1")[1].Arg(1));

        var line = BattleLine.Parse("|-damage|p1a: Pika|80/110|[from] item: Leftovers|[of] p2a: Geo");
        Assert.Equal(("item: Leftovers", "p2a: Geo"), (line.Tag("from"), line.Tag("of")));
        Assert.Equal(("p2", "Geo"), BattleProtocol.Who("p2a: Geo"));
        Assert.Equal((0, 0, "fnt"), BattleProtocol.Condition("0 fnt"));
    }

    [Fact]
    public void View_SpeaksFrench_FromEachSide()
    {
        string[] protocol =
        [
            "|player|p1|Sacha||", "|player|p2|Ondine||", "|teamsize|p1|2", "|teamsize|p2|1",
            "|switch|p1a: Pika|Pikachu, L50, M, shiny|110/110", "|switch|p2a: Abo|Ekans, L48, F|100/100",
            "|move|p1a: Pika|Thunderbolt|p2a: Abo", "|-supereffective|p2a: Abo", "|-damage|p2a: Abo|20/100",
            "|-unboost|p2a: Abo|atk|1", "|-boost|p1a: Pika|spa|2", "|-status|p2a: Abo|par|[from] ability: Static|[of] p1a: Pika",
            "|-weather|RainDance|[from] ability: Drizzle|[of] p2a: Abo", "|-sidestart|p1: Sacha|Reflect",
            "|-damage|p2a: Abo|0 fnt", "|faint|p2a: Abo", "|win|Sacha",
        ];
        var lines = protocol.Select(BattleLine.Parse).ToList();

        var sacha = new BattleView("p1");
        var steps = sacha.Apply(lines);
        Assert.Empty(sacha.Unhandled);
        Assert.Equal(
        [
            "Go ! Pika !", "Ondine envoie Abo !", "Pika utilise Tonnerre !", "C'est super efficace !",
            "L'Attaque d'Abo ennemi baisse !", "L'Attaque Spéciale de Pika augmente beaucoup !",
            "Abo ennemi est paralysé par Statik de Pika !", "Crachin d'Abo ennemi ! Il commence à pleuvoir !",
            "Protection renforce la Défense de ton équipe !", "Abo ennemi est K.O. !", "Tu as battu Ondine !",
        ], steps.Where(s => s.Text is not null).Select(s => s.Text));
        Assert.Equal(BattleOutcome.Won, sacha.Outcome);
        var scene = sacha.Scene;
        Assert.Equal(("Pika", 25, true, 110), (scene.Me!.Name, (int)scene.Me.Species, scene.Me.Shiny, scene.Me.MaxHp));
        Assert.Equal((0, 0, 2, 2, "RainDance"), (scene.Foe!.Hp, scene.FoeLeft, scene.MyLeft, scene.MyTeam, scene.Weather));
        Assert.Contains(steps, s => s.Cue == BattleCue.Attack && s.Mine); // Pika s'avance pour attaquer

        var ondine = new BattleView("p2");
        var seen = ondine.Apply(lines).Where(s => s.Text is not null).Select(s => s.Text).ToList();
        Assert.Equal(["Sacha envoie Pika !", "Go ! Abo !", "Pika ennemi utilise Tonnerre !"], seen.Take(3));
        Assert.Contains("Protection renforce la Défense de l'équipe ennemie !", seen);
        Assert.Equal(("Sacha a gagné le combat !", BattleOutcome.Lost), (seen[^1], ondine.Outcome));
    }

    // ---------- Vrais combats dans le simulateur ----------

    /// <summary>Dossier du simulateur (à la racine du dépôt), installé : sinon le test échoue avec la commande à lancer.</summary>
    internal static ShowdownOptions Showdown([CallerFilePath] string source = "")
    {
        var folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "showdown"));
        Assert.True(Directory.Exists(Path.Combine(folder, "node_modules", "@pkmn", "sim")),
            "Simulateur de combat absent : lancer « npm ci --prefix showdown » à la racine du dépôt.");
        return new ShowdownOptions("node", folder);
    }

    /// <summary>Un combat qui se déroule dans le simulateur, lu au rythme des tests (les deux joueurs à la fois).</summary>
    private sealed class Run : IAsyncDisposable
    {
        private readonly ShowdownBattle battle;
        private readonly BlockingCollection<string> chunks = [];
        public BattleView P1 { get; } = new("p1");
        public BattleView P2 { get; } = new("p2");
        public List<BattleStep> Steps1 { get; } = [];
        public List<BattleStep> Steps2 { get; } = [];
        public bool Over => P1.Outcome != BattleOutcome.None;

        public Run(BattleSetup setup)
        {
            battle = ShowdownBattle.Start(Showdown(), setup, NullLogger.Instance);
            battle.Chunk += chunks.Add;
        }

        /// <summary>Lit le simulateur jusqu'aux requêtes des deux joueurs (ou la fin du combat).</summary>
        public void Pump()
        {
            var requests = 0;
            while (requests < 2 && !Over)
            {
                Assert.True(chunks.TryTake(out var chunk, TimeSpan.FromSeconds(15)), "le simulateur ne répond plus");
                if (BattleProtocol.Request(chunk) is var (side, request))
                {
                    (side == "p1" ? P1 : P2).Request = request;
                    requests++;
                }
                Assert.True(BattleProtocol.Error(chunk) is null, $"choix refusé : {BattleProtocol.Error(chunk)}");
                Steps1.AddRange(P1.Apply(BattleProtocol.Lines(chunk, "p1")));
                Steps2.AddRange(P2.Apply(BattleProtocol.Lines(chunk, "p2")));
            }
        }

        public void Send(string command) => battle.Send(command);

        /// <summary>
        /// Joue jusqu'au bout, comme le site : chaque requête qui attend une action reçoit aussitôt un choix ; un choix
        /// refusé (« Unavailable choice » : capacité bloquée que la requête ne montrait pas) est suivi d'une requête corrigée,
        /// qui reçoit un nouveau choix. Match nul forcé au-delà de 300 tours.
        /// </summary>
        public void PlayRandomly(Random rng)
        {
            var forcedTie = false;
            foreach (var view in new[] { P1, P2 })
            {
                if (view.Request is { } first && Choose(first, rng) is { } choice)
                    Send($"{view.Viewer} {choice}");
            }
            while (!Over)
            {
                Assert.True(chunks.TryTake(out var chunk, TimeSpan.FromSeconds(15)), "le simulateur ne répond plus");
                Steps1.AddRange(P1.Apply(BattleProtocol.Lines(chunk, "p1")));
                Steps2.AddRange(P2.Apply(BattleProtocol.Lines(chunk, "p2")));
                if (BattleProtocol.Request(chunk) is var (side, request))
                {
                    var view = side == "p1" ? P1 : P2;
                    view.Request = request;
                    if (Choose(request, rng) is { } choice)
                        Send($"{side} {choice}");
                }
                if (!forcedTie && P1.Turn > 300)
                {
                    Send("forcetie");
                    forcedTie = true;
                }
            }
        }

        public ValueTask DisposeAsync() => battle.DisposeAsync();
    }

    private static BattlePlayer Player(string name, string file)
    {
        var sav = Save(file);
        return new BattlePlayer(name, Enumerable.Range(0, sav.PartyCount).Select(i => BattleTeam.ToSet(sav.GetPartySlotAtIndex(i), sav)).ToList());
    }

    [Fact]
    public async Task RealSaves_Battle_TranslatedForEachPlayer()
    {
        var p1 = Player("Sacha", "vert-feuille.sav");
        var p2 = Player("Ondine", "rouge-feu.sav");
        await using var run = new Run(new BattleSetup("gen3customgame", [1, 2, 3, 4], p1, p2));
        run.Pump();
        var lead = p2.Team[0].Name;
        Assert.Equal(["Go ! Bulbidou !", $"Ondine envoie {lead} !"], run.Steps1.Select(s => s.Text).OfType<string>().Take(2));
        Assert.Contains("Sacha envoie Bulbidou !", run.Steps2.Select(s => s.Text));

        // Charge (capacité 1) : les PP réels (35, sans PP Plus), et le message des deux côtés.
        var tackle = run.P1.Request!.Active![0].Moves[0];
        Assert.Equal(("Tackle", 35), (tackle.Move, tackle.MaxPp));
        run.Send("p1 move 1");
        run.Send("p2 move 1");
        run.Pump();
        Assert.Contains("Bulbidou utilise Charge !", run.Steps1.Select(s => s.Text));
        Assert.Contains("Bulbidou ennemi utilise Charge !", run.Steps2.Select(s => s.Text));
        Assert.Equal(34, run.P1.Request!.Active![0].Moves[0].Pp);

        // Abandon (ou temps écoulé) : forcé par le site.
        run.Send("forcelose p2");
        run.Pump();
        Assert.Equal((BattleOutcome.Won, BattleOutcome.Lost), (run.P1.Outcome, run.P2.Outcome));
        Assert.Empty(run.P1.Unhandled.Concat(run.P2.Unhandled));
    }

    // ---------- Fiche d'un Pokémon de l'équipe ----------

    /// <summary>
    /// Fiche lue dans une requête : K.O. (ses PV maximum viennent du pont), puissance écrite dans l'identifiant, catégorie de
    /// Puissance Cachée d'après son type, capacité qui met K.O. en un coup, objet, descriptions de la table générée.
    /// </summary>
    [Fact]
    public void Sheet_FromARequestMember()
    {
        var member = new BattleTeamMember("p1: Carmilla", "Crobat, L80, F", "0 fnt", false, "leftovers", "innerfocus",
            ["return102", "hiddenpowerdark70", "sheercold", "toxic"], new BattleMemberStats(223, 137, 125, 142, 254),
            [[20, 20], [3, 24], [5, 5]], 233, ["Poison", "Flying"]);
        var sheet = BattleSheet.Of(member);
        Assert.Equal(("Carmilla", French.specieslist[169], "F", 80), (sheet.Name, sheet.SpeciesName, sheet.Gender, sheet.Level));
        Assert.EndsWith(".png", sheet.IconUrl);
        Assert.Equal((0, 233, 0, true, false), (sheet.Hp, sheet.MaxHp, sheet.Percent, sheet.Fainted, sheet.Active));
        Assert.Equal(new BattleSheetType[] { new(French.types[3], "poison"), new(French.types[2], "flying") }, sheet.Types);
        Assert.Equal((French.abilitylist[39], "Empêche la peur."), (sheet.Ability, sheet.AbilityText));
        Assert.Equal((French.itemlist[234], "images/objets/234.png"), (sheet.Item, sheet.ItemUrl));
        Assert.Equal(254, sheet.Stats!.Spe);

        var (back, hidden, cold, toxic) = (sheet.Moves[0], sheet.Moves[1], sheet.Moves[2], sheet.Moves[3]);
        Assert.Equal((French.movelist[216], "normal", 20, 20, BattleMoveCategory.Physical, "102", "100"),
            (back.Name, back.Type.Key, back.Pp, back.MaxPp, back.Category, back.PowerText, back.AccuracyText));
        Assert.Equal((French.movelist[237], French.types[16], "dark", 3, BattleMoveCategory.Special, 70, "Spéciale"),
            (hidden.Name, hidden.Type.Name, hidden.Type.Key, hidden.Pp, hidden.Category, hidden.Power, hidden.CategoryName));
        Assert.Equal(("—", "30", "Spéciale"), (cold.PowerText, cold.AccuracyText, cold.CategoryName)); // Glaciation
        Assert.Equal(((int?)null, (int?)null, "Statut", "—", "85"), (toxic.Pp, toxic.MaxPp, toxic.CategoryName, toxic.PowerText, toxic.AccuracyText)); // PP inconnus
        Assert.Contains("poison", toxic.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Physique", back.CategoryName);

        // Sans objet, PV lus dans « condition », Puissance Cachée d'un type physique, capacité et talent inconnus
        var bare = BattleSheet.Of(member with { Condition = "51/110 par", Item = "", Ability = "", MaxHp = 0, Types = null, Stats = null,
            Moves = ["hiddenpowerrock40", "inconnue"], Pp = null, Ident = "p1", Active = true });
        Assert.Equal((51, 110, 47, "par", false, true), (bare.Hp, bare.MaxHp, bare.Percent, bare.Status, bare.Fainted, bare.Active));
        Assert.Equal(((string?)null, (string?)null, "", French.specieslist[169]), (bare.Item, bare.ItemUrl, bare.AbilityText, bare.Name));
        Assert.Empty(bare.Types);
        Assert.Equal(BattleMoveCategory.Physical, bare.Moves[0].Category);
        Assert.Equal(("inconnue", "", "—", "Statut"), (bare.Moves[1].Name, bare.Moves[1].Text, bare.Moves[1].PowerText, bare.Moves[1].CategoryName));
    }

    /// <summary>Table générée (outils/combats_textes.py) : chaque talent et chaque capacité de la 3e génération a sa description.</summary>
    [Fact]
    public void DexData_DescribesEveryGen3AbilityAndMove()
    {
        Assert.Equal(77, BattleDexData.Abilities.Length);
        Assert.Equal(355, BattleDexData.Moves.Length);
        Assert.All(BattleDexData.Abilities.Skip(1), text => Assert.False(string.IsNullOrWhiteSpace(text)));
        Assert.All(BattleDexData.Moves.Skip(1), move => Assert.False(string.IsNullOrWhiteSpace(move.Text)));
        Assert.Equal((BattleMoveCategory.Physical, 35, 95), (BattleDexData.Moves[33].Category, BattleDexData.Moves[33].Power, BattleDexData.Moves[33].Accuracy)); // Charge en 3e génération
        Assert.Equal((BattleMoveCategory.Special, 0), (BattleDexData.Moves[94].Category, BattleDexData.Moves[129].Accuracy)); // Psyko ; Météores n'échoue jamais
    }

    /// <summary>
    /// Le pont ajoute à la requête ce qu'il faut à la fiche : PP de tous les Pokémon de l'équipe (Showdown ne donne que
    /// ceux du Pokémon au combat), PV maximum, types de la 3e génération, statistiques.
    /// </summary>
    [Fact]
    public async Task Sheet_ReadsPpTypesAndStats_OfTheWholeTeam()
    {
        await using var run = new Run(new BattleSetup("gen3customgame", [1, 2, 3, 4], Player("Sacha", "vert-feuille.sav"), Player("Ondine", "rouge-feu.sav")));
        run.Pump();
        run.Send("p1 move 1");
        run.Send("p2 move 2"); // Mimi-Queue : sans dégâts
        run.Pump();
        var team = run.P1.Request!.Side.Pokemon.Select(BattleSheet.Of).ToList();
        var ivysaur = team[0];
        Assert.Equal(("Bulbidou", 30, true, false), (ivysaur.Name, ivysaur.Level, ivysaur.Active, ivysaur.Fainted));
        Assert.Equal(["grass", "poison"], ivysaur.Types.Select(t => t.Key));
        Assert.Equal((ivysaur.MaxHp, 100), (ivysaur.Hp, ivysaur.Percent));
        Assert.True(ivysaur.Stats is { Atk: > 0, Def: > 0, SpA: > 0, SpD: > 0, Spe: > 0 });
        Assert.Equal((French.abilitylist[65], "Augmente attaques PLANTE.", (string?)null), (ivysaur.Ability, ivysaur.AbilityText, ivysaur.Item));
        Assert.Equal((French.movelist[33], 34, 35), (ivysaur.Moves[0].Name, ivysaur.Moves[0].Pp, ivysaur.Moves[0].MaxPp)); // Charge, utilisée une fois
        Assert.Equal((French.movelist[79], 15, 15, "Statut"), (ivysaur.Moves[1].Name, ivysaur.Moves[1].Pp, ivysaur.Moves[1].MaxPp, ivysaur.Moves[1].CategoryName));
        // Un Pokémon resté dans sa Ball : ses PP, ses PV et ses types sont là aussi
        var fearow = team[1];
        Assert.Equal((false, "Piafou"), (fearow.Active, fearow.Name));
        Assert.Equal(["normal", "flying"], fearow.Types.Select(t => t.Key));
        Assert.True(fearow.MaxHp > 0 && fearow.Hp == fearow.MaxHp);
        Assert.All(fearow.Moves, move => Assert.True(move.Pp == move.MaxPp && move.MaxPp > 0));
        run.Send("forcetie");
        run.Pump();
    }

    /// <summary>
    /// Zarbi garde sa lettre (forme d'aspect de Showdown, pour l'image) ; chaque joueur voit les sprites de son jeu : de face
    /// pour l'adversaire, de dos pour les siens, Deoxys dans la forme de son jeu.
    /// </summary>
    [Fact]
    public async Task Unown_KeepsItsLetter_SeenWithTheSpritesOfTheViewersGame()
    {
        var leafGreen = Save("vert-feuille.sav");
        var unown = BattleTeam.ToSet(leafGreen.GetPartySlotAtIndex(0), leafGreen) with
        {
            Name = "Zarbi", Species = 201, Forme = BattleNames.UnownForme(27), Item = 0, Ability = 26, Moves = [237], PpUps = [0],
        };
        await using var run = new Run(new BattleSetup("gen3customgame", [1, 2, 3, 4], new BattlePlayer("Sacha", [unown]), Player("Ondine", "rouge-feu.sav")));
        run.Pump();
        var foe = run.P2.Scene.Foe!;
        Assert.Equal((201, "Question"), (foe.Species, foe.Forme));
        Assert.Empty(run.P1.Unhandled.Concat(run.P2.Unhandled));

        var sprites = BattleSprites.For(leafGreen); // Rouge Feu / Vert Feuille : PKHeX répond Rouge Feu
        const string folder = "images/livingdex/rouge-feu-vert-feuille/";
        Assert.Equal($"{folder}201-question.png", sprites.Url(foe, back: false));
        Assert.Equal($"{folder}dos/chromatique/201-question.png", sprites.Url(foe with { Shiny = true }, back: true));
        Assert.Equal($"{folder}dos/201-b.png", sprites.Url(foe with { Forme = "B" }, back: true));
        Assert.Equal($"{folder}386-attack.png", sprites.Url(foe with { Species = 386, Forme = "Speed" }, back: false)); // Deoxys d'Émeraude
        Assert.Equal($"{folder}chromatique/351-sunny.png", sprites.Url(foe with { Species = 351, Forme = "Sunny", Shiny = true }, back: false));
        Assert.Equal($"{folder}dos/6.png", sprites.Url(foe with { Species = 6, Forme = "" }, back: true));
        Assert.Equal(BattleSprites.Fallback(foe), BattleSprites.None.Url(foe, back: true));
        Assert.StartsWith("images/pokemon/", BattleSprites.Fallback(foe));
        Assert.Equal(BattleSprites.None, BattleSprites.For(Save("ultra-soleil.sav"))); // 7e génération : pas de sprites du jeu
    }

    /// <summary>
    /// 25 combats entre équipes tirées au hasard dans toute la 3e génération (espèces, capacités, talents, objets), choix au
    /// hasard : chacun va à son terme, et la vue sait traduire chaque ligne pour les deux joueurs.
    /// </summary>
    [Fact]
    public async Task RandomBattles_EndAndAreFullyTranslated()
    {
        var unhandled = new SortedSet<string>(StringComparer.Ordinal);
        for (var seed = 1; seed <= 25; seed++)
        {
            var rng = new Random(seed);
            await using var run = new Run(new BattleSetup("gen3customgame", [seed, 7, 11, 13], RandomPlayer("A", rng), RandomPlayer("B", rng)));
            run.Pump();
            run.PlayRandomly(rng);
            Assert.NotEqual(BattleOutcome.None, run.P2.Outcome);
            unhandled.UnionWith(run.P1.Unhandled.Concat(run.P2.Unhandled));
        }
        Assert.True(unhandled.Count == 0, "lignes non traduites :\n" + string.Join("\n", unhandled));
    }

    private static readonly string[] DeoxysFormes = ["", "Attack", "Defense", "Speed"];

    private static readonly int[] HeldItems = [.. Enumerable.Range(149, 27), .. Enumerable.Range(213, 47)]; // baies et objets tenus de la 3e génération

    private static BattlePlayer RandomPlayer(string name, Random rng)
    {
        var natures = GameInfo.GetStrings("en").natures;
        var team = Enumerable.Range(0, 6).Select(_ =>
        {
            var species = (ushort)rng.Next(1, 387);
            var personal = PersonalTable.E[species];
            var moves = Enumerable.Range(0, 4).Select(_ => rng.Next(1, 355)).Distinct().ToList();
            BattleStats Ivs() => new(rng.Next(32), rng.Next(32), rng.Next(32), rng.Next(32), rng.Next(32), rng.Next(32));
            return new BattleSet(French.specieslist[species], species, species == 386 ? DeoxysFormes[rng.Next(4)] : "",
                rng.Next(3) == 0 ? 0 : HeldItems[rng.Next(HeldItems.Length)], rng.Next(2) == 0 ? personal.Ability1 : personal.Ability2,
                moves, moves.Select(_ => rng.Next(4)).ToList(), natures[rng.Next(25)], "", rng.Next(20) == 0,
                rng.Next(20, 101), rng.Next(256), Ivs(), new BattleStats(0, 0, 0, 0, 0, 0));
        }).ToList();
        return new BattlePlayer(name, team);
    }

    /// <summary>Choix au hasard parmi ceux que la requête permet (null : ce joueur attend).</summary>
    private static string? Choose(BattleRequest request, Random rng)
    {
        if (!request.MustAct)
            return null;
        var bench = request.Side.Pokemon.Select((p, i) => (p, i)).Where(x => !x.p.Active && !x.p.Condition.EndsWith("fnt")).Select(x => x.i + 1).ToList();
        if (request.MustSwitch)
            return bench.Count > 0 ? $"switch {bench[rng.Next(bench.Count)]}" : null;
        var active = request.Active![0];
        if (bench.Count > 0 && !active.Trapped && !active.MaybeTrapped && rng.Next(8) == 0)
            return $"switch {bench[rng.Next(bench.Count)]}";
        var moves = active.Moves.Select((m, i) => (m, i)).Where(x => !x.m.IsDisabled).Select(x => x.i + 1).ToList();
        return $"move {(moves.Count > 0 ? moves[rng.Next(moves.Count)] : 1)}";
    }
}
