using Game.Application.Doudizhu;
using Game.Application.Profiles;
using Game.Doudizhu.Commands;

namespace Game.Application.Tests;

public sealed class JsonProfileStoreTests
{
    [Fact]
    public void Save_round_trips_versioned_profile_and_active_game()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonProfileStore(Path.Combine(directory.Path, "profile.json"));
        var session = DoudizhuGameSession.Start(20260801);
        var profile = new LocalPlayerProfile
        {
            Beans = 1_234,
            ActiveDoudizhu = session.CreateRecoveryState(),
        };

        store.Save(profile);
        var loaded = store.Load();

        Assert.Equal(LocalPlayerProfile.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(1_234, loaded.Beans);
        Assert.NotNull(loaded.ActiveDoudizhu);
        Assert.Equal(20260801UL, loaded.ActiveDoudizhu.Seed);
        Assert.False(File.Exists(store.TemporaryPath));
    }

    [Fact]
    public void Corrupt_primary_file_falls_back_to_single_backup()
    {
        using var directory = new TemporaryDirectory();
        var profilePath = Path.Combine(directory.Path, "profile.json");
        var store = new JsonProfileStore(profilePath);
        store.Save(new LocalPlayerProfile { Beans = 111 });
        store.Save(new LocalPlayerProfile { Beans = 222 });
        File.WriteAllText(profilePath, "{ broken json");

        var loaded = store.Load();

        Assert.Equal(111, loaded.Beans);
        Assert.True(File.Exists(store.BackupPath));

        store.Save(loaded);
        Assert.Equal(111, store.Load().Beans);
        File.WriteAllText(profilePath, "{ broken again");
        Assert.Equal(111, store.Load().Beans);
    }

    [Fact]
    public void Unknown_schema_version_is_rejected_instead_of_guessed()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonProfileStore(Path.Combine(directory.Path, "profile.json"));
        var profile = new LocalPlayerProfile { SchemaVersion = 99 };

        Assert.Throws<NotSupportedException>(() => store.Save(profile));
    }

    [Fact]
    public void Invalid_recovery_command_is_rejected_before_save()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonProfileStore(Path.Combine(directory.Path, "profile.json"));
        var profile = new LocalPlayerProfile
        {
            ActiveDoudizhu = new DoudizhuRecoveryState
            {
                AcceptedCommands =
                [
                    new DoudizhuCommandRecord
                    {
                        Kind = DoudizhuStoredCommandKind.Bid,
                        PlayerIndex = 0,
                        BidAction = (DoudizhuBidAction)99,
                    },
                ],
            },
        };

        Assert.Throws<InvalidDataException>(() => store.Save(profile));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fangcun-card-club-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}
