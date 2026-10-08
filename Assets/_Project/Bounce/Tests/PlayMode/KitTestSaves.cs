using System;
using System.Collections.Generic;
using BK.Kit;
using BK.Meta;
using BK.Options;
using BK.Save;

/// <summary>
/// Reads and seeds the Bounce save folder the way the app does, through SaveService slots.
/// Always uses a flush interval of 0 so no background flush driver is spawned for a throwaway service.
/// </summary>
public static class KitTestSaves
{
    private static SaveService Open(string folder) => new(folder, 0f);

    public static long Gold(string folder) => Currency(folder, BounceCurrencies.Gold);
    public static long Booster(string folder, BoosterKind kind) => Currency(folder, kind.ToString());
    public static long Hearts(string folder) => Currency(folder, BounceCurrencies.Heart);
    public static int Level(string folder) => Open(folder).Get<StageProgressData>().currentStage;
    public static string PlayerName(string folder) => Open(folder).Get<BounceProfileData>().playerName;
    public static OptionsData Options(string folder) => Open(folder).Get<OptionsData>();
    public static int OfferStep(string folder, OfferKind kind) => Open(folder).Get<BounceOfferData>().Step(kind);

    private static long Currency(string folder, string id)
    {
        var entry = Open(folder).Get<WalletData>().Find(id);
        return entry == null ? 0 : entry.value;
    }

    /// <summary>Writes a profile before the app boots. Unset values keep the catalog defaults.</summary>
    public static void Seed(string folder, long? gold = null, int? level = null, long? hearts = null, bool infiniteHearts = false,
        IReadOnlyDictionary<BoosterKind, long> boosters = null)
    {
        var saves = Open(folder);
        var wallet = saves.Get<WalletData>();
        void Set(string id, long value, long stamp = 0)
        {
            var entry = wallet.Find(id);
            if (entry == null) { entry = new WalletData.Entry { id = id }; wallet.entries.Add(entry); }
            entry.value = value; entry.stampTicks = stamp;
        }
        if (gold.HasValue) Set(BounceCurrencies.Gold, gold.Value);
        if (hearts.HasValue) Set(BounceCurrencies.Heart, hearts.Value);
        if (infiniteHearts) Set(BounceCurrencies.InfiniteHeart, 3600, DateTime.UtcNow.AddHours(1).Ticks);
        if (boosters != null) foreach (var pair in boosters) Set(pair.Key.ToString(), pair.Value);
        wallet.MarkDirty();
        if (level.HasValue)
        {
            var stage = saves.Get<StageProgressData>();
            stage.currentStage = level.Value; stage.MarkDirty();
        }
        if (!saves.Flush()) throw new InvalidOperationException("Could not seed the test save folder: " + folder);
    }

    public static IReadOnlyDictionary<BoosterKind, long> Boosters(long missiles = 0, long extraBalls = 0, long bombs = 0, long lasers = 0)
        => new Dictionary<BoosterKind, long>
        {
            [BoosterKind.Missile] = missiles, [BoosterKind.ExtraBall] = extraBalls, [BoosterKind.Bomb] = bombs, [BoosterKind.Laser] = lasers,
        };
}
