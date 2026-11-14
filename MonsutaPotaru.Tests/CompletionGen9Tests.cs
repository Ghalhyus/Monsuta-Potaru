using System.Buffers.Binary;
using PKHeX.Core;
using MonsutaPotaru.Data;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>
/// Complétion de la 9e génération : tables d'Écarlate et de Violet (générées d'après la liste de drapeaux de FlagsEditorEX
/// et la check-list de l'utilisateur) ; lecture en mémoire (pas encore de vraie sauvegarde) : listes des objets au sol et
/// des dresseurs, badges, Pokédex. Une sauvegarde vierge de PKHeX n'a aucun bloc de drapeau : ces étapes restent à faire.
/// </summary>
public sealed class CompletionGen9Tests
{
    private static CompletionRow[] Table(string game) => (CompletionRow[])typeof(Gen9CompletionData).GetField(game)!.GetValue(null)!;

    private static IEnumerable<string> Atoms(string condition) => condition.Split('|', '+');

    /// <summary>Marque une clé faite (valeur 1) dans la première place libre d'une liste d'Écarlate / Violet.</summary>
    private static void MarkDone(SAV9SV sav, uint block, string key)
    {
        var data = sav.Accessor.GetBlock(block).Data;
        for (var i = 0; i < data.Length; i += 16)
        {
            var entry = BinaryPrimitives.ReadUInt64LittleEndian(data[i..(i + 8)]);
            if (entry is 0 or 0xCBF29CE484222645)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(data[i..(i + 8)], Convert.ToUInt64(key, 16));
                BinaryPrimitives.WriteUInt64LittleEndian(data[(i + 8)..(i + 16)], 1);
                return;
            }
        }
        Assert.Fail("Liste pleine");
    }

    /// <summary>Donne à une variable (bloc numérique, sans type dans une sauvegarde vierge) la valeur voulue.</summary>
    private static void SetWork(SAV9SV sav, string condition, uint value)
    {
        var block = sav.Accessor.GetBlock(Convert.ToUInt32(condition.Split(':')[1], 16));
        block.ChangeStoredType(SCTypeCode.UInt32);
        block.SetValue(value);
    }

    [Theory]
    [InlineData("Scarlet")]
    [InlineData("Violet")]
    public void Tables_ConditionsKnownEachPlaceOnceAndEachMachineOnce(string game)
    {
        var rows = Table(game);
        Assert.DoesNotContain(rows, r => r.Section == CompletionSection.Pokedex);
        Assert.Equal([53, 383, 3750, 37, 78],
            new[] { CompletionSection.Story, CompletionSection.Trainers, CompletionSection.Items, CompletionSection.Encounters,
                    CompletionSection.Quests }.Select(s => rows.Count(r => r.Section == s)));
        foreach (var atom in rows.SelectMany(r => Atoms(r.Done)))
        {
            var parts = atom.Split(':');
            switch (parts[0])
            {
                case "X": Assert.Equal(8, parts[1].Length); break;
                case "XV": Assert.Equal(8, parts[1].Length); Assert.Equal("1", parts[2]); break;
                case "FI" or "TR": Assert.Equal(16, parts[1].Length); break;
                case "D": Assert.InRange(int.Parse(parts[1]), 1, 1025); break;
                default: Assert.Fail($"Condition inattendue en 9e génération : {atom}"); break;
            }
        }
        // Dresseurs et objets : chaque lieu en un seul bloc ; une condition = une étape ; chaque CT une fois (CT001 à CT229)
        foreach (var section in new[] { CompletionSection.Trainers, CompletionSection.Items })
        {
            var zones = rows.Where(r => r.Section == section).Select(r => r.Zone).ToList();
            var blocks = zones.Where((z, i) => i == 0 || zones[i - 1] != z).ToList();
            Assert.Equal(blocks.Count, blocks.Distinct().Count());
            var done = rows.Where(r => r.Section == section).Select(r => r.Done).ToList();
            Assert.Equal(done.Count, done.Distinct().Count());
        }
        Assert.Equal(Enumerable.Range(1, 229), rows.Where(r => r.Tm != 0).Select(r => r.Tm).Order());
        // Quêtes : chaque activité en un seul bloc
        var activities = rows.Where(r => r.Section == CompletionSection.Quests).Select(r => r.Zone).ToList();
        var groups = activities.Where((z, i) => i == 0 || activities[i - 1] != z).ToList();
        Assert.Equal(groups.Count, groups.Distinct().Count());
        // Chaque dresseur a un texte (classe traduite) et un lieu
        Assert.All(rows.Where(r => r.Section == CompletionSection.Trainers), r => Assert.False(string.IsNullOrEmpty(r.Label)));
    }

    [Fact]
    public void Labels_MachinesItemsVersionsAndPokedex()
    {
        Assert.Equal("CT001 Bélier", Gen9Completion.Label(new(CompletionSection.Items, "Machine à CT", null, "X:00000000", Tm: 1)));
        Assert.Equal("CT149 Séisme", Gen9Completion.Label(new(CompletionSection.Items, "Machine à CT", null, "X:00000000", Tm: 149)));
        Assert.Equal("Potion ×5", Gen9Completion.Label(new(CompletionSection.Items, "Cuchalaga", null, "FI:0", Item: 17, Count: 5)));
        // Pokédex : les 692 espèces présentes dans le jeu (pas Mew, par HOME seulement) ; Pêchaminus (Disque Indigo)
        Assert.Equal(692, Gen9CompletionData.Pokedex.Length);
        Assert.Contains(1025, Gen9CompletionData.Pokedex);
        Assert.DoesNotContain(151, Gen9CompletionData.Pokedex);
        // Versions : Koraidon / Miraidon, Fort-Ivoire / Roue-de-Fer, Ho-Oh d'Écarlate et Lugia de Violet (Friandises)
        Assert.Contains(Gen9CompletionData.Scarlet, r => r.Label == "Pouvoir chevaucher Koraidon");
        Assert.Contains(Gen9CompletionData.Violet, r => r.Label == "Battre le Dominant Roue-de-Fer");
        Assert.Contains(Gen9CompletionData.Scarlet, r => r.Section == CompletionSection.Encounters && r.Species == 250);
        Assert.DoesNotContain(Gen9CompletionData.Violet, r => r.Species == 250);
        Assert.Contains(Gen9CompletionData.Violet, r => r.Section == CompletionSection.Encounters && r.Species == 249);
        // Quêtes : 32 pieux, 8 revanches d'Arène, 15 coachs spéciaux ; dresseurs d'Arène faits d'office par leur badge
        Assert.Equal(32, Gen9CompletionData.Scarlet.Count(r => r.Zone == "Pieux des Fléaux"));
        Assert.Equal(8, Gen9CompletionData.Scarlet.Count(r => r.Zone == "Revanches d'Arène"));
        Assert.Equal(15, Gen9CompletionData.Scarlet.Count(r => r.Zone == "Club de la Ligue"));
        Assert.Contains(Gen9CompletionData.Scarlet, r => r.Section == CompletionSection.Trainers && r.Detail == "Arène" && r.Done.Contains("|XV:"));
        // Classes : le genre vient du nom interne, sinon la forme épicène ; jamais du prénom
        Assert.Contains(Gen9CompletionData.Scarlet, r => r.Label == "Musicien(ne) Noelia");
    }

    [Fact]
    public void Scarlet_InMemory_FieldItemsTrainersBadgesAndPokedex()
    {
        var sav = new SAV9SV { Version = GameVersion.SL };
        Assert.Equal([LivingDexGame.Scarlet], CompletionGames.For(sav));
        var empty = CompletionGames.Steps(sav, LivingDexGame.Scarlet)!;
        Assert.Equal(0, CompletionProgress.Of(empty).Tenths);
        Assert.Equal(692, empty.Count(s => s.Section == CompletionSection.Pokedex));
        Assert.Equal(CompletionSections.All, CompletionProgress.Of(empty).Sections.Select(s => s.Section));

        var rows = Gen9CompletionData.Scarlet;
        var item = rows.First(r => r.Done.StartsWith("FI:", StringComparison.Ordinal));
        var trainer = rows.First(r => r.Section == CompletionSection.Trainers && r.Detail is null);
        MarkDone(sav, 0x2482AD60, item.Done[3..]);
        MarkDone(sav, 0xF018C4AC, trainer.Done[3..]);
        SetWork(sav, rows.Single(r => r.Label == "Badge Insecte (Éra)").Done, 1); // premier badge obtenu
        sav.Zukan.SetDexEntryAll(906, false); // Poussacha

        var steps = Gen9Completion.Steps(sav, LivingDexGame.Scarlet);
        var picked = Assert.Single(steps, s => s.Done && s.Section == CompletionSection.Items);
        Assert.Equal((item.Zone, Gen9Completion.Label(item)), (picked.Zone, picked.Label));
        Assert.True(steps.Single(s => s.Label == trainer.Label).Done);
        Assert.True(steps.Single(s => s.Label == "Badge Insecte (Éra)").Done);
        Assert.False(steps.Single(s => s.Label == "Badge Plante (Colza)").Done);
        // Dresseurs de l'Arène de Sevaro : faits par le badge ; ceux de Cuencia, non
        Assert.All(steps.Where(s => s.Section == CompletionSection.Trainers && s.Zone == "Sevaro" && s.Detail == "Arène"), s => Assert.True(s.Done));
        Assert.DoesNotContain(steps, s => s.Done && s.Section == CompletionSection.Trainers && s.Zone == "Cuencia");
        Assert.True(steps.Single(s => s.Label == "Choisir son premier Pokémon").Done);
        Assert.True(steps.Single(s => s.Section == CompletionSection.Pokedex && s.Species == 906).Done);
        // Drapeaux absents d'une sauvegarde vierge : à faire (CT, histoire finie)
        Assert.DoesNotContain(steps, s => s.Done && s.Label.StartsWith("CT", StringComparison.Ordinal));
        Assert.False(steps.Single(s => s.Label.StartsWith("Finir l'histoire :", StringComparison.Ordinal)).Done);
        Assert.DoesNotContain(steps, s => s.State == CompletionState.Missed);
        Assert.InRange(CompletionProgress.Of(steps).Tenths, 1, 999);
    }
}
