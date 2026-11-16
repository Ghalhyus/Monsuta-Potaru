using PKHeX.Core;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>
/// Pokémon retiré de la sauvegarde, en attente d'être enregistré dans la banque : <see cref="Pokemon"/> pour un jeu,
/// <see cref="Cobblemon"/> pour un monde Minecraft (l'autre est null).
/// </summary>
public sealed record PendingDeposit(PKM? Pokemon, int Box, int Slot, CobblemonPokemon? Cobblemon = null);

/// <summary>Pokémon sorti de la banque et placé dans la sauvegarde, en attente de validation.</summary>
public sealed record PendingWithdrawal(int BankId, int Box, int Slot);

/// <summary>
/// Sauvegarde chargée par l'utilisateur pendant sa session (un circuit Blazor) : une sauvegarde de jeu (<see cref="Save"/>,
/// PKHeX) ou le dossier <c>pokemon</c> d'un monde Minecraft All the Mons (<see cref="Minecraft"/>, Cobblemon).
/// Les dépôts/retraits restent "en attente" tant que l'utilisateur n'a pas validé
/// et téléchargé la sauvegarde modifiée : rien n'est perdu ni dupliqué s'il abandonne.
/// <para>
/// Les deux mondes restent séparés : un Pokémon de Minecraft ne retourne que dans Minecraft, un Pokémon des jeux ne va
/// jamais dans Minecraft (<see cref="WithdrawalProblem(BankPokemon)"/>).
/// </para>
/// </summary>
public sealed class SaveSession
{
    public SaveFile? Save { get; private set; }
    public CobblemonSave? Minecraft { get; private set; }
    public string FileName { get; private set; } = "";
    public byte[]? OriginalData { get; private set; }

    public List<PendingDeposit> Deposits { get; } = [];
    public List<PendingWithdrawal> Withdrawals { get; } = [];

    public bool IsLoaded => Save is not null || Minecraft is not null;
    public bool HasChanges => Deposits.Count > 0 || Withdrawals.Count > 0;

    /// <summary>Refus d'un Pokémon des jeux dans un monde Minecraft.</summary>
    public const string GameToMinecraftRefusal = "Les Pokémon des jeux ne peuvent pas aller dans Minecraft.";

    /// <summary>Refus d'un Pokémon de Minecraft dans une sauvegarde de jeu.</summary>
    public const string MinecraftToGameRefusal = "Un Pokémon de Minecraft ne peut retourner que dans un monde All the Mons.";

    public bool TryLoad(byte[] data, string fileName, out string error)
    {
        if (CobblemonSave.IsZip(data))
        {
            if (CobblemonSave.TryRead(data, out error) is not { } world)
                return false;
            // level.dat donne l'identité du monde : sans elle, un monde stocké pourrait passer ici (même règle que les jeux).
            if (!world.HasLevel)
            {
                error = CobblemonSave.NoLevelMessage;
                return false;
            }
            Opened(null, world, data, fileName);
            return true;
        }

        // PKHeX peut modifier le tableau : on lui passe une copie pour garder l'original intact.
        if (!SaveUtil.TryGetSaveFile(data.ToArray(), out var sav, fileName))
        {
            error = "Fichier non reconnu comme une sauvegarde Pokémon (ni le zip du dossier « pokemon » d'un monde All the Mons).";
            return false;
        }
        if (!sav.HasBox)
        {
            error = "Cette sauvegarde n'a pas de boîtes PC.";
            return false;
        }
        SaveVersion.Refine(sav); // Vert Feuille, que PKHeX prend pour Rouge Feu

        Opened(sav, null, data, fileName);
        error = "";
        return true;
    }

    private void Opened(SaveFile? sav, CobblemonSave? world, byte[] data, string fileName)
    {
        Save = sav;
        Minecraft = world;
        FileName = fileName;
        OriginalData = data;
        Deposits.Clear();
        Withdrawals.Clear();
    }

    /// <summary>Annule toutes les opérations en attente en rechargeant la sauvegarde d'origine.</summary>
    public void Reset()
    {
        if (OriginalData is not null)
            TryLoad(OriginalData, FileName, out _);
    }

    public void Close()
    {
        Save = null;
        Minecraft = null;
        OriginalData = null;
        FileName = "";
        Deposits.Clear();
        Withdrawals.Clear();
    }

    // ---------- Boîtes (communes aux deux formats) ----------

    /// <summary>Nombre de boîtes ; pour Minecraft, l'équipe (boîte 0) puis les boîtes du PC.</summary>
    public int BoxCount => Save?.BoxCount ?? Minecraft?.PageCount ?? 0;

    /// <summary>Cases de cette boîte (6 pour l'équipe de Minecraft).</summary>
    public int SlotCount(int box) => Save?.BoxSlotCount ?? Minecraft?.SlotCount(box) ?? 0;

    /// <summary>Nom de la boîte donné dans le jeu, sinon « Boîte 3 » (« Équipe » pour Minecraft).</summary>
    public string BoxName(int box)
    {
        if (Minecraft is { } world)
            return world.PageName(box);
        var name = Save is IBoxDetailName named ? named.GetBoxName(box) : "";
        return string.IsNullOrWhiteSpace(name) ? $"Boîte {box + 1}" : name;
    }

    public PKM? GetSlot(int box, int slot)
    {
        var pk = Save?.GetBoxSlotAtIndex(box, slot);
        return pk is null || pk.Species == 0 ? null : pk;
    }

    public CobblemonPokemon? GetCobblemon(int box, int slot) => Minecraft?.Get(box, slot);

    /// <summary>Case vide (dans l'un ou l'autre format).</summary>
    public bool IsEmpty(int box, int slot) => Minecraft is { } world ? world.Get(box, slot) is null : GetSlot(box, slot) is null;

    /// <summary>Case que le jeu interdit de vider ou de remplir (Boîte Combat ; enclos de Cobblemon).</summary>
    public bool IsLocked(int box, int slot) =>
        Minecraft is { } world ? world.Get(box, slot)?.IsTethered == true
        : Save is not null && (Save.IsBoxSlotLocked(box, slot) || Save.IsBoxSlotOverwriteProtected(box, slot));

    /// <summary>Raison d'un verrou, prête à afficher.</summary>
    public string LockedMessage => Minecraft is not null
        ? "Ce Pokémon est dans un enclos : reprends-le dans le jeu avant de le déposer."
        : "Cet emplacement est verrouillé par le jeu (Boîte Combat...).";

    public bool IsPendingWithdrawal(int bankId) => Withdrawals.Any(w => w.BankId == bankId);

    public bool IsPendingSlot(int box, int slot) => Withdrawals.Any(w => w.Box == box && w.Slot == slot);

    /// <summary>
    /// Identité de la sauvegarde chargée (celle de « Mes sauvegardes ») : refusée ici tant qu'elle y est stockée. Null sans
    /// sauvegarde.
    /// </summary>
    public string? IdentityKey => Minecraft?.IdentityKey ?? (Save is { } sav ? SaveIdentity.Of(sav).Key : null);

    /// <summary>Empreintes de tous les Pokémon de la sauvegarde (refus d'une vieille copie au chargement).</summary>
    public IEnumerable<string> Fingerprints() =>
        Minecraft is { } world ? world.All.Select(p => p.Fingerprint)
        : Save is { } sav ? StoredSaveService.ReadAllPokemon(sav).Select(p => PokemonFingerprint.Of(p.Pokemon))
        : [];

    // ---------- Dépôts ----------

    /// <summary>Retire un Pokémon de la sauvegarde (en mémoire) pour le déposer dans la banque.</summary>
    public bool TryDeposit(int box, int slot, out string error)
    {
        error = "";
        if (IsEmpty(box, slot)) { error = "Emplacement vide."; return false; }
        if (IsLocked(box, slot)) { error = LockedMessage; return false; }

        var withdrawn = Withdrawals.FirstOrDefault(w => w.Box == box && w.Slot == slot);
        if (withdrawn is not null)
            Withdrawals.Remove(withdrawn); // Il sortait tout juste de la banque : on annule simplement le retrait.
        else if (Minecraft is { } world)
            Deposits.Add(new PendingDeposit(null, box, slot, world.Get(box, slot)!.Clone()));
        else
            Deposits.Add(new PendingDeposit(GetSlot(box, slot)!.Clone(), box, slot));

        Clear(box, slot);
        return true;
    }

    /// <summary>Remet un dépôt en attente dans la sauvegarde.</summary>
    public bool UndoDeposit(PendingDeposit deposit)
    {
        var (box, slot) = IsEmpty(deposit.Box, deposit.Slot) ? (deposit.Box, deposit.Slot) : FirstEmptySlot();
        if (box < 0) return false;
        if (deposit.Cobblemon is { } mon)
            Minecraft!.Set(box, slot, mon);
        else
            Save!.SetBoxSlotAtIndex(deposit.Pokemon!, box, slot);
        Deposits.Remove(deposit);
        return true;
    }

    private void Clear(int box, int slot)
    {
        if (Minecraft is { } world)
            world.Set(box, slot, null);
        else
            Save!.SetBoxSlotAtIndex(Save.BlankPKM, box, slot);
    }

    // ---------- Retraits ----------

    /// <summary>Place un Pokémon de la banque dans le premier emplacement libre de la sauvegarde.</summary>
    public bool TryWithdraw(int bankId, PKM pk, out string error) => TryWithdraw(bankId, pk, -1, -1, out error);

    /// <summary>
    /// Place un Pokémon de la banque dans l'emplacement indiqué (glisser-déposer),
    /// ou dans le premier emplacement libre si <paramref name="box"/> est négatif.
    /// </summary>
    public bool TryWithdraw(int bankId, PKM pk, int box, int slot, out string error)
    {
        if (Minecraft is not null) { error = GameToMinecraftRefusal; return false; }
        if (!CheckTarget(bankId, ref box, ref slot, out error))
            return false;
        if (Prepare(pk, out error) is not { } converted)
            return false;
        if (!FindTarget(ref box, ref slot, out error))
            return false;

        Save!.SetBoxSlotAtIndex(converted, box, slot);
        Withdrawals.Add(new PendingWithdrawal(bankId, box, slot));
        return true;
    }

    /// <summary>
    /// Retire ce Pokémon du stockage interne, quel que soit son format (jeu ou Minecraft), dans la case indiquée ou la
    /// première libre si <paramref name="box"/> est négatif. Refusé entre Minecraft et les jeux.
    /// </summary>
    public bool TryWithdraw(BankPokemon entry, int box, int slot, out string error)
    {
        if (!BankService.IsCobblemon(entry))
        {
            if (BankService.ToPkm(entry) is not { } pk) { error = "Données illisibles."; return false; }
            return TryWithdraw(entry.Id, pk, box, slot, out error);
        }

        if (Minecraft is not { } world) { error = MinecraftToGameRefusal; return false; }
        if (!CheckTarget(entry.Id, ref box, ref slot, out error))
            return false;
        if (BankService.ToCobblemon(entry) is not { } mon) { error = "Données illisibles."; return false; }
        if (!FindTarget(ref box, ref slot, out error))
            return false;

        world.Set(box, slot, mon);
        Withdrawals.Add(new PendingWithdrawal(entry.Id, box, slot));
        return true;
    }

    /// <summary>Déjà retiré, case choisie verrouillée ou occupée : refus.</summary>
    private bool CheckTarget(int bankId, ref int box, ref int slot, out string error)
    {
        error = "";
        if (IsPendingWithdrawal(bankId)) { error = "Déjà retiré."; return false; }
        if (box >= 0)
        {
            if (IsLocked(box, slot)) { error = LockedMessage; return false; }
            if (!IsEmpty(box, slot)) { error = "Cet emplacement est déjà occupé."; return false; }
        }
        return true;
    }

    /// <summary>Sans case choisie, la première libre.</summary>
    private bool FindTarget(ref int box, ref int slot, out string error)
    {
        error = "";
        if (box >= 0)
            return true;
        (box, slot) = FirstEmptySlot();
        if (box >= 0)
            return true;
        error = "Aucun emplacement libre dans les boîtes.";
        return false;
    }

    /// <summary>
    /// Raison pour laquelle ce Pokémon ne peut pas aller dans la sauvegarde chargée (« Dépôt / Retrait » le grise), ou
    /// null s'il le peut ; rien n'est écrit. Mêmes vérifications que <see cref="TryWithdraw(int, PKM, int, int, out string)"/>.
    /// </summary>
    public string? WithdrawalProblem(PKM pk) =>
        Minecraft is not null ? GameToMinecraftRefusal : Prepare(pk, out var error) is null ? error : null;

    /// <summary>
    /// <see cref="WithdrawalProblem(PKM)"/> pour un Pokémon du stockage interne, quel que soit son format (null sans
    /// sauvegarde chargée).
    /// </summary>
    public string? WithdrawalProblem(BankPokemon entry)
    {
        if (!IsLoaded)
            return null;
        if (BankService.IsCobblemon(entry))
            return Minecraft is null ? MinecraftToGameRefusal : null;
        return BankService.ToPkm(entry) is { } pk ? WithdrawalProblem(pk) : "Données illisibles.";
    }

    private PKM? Prepare(PKM pk, out string error) => PrepareFor(Save!, pk, out error);

    /// <summary>
    /// Le Pokémon au format de <paramref name="sav"/> (converti au besoin), ou null avec la raison du refus : les règles d'un
    /// retrait du stockage interne (aussi celles de la boîte de réception de la GTS).
    /// </summary>
    internal static PKM? PrepareFor(SaveFile sav, PKM pk, out string error)
    {
        error = "";
        if (pk.Species > sav.MaxSpeciesID)
        {
            error = $"Ce Pokémon n'existe pas dans cette génération (espèce n°{pk.Species}).";
            return null;
        }

        var converted = pk;
        if (pk.GetType() != sav.PKMType)
        {
            converted = EntityConverter.ConvertToType(pk, sav.PKMType, out var result);
            if (converted is null || !result.IsSuccess)
            {
                error = $"Conversion {pk.Extension.ToUpperInvariant()} → {sav.BlankPKM.Extension.ToUpperInvariant()} impossible ({result}).";
                return null;
            }
        }

        var problems = sav.EvaluateCompatibility(converted);
        if (problems.Count > 0)
        {
            error = "Incompatible avec cette sauvegarde : " + string.Join(" ; ", problems);
            return null;
        }
        return converted;
    }

    public void UndoWithdrawal(PendingWithdrawal withdrawal)
    {
        Clear(withdrawal.Box, withdrawal.Slot);
        Withdrawals.Remove(withdrawal);
    }

    // ---------- Validation ----------

    /// <summary>Le fichier modifié à rendre au joueur (sauvegarde du jeu, ou zip du dossier « pokemon »).</summary>
    public byte[] Write() =>
        Minecraft is { } world ? world.Write()
        : Save?.Write().ToArray() ?? throw new InvalidOperationException("Aucune sauvegarde chargée.");

    /// <summary>Appelé après validation en base : la sauvegarde modifiée devient la nouvelle référence.</summary>
    public void MarkCommitted(byte[] newData)
    {
        OriginalData = newData;
        Deposits.Clear();
        Withdrawals.Clear();
    }

    /// <summary>
    /// Case d'où partir pour placer un retrait sans case choisie : la première ; pour Minecraft, celle du PC (l'équipe
    /// en dernier recours).
    /// </summary>
    public (int Box, int Slot) DefaultStart => (Minecraft is not null && BoxCount > 1 ? 1 : 0, 0);

    private (int Box, int Slot) FirstEmptySlot() => FindEmptySlot(DefaultStart.Box, DefaultStart.Slot);

    /// <summary>
    /// Premier emplacement libre à partir de (<paramref name="fromBox"/>, <paramref name="fromSlot"/>) inclus,
    /// en parcourant les boîtes dans l'ordre puis en reprenant au début ; (-1, -1) si tout est plein.
    /// </summary>
    public (int Box, int Slot) FindEmptySlot(int fromBox, int fromSlot)
    {
        int boxes = BoxCount;
        if (boxes == 0)
            return (-1, -1);
        fromBox = Math.Clamp(fromBox, 0, boxes - 1);
        fromSlot = Math.Clamp(fromSlot, 0, SlotCount(fromBox) - 1);
        // Toutes les cases, boîte après boîte, en partant de (fromBox, fromSlot) et en revenant au début.
        for (int i = 0; i <= boxes; i++)
        {
            int b = (fromBox + i) % boxes;
            int first = i == 0 ? fromSlot : 0, last = i == boxes ? fromSlot : SlotCount(b);
            for (int s = first; s < last; s++)
                if (IsEmpty(b, s) && !IsLocked(b, s))
                    return (b, s);
        }
        return (-1, -1);
    }
}
