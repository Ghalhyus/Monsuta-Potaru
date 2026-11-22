using System.Text.Json.Serialization;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Un Pokémon de l'animation d'échange (<c>Shared/TradeAnimation.razor</c>) : son nom, son image, et s'il évolue par
/// l'échange, son espèce et son image après l'évolution.
/// </summary>
/// <param name="Nickname">Nom affiché (surnom, ou nom de l'espèce) ; « Œuf » pour un œuf.</param>
/// <param name="SpriteUrl">Image avant l'évolution ; null pour un œuf (son image révélerait l'espèce).</param>
public sealed record TradeShowPokemon(string Nickname, string? SpriteUrl, bool IsEgg, string? EvolvedSpecies = null, string? EvolvedSpriteUrl = null)
{
    [JsonIgnore] public bool Evolves => EvolvedSpecies is not null;

    /// <summary>Le Pokémon tel qu'il est, avant une éventuelle évolution.</summary>
    public static TradeShowPokemon Of(PokemonInfo info, PKM pk) => pk.IsEgg
        ? new("Œuf", null, true)
        : new(info.Describe(pk).Nickname, PokemonSprites.Url(pk), false);

    /// <summary>Le même, avec son évolution par l'échange (<paramref name="evolved"/> : après l'évolution), s'il y en a une.</summary>
    public TradeShowPokemon Evolving(PokemonInfo info, PKM evolved, ushort? evolvedTo) => evolvedTo is null ? this
        : this with { EvolvedSpecies = info.Describe(evolved).SpeciesName, EvolvedSpriteUrl = PokemonSprites.Url(evolved) };
}

/// <summary>
/// Ce que montre l'animation d'échange à un joueur : le Pokémon qu'il envoie à <see cref="Partner"/> (un pseudo, ou le nom
/// de l'autre sauvegarde pour l'échange interne) et celui qu'il reçoit, puis leurs évolutions par échange (celle du
/// Pokémon reçu, et celle du Pokémon envoyé quand le joueur la voit aussi : échange interne).
/// </summary>
public sealed record TradeShow(string Partner, TradeShowPokemon Sent, TradeShowPokemon Received)
{
    /// <summary>Évolutions à montrer après l'échange, dans l'ordre : le Pokémon reçu, puis le Pokémon envoyé.</summary>
    [JsonIgnore] public IEnumerable<TradeShowPokemon> Evolutions => new[] { Received, Sent }.Where(p => p.Evolves);
}
