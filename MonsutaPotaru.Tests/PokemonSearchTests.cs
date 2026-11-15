using PKHeX.Core;
using MonsutaPotaru.Services;

namespace MonsutaPotaru.Tests;

/// <summary>Recherche et filtres de « Ma collection » appliqués en mémoire aux Pokémon à choisir (échanges, équipe de combat).</summary>
public sealed class PokemonSearchTests
{
    private static readonly PokemonInfo Info = new();

    private static PokemonFilterTraits Traits(PKM pk) => PokemonFilterTraits.Of(pk, Info.Describe(pk));

    private static PK5 Pk5(ushort species, byte level, int index, string? nickname = null)
    {
        var pk = SaveSessionTests.CreatePk5(species, level, "Ludo", index);
        if (nickname is not null)
        {
            pk.Nickname = nickname;
            pk.IsNicknamed = true;
        }
        pk.IV_HP = 31;
        pk.RefreshChecksum();
        return pk;
    }

    private static List<PokemonFilterTraits> Box() =>
    [
        Traits(Pk5(25, 5, 0)),               // Pikachu, Électrik
        Traits(Pk5(64, 30, 1, "Cuillère")),  // Kadabra surnommé, Psy
        Traits(Pk5(4, 12, 2)),               // Salamèche, Feu
    ];

    private static List<string> Names(IEnumerable<PokemonFilterTraits> traits) => traits.Select(t => t.Nickname).ToList();

    [Fact]
    public void Of_ReadsTheSameTraitsAsTheCollection()
    {
        var pk = Pk5(64, 30, 1, "Cuillère");
        var row = MonsutaPotaru.Services.BankService.CreateEntry(Info, pk, "sacha", DateTime.UtcNow);
        var traits = Traits(pk);
        Assert.Equal((row.Type1, row.Type2, row.Nature, row.Ability, row.IvHp, row.Level, row.Gender),
            (traits.Type1, traits.Type2, (byte?)traits.Nature, traits.Ability, traits.IvHp, traits.Level, (byte?)traits.Gender));
        Assert.Equal(("Cuillère", "Kadabra"), (traits.Nickname, traits.SpeciesName));
    }

    [Fact]
    public void Filter_BySpeciesOrNickname_IgnoringCaseAndAccents()
    {
        var box = Box();
        Assert.Equal(["Pikachu"], Names(PokemonSearch.Filter(box, " pika ", PokemonCriteria.None, t => t)));
        Assert.Equal(["Cuillère"], Names(PokemonSearch.Filter(box, "cuillere", PokemonCriteria.None, t => t)));
        Assert.Equal(["Cuillère"], Names(PokemonSearch.Filter(box, "kadabra", PokemonCriteria.None, t => t)));
        Assert.Equal(3, PokemonSearch.Filter(box, "", PokemonCriteria.None, t => t).Count);
        Assert.Empty(PokemonSearch.Filter(box, "Mew", PokemonCriteria.None, t => t));
    }

    [Fact]
    public void Filter_WithChips_KeepsTheStartingOrder()
    {
        var box = Box();
        var fireOrPsychic = PokemonCriteria.None with { Types = new HashSet<byte> { (byte)MoveType.Fire, (byte)MoveType.Psychic } };
        Assert.Equal(["Cuillère", "Salamèche"], Names(PokemonSearch.Filter(box, "", fireOrPsychic, t => t)));
        Assert.Equal(["Salamèche"], Names(PokemonSearch.Filter(box, "sala", fireOrPsychic, t => t)));
        Assert.Equal(["Cuillère", "Salamèche"], Names(PokemonSearch.Filter(box, "", PokemonCriteria.None.WithLevels(10, null), t => t)));
        Assert.Equal(["Cuillère"], Names(PokemonSearch.Filter(box, "", PokemonCriteria.None with { Nicknamed = true }, t => t)));
        Assert.Equal(3, PokemonSearch.Filter(box, "", PokemonCriteria.None with { MinIvs = [31, 0, 0, 0, 0, 0] }, t => t).Count);
    }

    [Fact]
    public void Eggs_HideTheirSpecies_AndAreLeftOutByOtherChips()
    {
        var egg = Pk5(25, 1, 3);
        egg.IsEgg = true;
        egg.RefreshChecksum();
        var box = Box().Append(Traits(egg)).ToList();

        Assert.Equal(["Pikachu"], Names(PokemonSearch.Filter(box, "pikachu", PokemonCriteria.None, t => t)));
        Assert.Equal(["Œuf"], Names(PokemonSearch.Filter(box, "œuf", PokemonCriteria.None, t => t)));
        var electric = PokemonCriteria.None with { Types = new HashSet<byte> { (byte)MoveType.Electric } };
        Assert.Equal(["Pikachu"], Names(PokemonSearch.Filter(box, "", electric, t => t)));
        Assert.Equal(["Œuf"], Names(PokemonSearch.Filter(box, "", PokemonCriteria.None with { Egg = true }, t => t)));
    }

    [Fact]
    public void Abilities_OfTheList_WithoutEggs()
    {
        var egg = Pk5(4, 1, 3);
        egg.IsEgg = true;
        egg.Ability = 200; // seul l'œuf a ce talent
        egg.RefreshChecksum();
        var box = Box().Append(Traits(egg)).ToList();
        Assert.Equal(200, box[^1].Ability);
        var abilities = PokemonSearch.Abilities(box, t => t);
        Assert.Equal(box.Take(3).Select(t => t.Ability).Where(a => a > 0).Distinct().Order(), abilities.Order());
    }
}
