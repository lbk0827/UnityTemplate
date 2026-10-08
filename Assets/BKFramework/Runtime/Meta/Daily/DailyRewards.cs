using System;
using System.Collections.Generic;
using R3;
using BK.Core.Time;
using BK.Save;

namespace BK.Meta
{
    public interface IDailyRewardCatalog
    {
        /// <summary>One entry per day of the cycle (sf: 7).</summary>
        IReadOnlyList<ItemGrant[]> Days { get; }

        /// <summary>Extra claims available every day, in order (sf: three rewarded-ad slots).</summary>
        IReadOnlyList<ItemGrant[]> BonusSlots { get; }

        /// <summary>Claimable once per hour. Empty disables it.</summary>
        ItemGrant[] Hourly { get; }
    }

    [Serializable]
    public sealed class DailyRewardsData : SaveData
    {
        public override int CurrentVersion => 1;
        /// <summary>0-based day to claim next.</summary>
        public int nextDay;
        public long lastDailyTicks;
        public int bonusClaimed;
        public long lastBonusTicks;
        public long lastHourlyTicks;
        public override bool Validate() => nextDay >= 0 && bonusClaimed >= 0;
    }

    /// <summary>Pure calendar rules. "A new day" is a change of the local calendar date, not a rolling 24h.</summary>
    public static class DailyRewardLogic
    {
        public static DateTime LocalDate(DateTime utc) => utc.ToLocalTime().Date;

        public static bool IsNewLocalDay(long lastUtcTicks, DateTime nowUtc)
            => lastUtcTicks == 0 || LocalDate(new DateTime(lastUtcTicks, DateTimeKind.Utc)) < LocalDate(nowUtc);

        public static int NextDay(int day, int cycleLength) => cycleLength <= 0 ? 0 : (day + 1) % cycleLength;

        public static bool HourlyReady(long lastUtcTicks, DateTime nowUtc)
            => lastUtcTicks == 0 || nowUtc >= new DateTime(lastUtcTicks, DateTimeKind.Utc) + TimeSpan.FromHours(1);

        public static TimeSpan TimeToHourly(long lastUtcTicks, DateTime nowUtc)
        {
            if (lastUtcTicks == 0) return TimeSpan.Zero;
            var ready = new DateTime(lastUtcTicks, DateTimeKind.Utc) + TimeSpan.FromHours(1);
            return ready > nowUtc ? ready - nowUtc : TimeSpan.Zero;
        }
    }

    /// <summary>Day-cycle login rewards, daily bonus slots and an hourly gift. No streak penalty, no catch-up.</summary>
    public sealed class DailyRewards : IDisposable
    {
        public const string ReasonDaily = "daily_reward";
        public const string ReasonBonus = "daily_bonus";
        public const string ReasonHourly = "hourly_gift";

        private readonly IDailyRewardCatalog _catalog;
        private readonly DailyRewardsData _data;
        private readonly IWallet _wallet;
        private readonly IClock _clock;
        private readonly Subject<Unit> _changed = new();

        public DailyRewards(IDailyRewardCatalog catalog, ISaveService saves, IWallet wallet, IClock clock)
        {
            _catalog = catalog;
            _data = saves.Get<DailyRewardsData>();
            _wallet = wallet;
            _clock = clock;
            if (_catalog.Days.Count > 0 && _data.nextDay >= _catalog.Days.Count)
            {
                _data.nextDay = 0;
                _data.MarkDirty();
            }
        }

        public Observable<Unit> Changed => _changed;
        public int CycleLength => _catalog.Days.Count;
        public int NextDay => _data.nextDay;
        public bool CanClaimDaily => CycleLength > 0 && DailyRewardLogic.IsNewLocalDay(_data.lastDailyTicks, _clock.UtcNow);

        public int BonusClaimed => DailyRewardLogic.IsNewLocalDay(_data.lastBonusTicks, _clock.UtcNow) ? 0 : _data.bonusClaimed;
        public int BonusSlotCount => _catalog.BonusSlots.Count;
        public bool CanClaimBonus(int index) => index == BonusClaimed && index < BonusSlotCount;

        public bool HasHourly => _catalog.Hourly != null && _catalog.Hourly.Length > 0;
        public bool CanClaimHourly => HasHourly && DailyRewardLogic.HourlyReady(_data.lastHourlyTicks, _clock.UtcNow);
        public TimeSpan TimeToHourly => DailyRewardLogic.TimeToHourly(_data.lastHourlyTicks, _clock.UtcNow);

        /// <summary>Badge count: daily + remaining bonus slots + hourly.</summary>
        public int ClaimableCount => (CanClaimDaily ? 1 : 0) + Math.Max(0, BonusSlotCount - BonusClaimed) + (CanClaimHourly ? 1 : 0);

        public bool TryClaimDaily(out ItemGrant[] granted)
        {
            granted = Array.Empty<ItemGrant>();
            if (!CanClaimDaily)
                return false;
            granted = _catalog.Days[_data.nextDay];
            _data.nextDay = DailyRewardLogic.NextDay(_data.nextDay, CycleLength);
            _data.lastDailyTicks = _clock.UtcNow.Ticks;
            _data.MarkDirty();
            _wallet.GrantAll(granted, ReasonDaily);
            _changed.OnNext(Unit.Default);
            return true;
        }

        public bool TryClaimBonus(int index, out ItemGrant[] granted)
        {
            granted = Array.Empty<ItemGrant>();
            if (!CanClaimBonus(index))
                return false;
            var claimedToday = BonusClaimed; // normalises a stale counter from a previous date
            granted = _catalog.BonusSlots[index];
            _data.bonusClaimed = claimedToday + 1;
            _data.lastBonusTicks = _clock.UtcNow.Ticks;
            _data.MarkDirty();
            _wallet.GrantAll(granted, ReasonBonus);
            _changed.OnNext(Unit.Default);
            return true;
        }

        public bool TryClaimHourly(out ItemGrant[] granted)
        {
            granted = Array.Empty<ItemGrant>();
            if (!CanClaimHourly)
                return false;
            granted = _catalog.Hourly;
            _data.lastHourlyTicks = _clock.UtcNow.Ticks;
            _data.MarkDirty();
            _wallet.GrantAll(granted, ReasonHourly);
            _changed.OnNext(Unit.Default);
            return true;
        }

        public void Dispose() => _changed.Dispose();
    }
}
