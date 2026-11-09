using Microsoft.EntityFrameworkCore;
using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>Place d'un Pokémon dans une sauvegarde : équipe (<see cref="Box"/> = -1) ou boîte.</summary>
public readonly record struct TradeSlot(int Box, int Slot)
{
    public bool InParty => Box < 0;
    public static TradeSlot Party(int index) => new(-1, index);
}

/// <summary>
/// Pokémon qu'une sauvegarde peut recevoir de l'autre : de quoi l'afficher, sa place, et son empreinte
/// (<see cref="PokemonFingerprint"/>) pour vérifier au moment de l'échange qu'il n'a pas changé entre-temps.
/// </summary>
/// <param name="Problem">Échange interne : pourquoi ce Pokémon ne peut pas être choisi avec celui déjà choisi en face
/// (une équipe qui garderait seulement des œufs) ; null s'il peut l'être.</param>
public sealed record TradeCandidate(TradeSlot Slot, string Fingerprint, ushort Species, string Name, string SpeciesName, int Level,
    bool IsEgg, bool IsShiny, string SpriteUrl, string Location, string? Problem = null);

/// <summary>Deux sauvegardes qui peuvent échanger (<see cref="TradeFamily.CanTradeWith"/>) : la mienne et celle de l'ami.</summary>
public sealed record TradeSavePair(StoredSaveInfo Mine, StoredSaveInfo Theirs);

/// <summary>Un côté d'un échange : le joueur, sa sauvegarde, et le Pokémon qu'elle donne.</summary>
public sealed record TradeSide(string UserId, int SaveId, TradeCandidate Gives);

/// <summary>Résultat d'un échange ; <see cref="FirstMessage"/> et <see cref="SecondMessage"/> : ce que chacun a reçu.</summary>
/// <param name="Retry">Échec qui permet de choisir de nouveau (un Pokémon a changé, sauvegarde modifiée entre-temps).</param>
public sealed record TradeOutcome(bool Ok, string FirstMessage, string SecondMessage, bool Retry = false)
{
    public static TradeOutcome Failed(string message, bool retry = true) => new(false, message, message, retry);
}

/// <summary>Statistiques d'échange d'un joueur (pour les futurs succès).</summary>
public sealed record TradeStats(int Total, int Partners);

/// <summary>Région d'une cartouche de 1re ou 2e génération : une japonaise n'échange qu'avec une japonaise.</summary>
public enum GameBoyRegion { International, Japanese, Korean }

/// <summary>
/// Ce qui décide si deux sauvegardes peuvent échanger, comme sur la console d'origine (même règle pour l'échange entre amis
/// et l'échange interne) : le contexte de jeu de PKHeX (Noire ↔ Noire 2, Platine ↔ HeartGold, Rubis ↔ Rouge Feu ; pas
/// Let's Go ↔ Soleil), plus le Time Capsule (Rouge / Bleu / Jaune ↔ Or / Argent / Cristal). En Game Boy, pas d'échange
/// entre cartouches japonaises et non japonaises, et les coréennes (Or / Argent) n'ont pas de Time Capsule.
/// </summary>
public readonly record struct TradeFamily(EntityContext Context, GameBoyRegion Region)
{
    public static TradeFamily Of(SaveFile sav) => new(sav.Context, sav switch
    {
        SAV1 { Japanese: true } or SAV2 { Japanese: true } => GameBoyRegion.Japanese,
        SAV2 { Korean: true } => GameBoyRegion.Korean,
        _ => GameBoyRegion.International,
    });

    private bool GameBoy => Context is EntityContext.Gen1 or EntityContext.Gen2;

    public bool CanTradeWith(TradeFamily other)
    {
        if (!GameBoy || !other.GameBoy)
            return Context == other.Context;
        if (Region == other.Region)
            return true; // même génération, ou Time Capsule entre deux cartouches de même région
        // Or / Argent coréens ↔ Or / Argent / Cristal non japonais (sans Time Capsule).
        return Context == EntityContext.Gen2 && other.Context == EntityContext.Gen2
            && Region != GameBoyRegion.Japanese && other.Region != GameBoyRegion.Japanese;
    }
}

/// <summary>Une sauvegarde du joueur, et ses autres sauvegardes avec lesquelles elle peut échanger (échange interne).</summary>
public sealed record InternalTradeSave(StoredSaveInfo Save, IReadOnlyList<int> Partners);

/// <summary>
/// Ce que donnera l'échange interne choisi : <see cref="Problem"/> s'il ne peut plus se faire (avec <see cref="Retry"/> :
/// choisir de nouveau), sinon les évolutions annoncées (« Kadabra évoluera en Alakazam... »), null s'il n'y en a pas.
/// <see cref="FirstEvolution"/> : le Pokémon envoyé par la première sauvegarde.
/// </summary>
public sealed record TradePreview(string? Problem, bool Retry, string? FirstEvolution, string? SecondEvolution);

/// <summary>
/// Échanges de Pokémon entre deux sauvegardes stockées : celles de deux amis (Multijoueurs → Échanges ; le déroulé en direct
/// est dans <see cref="TradeHub"/>), ou deux sauvegardes du même compte (« Échange interne »). Deux sauvegardes échangent
/// si la console d'origine le permet (<see cref="TradeFamily"/>). Chaque Pokémon reçu prend la place de celui qui part,
/// évolue si l'échange le fait évoluer (<see cref="EvolveByTrade"/>), entre au Pokédex ; les deux sauvegardes sont écrites
/// ensemble et marquées « modifiées sur le serveur » (à télécharger, comme après un retrait).
/// </summary>
public sealed class TradeService(IDbContextFactory<ApplicationDbContext> dbFactory, StoredSaveService storedSaves, PokemonInfo info,
    ILogger<TradeService> logger)
{
    /// <summary>
    /// Paires de sauvegardes (la mienne, celle de l'ami) qui peuvent échanger, les plus récentes d'abord. Vide si
    /// <paramref name="friendId"/> n'est pas (ou plus) un ami : la page ne propose que ses amis, mais l'identifiant vient
    /// du navigateur, et la liste montre les sauvegardes de l'autre (jeu, dresseur).
    /// </summary>
    public async Task<List<TradeSavePair>> SavePairsAsync(string userId, string friendId)
    {
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            if (await FriendService.RelationAsync(db, userId, friendId) != PlayerRelation.Friend)
                return [];
        }
        var mine = await storedSaves.ListAsync(userId);
        var theirs = await storedSaves.ListAsync(friendId);
        var families = await FamiliesAsync(mine.Select(s => s.Id).Concat(theirs.Select(s => s.Id)));
        return [.. from m in mine
                   from t in theirs
                   where CanTrade(families, m.Id, t.Id)
                   orderby m.UpdatedAt descending, t.UpdatedAt descending
                   select new TradeSavePair(m, t)];
    }

    /// <summary>
    /// Échange interne : les sauvegardes stockées du joueur (ordre de « Mes sauvegardes »), chacune avec les autres avec
    /// lesquelles elle peut échanger (deux parties du même jeu comprises, jamais elle-même).
    /// </summary>
    public async Task<List<InternalTradeSave>> InternalSavesAsync(string userId)
    {
        var saves = await storedSaves.ListAsync(userId);
        var families = await FamiliesAsync(saves.Select(s => s.Id));
        return [.. saves.Select(s => new InternalTradeSave(s,
            [.. saves.Where(o => o.Id != s.Id && CanTrade(families, s.Id, o.Id)).Select(o => o.Id)]))];
    }

    private static bool CanTrade(Dictionary<int, TradeFamily> families, int a, int b) =>
        families.TryGetValue(a, out var fa) && families.TryGetValue(b, out var fb) && fa.CanTradeWith(fb);

    private async Task<Dictionary<int, TradeFamily>> FamiliesAsync(IEnumerable<int> saveIds)
    {
        var ids = saveIds.ToList();
        await using var db = await dbFactory.CreateDbContextAsync();
        var files = await db.StoredSaves.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, s.Data, s.FileName }).ToListAsync();
        var families = new Dictionary<int, TradeFamily>();
        foreach (var f in files)
        {
            if (StoredSaveService.TryRead(f.Data, f.FileName, out var sav))
                families[f.Id] = TradeFamily.Of(sav);
        }
        return families;
    }

    /// <summary>Deux sauvegardes lues, avec leur ligne en base.</summary>
    private sealed record SavePair(StoredSave FromSave, SaveFile From, StoredSave ToSave, SaveFile To);

    /// <summary>Les deux sauvegardes lues, si elles existent, sont à ces comptes, différentes, et peuvent échanger ; null sinon.</summary>
    private async Task<SavePair?> ReadPairAsync(string ownerId, int ownerSaveId, string receiverId, int receiverSaveId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var owner = await db.StoredSaves.AsNoTracking().FirstOrDefaultAsync(s => s.Id == ownerSaveId && s.OwnerId == ownerId);
        var receiver = await db.StoredSaves.AsNoTracking().FirstOrDefaultAsync(s => s.Id == receiverSaveId && s.OwnerId == receiverId);
        if (owner is null || receiver is null || owner.Id == receiver.Id
            || !StoredSaveService.TryRead(owner.Data, owner.FileName, out var from)
            || !StoredSaveService.TryRead(receiver.Data, receiver.FileName, out var to)
            || !TradeFamily.Of(from).CanTradeWith(TradeFamily.Of(to)))
            return null;
        return new(owner, from, receiver, to);
    }

    /// <summary>
    /// Pokémon de la sauvegarde de <paramref name="ownerId"/> que celle de <paramref name="receiverId"/> peut recevoir
    /// (équipe puis boîtes ; ni cases verrouillées, ni Pokémon que l'autre jeu ne connaît pas : espèce, forme, objet,
    /// capacité, attaques ; pas d'œuf vers la 1re génération). Vide si l'une des deux n'existe plus.
    /// </summary>
    public async Task<List<TradeCandidate>> CandidatesAsync(string ownerId, int ownerSaveId, string receiverId, int receiverSaveId)
    {
        var pair = await ReadPairAsync(ownerId, ownerSaveId, receiverId, receiverSaveId);
        return pair is null ? [] : Candidates(pair.From, pair.To, (_, _) => null);
    }

    /// <summary>
    /// Échange interne, troisième étape : les Pokémon de la seconde sauvegarde que la première peut recevoir à la place de
    /// <paramref name="firstGives"/>. Ceux qui laisseraient une équipe avec seulement des œufs restent dans la liste, avec la
    /// raison (<see cref="TradeCandidate.Problem"/>).
    /// </summary>
    public async Task<List<TradeCandidate>> InternalReceivableAsync(string userId, int firstSaveId, TradeCandidate firstGives, int secondSaveId)
    {
        if (await ReadPairAsync(userId, secondSaveId, userId, firstSaveId) is not { } pair)
            return [];
        var (second, first) = (pair.From, pair.To);
        var sent = Get(first, firstGives.Slot) is { Species: > 0 } pk && PokemonFingerprint.Of(pk) == firstGives.Fingerprint ? pk : null;
        var sentIsEgg = sent?.IsEgg ?? firstGives.IsEgg;
        return Candidates(second, first, (slot, received) =>
            !LeavesAPokemon(first, firstGives.Slot, received.IsEgg) ? $"L'équipe de « {Label(pair.ToSave)} » garderait seulement des œufs."
            : !LeavesAPokemon(second, slot, sentIsEgg) ? $"L'équipe de « {Label(pair.FromSave)} » garderait seulement des œufs."
            : null);
    }

    /// <summary>« Noire — Ghal », comme <see cref="StoredSaveInfo.Label"/>.</summary>
    private static string Label(StoredSave save) => $"{save.GameName} — {save.TrainerName}";

    private List<TradeCandidate> Candidates(SaveFile from, SaveFile to, Func<TradeSlot, PKM, string?> problem)
    {
        var candidates = new List<TradeCandidate>();
        foreach (var (slot, pk) in Pokemon(from))
        {
            if (ConvertFor(pk, to) is null)
                continue;
            var view = info.Describe(pk);
            var location = slot.InParty ? $"Équipe {slot.Slot + 1}" : $"Boîte {slot.Box + 1}, case {slot.Slot + 1}";
            candidates.Add(new TradeCandidate(slot, PokemonFingerprint.Of(pk), pk.Species, pk.IsEgg ? "Œuf" : view.Nickname,
                pk.IsEgg ? "" : view.SpeciesName, pk.IsEgg ? 0 : pk.CurrentLevel, pk.IsEgg, pk.IsShiny && !pk.IsEgg,
                PokemonSprites.Url(pk), location, problem(slot, pk)));
        }
        return candidates;
    }

    /// <summary>Pokémon de l'équipe puis des boîtes (cases verrouillées exclues : Boîte Combat, équipe de combat...).</summary>
    private static IEnumerable<(TradeSlot Slot, PKM Pokemon)> Pokemon(SaveFile sav)
    {
        for (int i = 0; i < sav.PartyCount; i++)
        {
            if (sav.GetPartySlotAtIndex(i) is { Species: > 0 } pk)
                yield return (TradeSlot.Party(i), pk);
        }
        for (int box = 0; box < sav.BoxCount; box++)
        {
            for (int slot = 0; slot < sav.BoxSlotCount; slot++)
            {
                if (!sav.IsBoxSlotLocked(box, slot) && sav.GetBoxSlotAtIndex(box, slot) is { Species: > 0 } pk)
                    yield return (new TradeSlot(box, slot), pk);
            }
        }
    }

    private static PKM? Get(SaveFile sav, TradeSlot slot)
    {
        if (slot.InParty)
            return slot.Slot >= 0 && slot.Slot < sav.PartyCount ? sav.GetPartySlotAtIndex(slot.Slot) : null;
        var inBox = slot.Box < sav.BoxCount && slot.Slot >= 0 && slot.Slot < sav.BoxSlotCount && !sav.IsBoxSlotLocked(slot.Box, slot.Slot);
        return inBox ? sav.GetBoxSlotAtIndex(slot.Box, slot.Slot) : null;
    }

    /// <summary>
    /// Le Pokémon au format de l'autre sauvegarde, s'il peut y aller ; null sinon. Time Capsule : vers la 1re génération,
    /// seulement les espèces et les attaques qu'elle connaît, et pas d'œuf.
    /// </summary>
    private static PKM? ConvertFor(PKM pk, SaveFile to)
    {
        if (pk.IsEgg && to.Generation == 1)
            return null;
        var converted = pk.GetType() == to.PKMType ? pk.Clone() : EntityConverter.ConvertToType(pk, to.PKMType, out _);
        if (converted is null || converted.Species > to.MaxSpeciesID
            || converted.Move1 > to.MaxMoveID || converted.Move2 > to.MaxMoveID || converted.Move3 > to.MaxMoveID || converted.Move4 > to.MaxMoveID)
            return null;
        return to.IsCompatiblePKM(converted) ? converted : null;
    }

    /// <summary>
    /// Une équipe garde au moins un Pokémon qui n'est pas un œuf : on ne donne pas son dernier contre un œuf.
    /// </summary>
    private static bool LeavesAPokemon(SaveFile sav, TradeSlot gives, bool receivesAnEgg)
    {
        if (!gives.InParty || !receivesAnEgg)
            return true;
        for (int i = 0; i < sav.PartyCount; i++)
        {
            if (i != gives.Slot && sav.GetPartySlotAtIndex(i) is { Species: > 0, IsEgg: false })
                return true;
        }
        return false;
    }

    /// <summary>Pierre Stase, selon la numérotation des objets de chaque génération (pas d'objet tenu en 1re génération).</summary>
    private static int Everstone(PKM pk) => pk.Format switch { 1 => -1, 2 => 112, 3 => 195, _ => 229 };

    private const ushort Karrablast = 588, Shelmet = 616;

    /// <summary>
    /// Fait évoluer un Pokémon qui vient d'être échangé, selon les évolutions de son jeu (PKHeX) : par échange (Kadabra),
    /// avec un objet tenu, consommé (Onix et Peau Métal), ou contre l'autre de la paire Carabing / Escargaume. Ni un œuf, ni
    /// un Pokémon qui tient une Pierre Stase. Renvoie la nouvelle espèce, ou null s'il n'évolue pas.
    /// </summary>
    public static ushort? EvolveByTrade(PKM pk, ushort partnerSpecies)
    {
        if (pk.IsEgg || pk.HeldItem == Everstone(pk))
            return null;
        var tree = EvolutionTree.GetEvolutionTree(pk.Context);
        foreach (var evo in tree.Forward.GetForward(pk.Species, pk.Form).Span)
        {
            if (!EvolvesByTrade(pk, evo, partnerSpecies))
                continue;
            Evolve(pk, evo);
            return evo.Species;
        }
        return null;
    }

    /// <summary>Cette évolution se fait-elle par cet échange (contre <paramref name="partnerSpecies"/>) ?</summary>
    private static bool EvolvesByTrade(PKM pk, EvolutionMethod evo, ushort partnerSpecies) => evo.Method switch
    {
        EvolutionType.Trade => true,
        EvolutionType.TradeHeldItem => pk.HeldItem == evo.Argument,
        EvolutionType.TradeShelmetKarrablast => partnerSpecies == (pk.Species == Karrablast ? Shelmet : Karrablast),
        _ => false,
    };

    /// <summary>Fait évoluer le Pokémon : objet consommé, nouvelle espèce (et son nom s'il n'a pas de surnom), talent, stats.</summary>
    private static void Evolve(PKM pk, EvolutionMethod evo)
    {
        if (evo.Method == EvolutionType.TradeHeldItem)
            pk.HeldItem = 0; // l'objet est consommé
        var wasNicknamed = pk.IsNicknamed;
        pk.Species = evo.Species;
        pk.Form = evo.GetDestinationForm(pk.Form);
        if (!wasNicknamed)
            pk.ClearNickname(); // le nom de la nouvelle espèce
        if (pk.Format >= 3)
            pk.RefreshAbility(pk.AbilityNumber >> 1);
        pk.ResetPartyStats();
    }

    /// <summary>
    /// Un échange prêt à écrire : les deux sauvegardes (suivies par le contexte), les Pokémon qui partent, et ce que chacune
    /// reçoit, déjà converti et évolué (<c>ReceivedByA</c> : son nom avant l'évolution).
    /// </summary>
    private sealed record Prepared(StoredSave SaveA, SaveFile SavA, PKM PkA, PKM ToA, string ReceivedByA, ushort? EvolvedToA,
        StoredSave SaveB, SaveFile SavB, PKM PkB, PKM ToB, string ReceivedByB, ushort? EvolvedToB);

    /// <summary>
    /// Relit les deux sauvegardes et prépare l'échange : <paramref name="first"/> donne <c>first.Gives</c> à la sauvegarde
    /// de <paramref name="second"/>, et inversement. Refus si une sauvegarde manque (ou les deux sont la même), si un Pokémon
    /// a changé depuis le choix (empreinte), si une équipe garderait seulement des œufs, ou si un Pokémon ne peut plus aller
    /// dans l'autre jeu.
    /// </summary>
    private async Task<(Prepared? Ready, TradeOutcome? Failure)> PrepareAsync(ApplicationDbContext db, TradeSide first, TradeSide second)
    {
        if (first.SaveId == second.SaveId)
            return (null, TradeOutcome.Failed("Une sauvegarde ne peut pas échanger avec elle-même : choisis-en deux.", retry: false));
        var saveA = await db.StoredSaves.FirstOrDefaultAsync(s => s.Id == first.SaveId && s.OwnerId == first.UserId);
        var saveB = await db.StoredSaves.FirstOrDefaultAsync(s => s.Id == second.SaveId && s.OwnerId == second.UserId);
        if (saveA is null || saveB is null)
            return (null, TradeOutcome.Failed("Une des deux sauvegardes n'existe plus : l'échange est annulé.", retry: false));
        if (!StoredSaveService.TryRead(saveA.Data, saveA.FileName, out var savA) || !StoredSaveService.TryRead(saveB.Data, saveB.FileName, out var savB)
            || !TradeFamily.Of(savA).CanTradeWith(TradeFamily.Of(savB)))
            return (null, TradeOutcome.Failed("Une des deux sauvegardes ne peut plus être lue : l'échange est annulé.", retry: false));

        var pkA = Get(savA, first.Gives.Slot);
        var pkB = Get(savB, second.Gives.Slot);
        if (pkA is not { Species: > 0 } || PokemonFingerprint.Of(pkA) != first.Gives.Fingerprint
            || pkB is not { Species: > 0 } || PokemonFingerprint.Of(pkB) != second.Gives.Fingerprint)
            return (null, TradeOutcome.Failed("Une des sauvegardes a changé depuis le choix (mise à jour, autre échange) : choisis de nouveau."));
        if (!LeavesAPokemon(savA, first.Gives.Slot, pkB.IsEgg) || !LeavesAPokemon(savB, second.Gives.Slot, pkA.IsEgg))
            return (null, TradeOutcome.Failed("Une équipe garderait seulement des œufs : choisis un autre Pokémon."));

        var toB = ConvertFor(pkA, savB);
        var toA = ConvertFor(pkB, savA);
        if (toA is null || toB is null)
            return (null, TradeOutcome.Failed("Un des deux Pokémon ne peut plus aller dans l'autre jeu : choisis-en un autre."));
        var receivedByA = ReceivedName(toA); // avant l'évolution : « tu as reçu Kadabra, qui a évolué en Alakazam »
        var receivedByB = ReceivedName(toB);
        var evolvedToB = EvolveByTrade(toB, pkB.Species);
        var evolvedToA = EvolveByTrade(toA, pkA.Species);
        return (new Prepared(saveA, savA, pkA, toA, receivedByA, evolvedToA, saveB, savB, pkB, toB, receivedByB, evolvedToB), null);
    }

    /// <summary>
    /// Écrit l'échange préparé : chaque Pokémon à la place de l'autre, les deux sauvegardes dans une même transaction
    /// (copies de sécurité avant et après), marquées modifiées sur le serveur ; <paramref name="record"/> : la ligne des
    /// statistiques (échange entre amis seulement). Renvoie un échec si une sauvegarde a changé entre-temps, null sinon.
    /// </summary>
    private async Task<TradeOutcome?> WriteAsync(ApplicationDbContext db, Prepared p, TradeSide first, TradeSide second, TradeRecord? record)
    {
        Set(p.SavA, first.Gives.Slot, p.ToA);
        Set(p.SavB, second.Gives.Slot, p.ToB);
        var dataA = p.SavA.Write().ToArray();
        var dataB = p.SavB.Write().ToArray();

        await storedSaves.WriteBackupAsync(first.UserId, p.SaveA, "echange-avant");
        await storedSaves.WriteBackupAsync(second.UserId, p.SaveB, "echange-avant");
        var now = DateTime.UtcNow;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await storedSaves.ApplyServerVersionAsync(db, p.SaveA, p.SavA, dataA, m => m.PokemonTraded = true, now);
            await storedSaves.ApplyServerVersionAsync(db, p.SaveB, p.SavB, dataB, m => m.PokemonTraded = true, now);
            if (record is not null)
            {
                record.At = now;
                db.TradeRecords.Add(record);
            }
            try
            {
                await db.SaveChangesAsync(); // jeton de concurrence : échoue si une sauvegarde a changé depuis sa lecture
            }
            catch (DbUpdateConcurrencyException)
            {
                return TradeOutcome.Failed("Une des sauvegardes vient d'être modifiée ailleurs : choisis de nouveau.");
            }
            await tx.CommitAsync();
        }
        await storedSaves.WriteBackupAsync(first.UserId, p.SaveA, "echange-apres");
        await storedSaves.WriteBackupAsync(second.UserId, p.SaveB, "echange-apres");
        return null;
    }

    /// <summary>
    /// Fait l'échange entre amis : chacun reçoit le Pokémon qu'il a choisi chez l'autre, à la place de celui qu'il donne.
    /// Tout ou rien ; compté dans les statistiques (<see cref="StatsAsync"/>).
    /// </summary>
    public async Task<TradeOutcome> ExecuteAsync(TradeSide first, TradeSide second)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var (p, failure) = await PrepareAsync(db, first, second);
        if (p is null)
            return failure!;
        var record = new TradeRecord
        {
            FirstId = first.UserId,
            SecondId = second.UserId,
            FirstGave = p.PkA.Species,
            FirstGaveEvolvedTo = p.EvolvedToB,
            SecondGave = p.PkB.Species,
            SecondGaveEvolvedTo = p.EvolvedToA,
        };
        if (await WriteAsync(db, p, first, second, record) is { } conflict)
            return conflict;
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Échange entre les comptes {First} (sauvegarde {SaveA}) et {Second} (sauvegarde {SaveB}).",
                first.UserId, p.SaveA.Id, second.UserId, p.SaveB.Id);

        return new(true, Received(p.ReceivedByA, p.ToA, p.EvolvedToA, p.SaveA), Received(p.ReceivedByB, p.ToB, p.EvolvedToB, p.SaveB));
    }

    /// <summary>
    /// Échange interne, dernière étape : ce que donnera l'échange (évolutions annoncées), ou pourquoi il ne peut plus se
    /// faire. Rien n'est écrit.
    /// </summary>
    public async Task<TradePreview> InternalPreviewAsync(string userId, int firstSaveId, TradeCandidate firstGives, int secondSaveId, TradeCandidate secondGives)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var (p, failure) = await PrepareAsync(db, new TradeSide(userId, firstSaveId, firstGives), new TradeSide(userId, secondSaveId, secondGives));
        if (p is null)
            return new(failure!.FirstMessage, failure.Retry, null, null);
        return new(null, false, Evolution(p.ReceivedByB, p.ToB, p.EvolvedToB, p.SaveB), Evolution(p.ReceivedByA, p.ToA, p.EvolvedToA, p.SaveA));
    }

    /// <summary>« Kadabra évoluera en Alakazam en arrivant dans « Noire 2 — Ghal ». » ; null sans évolution.</summary>
    private string? Evolution(string name, PKM evolved, ushort? evolvedTo, StoredSave arrival) => evolvedTo is null ? null
        : $"{name} évoluera en {info.Describe(evolved).SpeciesName} en arrivant dans « {Label(arrival)} ».";

    /// <summary>
    /// Fait l'échange interne : la première sauvegarde donne <paramref name="firstGives"/> à la seconde, qui lui donne
    /// <paramref name="secondGives"/>. Mêmes règles et même écriture qu'un échange entre amis (sauvegardes revérifiées : au
    /// compte, différentes, compatibles), mais pas compté dans les statistiques.
    /// </summary>
    public async Task<TradeOutcome> ExecuteInternalAsync(string userId, int firstSaveId, TradeCandidate firstGives, int secondSaveId, TradeCandidate secondGives)
    {
        var first = new TradeSide(userId, firstSaveId, firstGives);
        var second = new TradeSide(userId, secondSaveId, secondGives);
        await using var db = await dbFactory.CreateDbContextAsync();
        var (p, failure) = await PrepareAsync(db, first, second);
        if (p is null)
            return failure!;
        if (await WriteAsync(db, p, first, second, record: null) is { } conflict)
            return conflict;
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Échange interne du compte {User} entre les sauvegardes {SaveA} et {SaveB}.", userId, p.SaveA.Id, p.SaveB.Id);

        var message = $"Échange réussi : « {Label(p.SaveA)} » a reçu {p.ReceivedByA}{Evolved(p.ToA, p.EvolvedToA)}, "
            + $"et « {Label(p.SaveB)} » a reçu {p.ReceivedByB}{Evolved(p.ToB, p.EvolvedToB)}. "
            + "Télécharge les deux sauvegardes (« Mes sauvegardes », ou « Récupérer » depuis ta 3DS) et remets-les dans tes jeux.";
        return new(true, message, message);
    }

    private static void Set(SaveFile sav, TradeSlot slot, PKM pk)
    {
        if (slot.InParty)
            sav.SetPartySlotAtIndex(pk, slot.Slot, EntityImportSettings.All); // dresseur actuel, Pokédex
        else
            sav.SetBoxSlotAtIndex(pk, slot.Box, slot.Slot, EntityImportSettings.All);
    }

    /// <summary>« Kadabra », « Bob (Kadabra) » pour un Pokémon surnommé, « un œuf ».</summary>
    private string ReceivedName(PKM pk)
    {
        if (pk.IsEgg)
            return "un œuf";
        var view = info.Describe(pk);
        return view.Nickname == view.SpeciesName ? view.SpeciesName : $"{view.Nickname} ({view.SpeciesName})";
    }

    /// <summary>« , qui a évolué en Alakazam » ; vide sans évolution.</summary>
    private string Evolved(PKM pk, ushort? evolvedTo) => evolvedTo is null ? "" : $", qui a évolué en {info.Describe(pk).SpeciesName}";

    private string Received(string name, PKM pk, ushort? evolvedTo, StoredSave save) =>
        $"Échange réussi : tu as reçu {name}{Evolved(pk, evolvedTo)} dans « {Label(save)} ». "
        + "Télécharge cette sauvegarde (« Mes sauvegardes », ou « Récupérer » depuis ta 3DS) et remets-la dans ton jeu.";

    /// <summary>Échanges réussis du joueur avec ses amis (pas les échanges internes) : en tout, et avec combien de partenaires.</summary>
    public async Task<TradeStats> StatsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var partners = await db.TradeRecords.AsNoTracking()
            .Where(t => t.FirstId == userId || t.SecondId == userId)
            .Select(t => t.FirstId == userId ? t.SecondId : t.FirstId)
            .ToListAsync();
        return new(partners.Count, partners.Distinct().Count());
    }
}
