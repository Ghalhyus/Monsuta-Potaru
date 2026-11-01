using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Tests;

/// <summary>Migrations qui modifient des données (pas seulement le schéma), passées sur une vraie base SQLite en mémoire.</summary>
public sealed class MigrationsTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public MigrationsTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private ApplicationDbContext NewContext() => new(_options);

    /// <summary>
    /// Compte ajouté en SQL, avec les seules colonnes d'origine d'Identity : le modèle actuel a des colonnes (Theme...) que
    /// la base, migrée seulement jusqu'à une étape plus ancienne, n'a pas encore.
    /// </summary>
    private static Task AddUserAsync(ApplicationDbContext db, string id, string userName, string? phone = null, bool twoFactor = false) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO AspNetUsers (Id, UserName, EmailConfirmed, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
            VALUES ({id}, {userName}, 0, {phone}, {phone is not null}, {twoFactor}, 1, 0)
            """);

    [Fact]
    public async Task JournalFileNames_AddsTheFileName_OfSavesThatStillExist()
    {
        using (var db = NewContext())
        {
            await db.GetService<IMigrator>().MigrateAsync("20261016182506_SpriteDetails"); // juste avant JournalFileNames
            await AddUserAsync(db, "sacha", "sacha");
            var save = new StoredSave { OwnerId = "sacha", IdentityKey = "cle", FileName = "rouge.sav", GameName = "Rouge / Bleu", TrainerName = "Red" };
            db.StoredSaves.Add(save);
            await db.SaveChangesAsync();
            db.WalletOperations.AddRange(
                new WalletOperation { OwnerId = "sacha", StoredSaveId = save.Id, SaveLabel = "Rouge / Bleu — Red" },
                new WalletOperation { OwnerId = "sacha", SaveLabel = "Noire — Ghal" }, // sauvegarde supprimée depuis
                new WalletOperation { OwnerId = "sacha" }, // achat d'emplacements : pas de sauvegarde
                new WalletOperation { OwnerId = "sacha", StoredSaveId = save.Id, SaveLabel = "Rouge / Bleu — Red\nancien.sav" });
            await db.SaveChangesAsync();
        }

        using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
            Assert.Equal(["Rouge / Bleu — Red\nrouge.sav", "Noire — Ghal", "", "Rouge / Bleu — Red\nancien.sav"],
                await db.WalletOperations.OrderBy(o => o.Id).Select(o => o.SaveLabel).ToListAsync());

            await db.GetService<IMigrator>().MigrateAsync("20261016182506_SpriteDetails"); // et retour
            Assert.Equal(["Rouge / Bleu — Red", "Noire — Ghal", "", "Rouge / Bleu — Red"],
                await db.WalletOperations.OrderBy(o => o.Id).Select(o => o.SaveLabel).ToListAsync());
        }
    }

    [Fact]
    public async Task GameGroupNames_RenamesTheCodesOfGameGroups()
    {
        const string before = "20261022190816_LivingDexes"; // juste avant GameGroupNames
        using (var db = NewContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(before);
            await AddUserAsync(db, "sacha", "sacha");
            db.StoredSaves.AddRange(
                new StoredSave { OwnerId = "sacha", IdentityKey = "a", FileName = "soulsilver.sav", GameName = "HGSS", TrainerName = "Ghal" },
                new StoredSave { OwnerId = "sacha", IdentityKey = "b", FileName = "rubis.sav", GameName = "RS", TrainerName = "Ghal" },
                new StoredSave { OwnerId = "sacha", IdentityKey = "c", FileName = "platine.sav", GameName = "Platine", TrainerName = "Lucas" });
            db.WalletOperations.AddRange(
                new WalletOperation { OwnerId = "sacha", SaveLabel = "HGSS — Ghal\nsoulsilver.sav" },
                new WalletOperation { OwnerId = "sacha", SaveLabel = "RSE — Ghal" }, // « RS — » ne doit pas le prendre
                new WalletOperation { OwnerId = "sacha", SaveLabel = "Platine — Lucas" });
            await db.SaveChangesAsync();
        }

        using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
            Assert.Equal(["Or HeartGold / Argent SoulSilver", "Rubis / Saphir", "Platine"],
                await db.StoredSaves.OrderBy(s => s.Id).Select(s => s.GameName).ToListAsync());
            Assert.Equal(["Or HeartGold / Argent SoulSilver — Ghal\nsoulsilver.sav", "Rubis / Saphir / Émeraude — Ghal", "Platine — Lucas"],
                await db.WalletOperations.OrderBy(o => o.Id).Select(o => o.SaveLabel).ToListAsync());

            await db.GetService<IMigrator>().MigrateAsync(before); // et retour
            Assert.Equal(["HGSS", "RS", "Platine"], await db.StoredSaves.OrderBy(s => s.Id).Select(s => s.GameName).ToListAsync());
            Assert.Equal(["HGSS — Ghal\nsoulsilver.sav", "RSE — Ghal", "Platine — Lucas"],
                await db.WalletOperations.OrderBy(o => o.Id).Select(o => o.SaveLabel).ToListAsync());
        }
    }
    [Fact]
    public async Task RemovePhoneAndTwoFactor_ClearsPhoneNumbers_AndTwoFactorData()
    {
        const string before = "20261025202449_SyncSaveLocations"; // juste avant RemovePhoneAndTwoFactor
        using (var db = NewContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(before);
            await AddUserAsync(db, "sacha", "sacha@kanto.fr", phone: "0612345678", twoFactor: true);
            await AddUserAsync(db, "ondine", "ondine@azuria.fr");
            db.UserTokens.AddRange(
                new IdentityUserToken<string> { UserId = "sacha", LoginProvider = "[AspNetUserStore]", Name = "AuthenticatorKey", Value = "cle" },
                new IdentityUserToken<string> { UserId = "sacha", LoginProvider = "[AspNetUserStore]", Name = "RecoveryCodes", Value = "codes" },
                new IdentityUserToken<string> { UserId = "sacha", LoginProvider = "Autre", Name = "AuthenticatorKey", Value = "garde" });
            await db.SaveChangesAsync();
        }

        using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
            var sacha = await db.Users.SingleAsync(u => u.Id == "sacha");
            Assert.Equal((null, false, false), (sacha.PhoneNumber, sacha.PhoneNumberConfirmed, sacha.TwoFactorEnabled));
            Assert.Equal("sacha@kanto.fr", sacha.UserName); // le reste ne bouge pas
            Assert.Equal("garde", Assert.Single(await db.UserTokens.ToListAsync()).Value);

            await db.GetService<IMigrator>().MigrateAsync(before); // retour : rien à remettre
            Assert.Equal(2, await db.Users.CountAsync());
        }
    }
    [Fact]
    public async Task RolesAndInviteCode_GivesTheStandardRole_ToExistingAccounts()
    {
        const string before = "20261026171901_RemovePhoneAndTwoFactor"; // juste avant RolesAndInviteCode
        using (var db = NewContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(before);
            await AddUserAsync(db, "sacha", "Sacha");
            await AddUserAsync(db, "ondine", "ondine@azuria.fr");
        }

        using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
            Assert.Equal([SiteRoles.Admin, SiteRoles.Standard], await db.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync());
            Assert.Equal([("ondine", SiteRoles.StandardId), ("sacha", SiteRoles.StandardId)],
                (await db.UserRoles.OrderBy(r => r.UserId).ToListAsync()).Select(r => (r.UserId, r.RoleId)).ToList());

            await db.GetService<IMigrator>().MigrateAsync(before); // et retour
            Assert.Empty(await db.UserRoles.ToListAsync());
            Assert.Empty(await db.Roles.ToListAsync());
        }
    }
    [Fact]
    public async Task ProfileVisibility_MakesExistingAccountsPublic_WithAllTheirPages()
    {
        const string before = "20261026181527_UserTheme"; // juste avant ProfileVisibility
        using (var db = NewContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(before);
            await AddUserAsync(db, "sacha", "Sacha");
        }

        using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
            var sacha = await db.Users.SingleAsync(u => u.Id == "sacha");
            Assert.Equal((ProfileAudience.Everyone, PublicPages.All), (sacha.Audience, sacha.PublicPages));
        }
    }

    [Fact]
    public async Task ParkLandscapes_TurnsEachPark_IntoAForest()
    {
        const string before = "20261031095313_ParkExpansions"; // juste avant ParkLandscapes
        int pikachu, misty;
        using (var db = NewContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(before);
            await AddUserAsync(db, "sacha", "Sacha");
            await AddUserAsync(db, "ondine", "Ondine");
            await db.Database.ExecuteSqlRawAsync("UPDATE AspNetUsers SET ParkExpansions = 2 WHERE Id = 'sacha';");
            var pokemon = new[] { "sacha", "ondine" }.Select(owner => new BankPokemon
            {
                OwnerId = owner, Species = 25, SpeciesName = "Pikachu", Nickname = "Pikachu", Level = 10, DepositedAt = DateTime.UtcNow, Data = [1],
            }).ToList();
            db.BankPokemon.AddRange(pokemon); // table pas modifiée depuis : le modèle actuel convient
            await db.SaveChangesAsync();
            (pikachu, misty) = (pokemon[0].Id, pokemon[1].Id);
            var at = new DateTime(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO ParkMembers (BankPokemonId, OwnerId, AddedAt) VALUES ({pikachu}, 'sacha', {at}), ({misty}, 'ondine', {at})");
        }

        using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
            var parks = await db.Parks.OrderBy(p => p.OwnerId).ToListAsync();
            Assert.Equal([("ondine", ParkLandscape.Forest, 0), ("sacha", ParkLandscape.Forest, 2)],
                parks.Select(p => (p.OwnerId, p.Landscape, p.Expansions)));
            Assert.Equal([(pikachu, parks[1].Id), (misty, parks[0].Id)],
                (await db.ParkMembers.OrderBy(m => m.BankPokemonId).ToListAsync()).Select(m => (m.BankPokemonId, m.ParkId)));

            // Et retour : Pikachu, parti à la plage, quitte le parc ; la Forêt redevient le parc avec ses agrandissements
            var beach = new Park { OwnerId = "sacha", Landscape = ParkLandscape.Beach, Expansions = 1 };
            db.Parks.Add(beach);
            await db.SaveChangesAsync();
            await db.ParkMembers.Where(m => m.BankPokemonId == pikachu).ExecuteUpdateAsync(m => m.SetProperty(x => x.ParkId, beach.Id));
            await db.GetService<IMigrator>().MigrateAsync(before);
            Assert.Equal([0, 2], await db.Database.SqlQueryRaw<int>("SELECT ParkExpansions AS Value FROM AspNetUsers ORDER BY Id").ToListAsync());
            Assert.Equal([misty], await db.Database.SqlQueryRaw<int>("SELECT BankPokemonId AS Value FROM ParkMembers").ToListAsync());
        }
    }

    [Fact]
    public async Task Friends_TurnsPrivateProfiles_IntoNobody()
    {
        const string before = "20261026185115_ProfileVisibility"; // juste avant Friends
        using (var db = NewContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(before);
            await AddUserAsync(db, "sacha", "Sacha");
            await AddUserAsync(db, "ondine", "Ondine");
            await db.Database.ExecuteSqlRawAsync("UPDATE AspNetUsers SET ProfilePrivate = 1 WHERE Id = 'ondine';");
        }

        using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
            Assert.Equal([("ondine", ProfileAudience.Nobody), ("sacha", ProfileAudience.Everyone)],
                (await db.Users.OrderBy(u => u.Id).Select(u => new { u.Id, u.Audience }).ToListAsync()).Select(u => (u.Id, u.Audience)).ToList());
            await db.Database.ExecuteSqlRawAsync("UPDATE AspNetUsers SET Audience = 1 WHERE Id = 'sacha';"); // amis seulement

            await db.GetService<IMigrator>().MigrateAsync(before); // et retour : amis seulement et personne redeviennent privés
            Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM AspNetUsers WHERE ProfilePrivate = 1").SingleAsync());
        }
    }
}
