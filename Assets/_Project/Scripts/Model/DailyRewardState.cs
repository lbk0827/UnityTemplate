using System;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Project
{
    /// <summary>
    /// 7일 주기 출석 보상과 1시간 주기 무료 코인의 수령 상태.
    /// 날짜 경계는 기기 로컬 자정, 무료 코인은 마지막 수령 시각 + 1시간.
    /// </summary>
    public sealed class DailyRewardState : ITickable, IDisposable
    {
        public const int CycleDays = 7;
        public static readonly TimeSpan FreeCoinInterval = TimeSpan.FromHours(1);

        private readonly ReactiveProperty<int> _nextRewardDay;      // 0..6
        private readonly ReactiveProperty<bool> _claimedToday = new(false);
        private readonly ReactiveProperty<bool> _freeCoinAvailable = new(false);
        private readonly ReactiveProperty<int> _freeCoinRemainingSeconds = new(0);
        private readonly ReactiveProperty<int> _claimableCount = new(0);

        private string _lastClaimDate;
        private DateTime _freeCoinClaimedAtUtc;

        public DailyRewardState()
        {
            _nextRewardDay = new ReactiveProperty<int>(PlayerPrefs.GetInt("daily.next", 0));
            _lastClaimDate = PlayerPrefs.GetString("daily.lastDate", string.Empty);
            _freeCoinClaimedAtUtc = long.TryParse(PlayerPrefs.GetString("daily.freeAt", string.Empty), out var ticks)
                ? new DateTime(ticks, DateTimeKind.Utc)
                : DateTime.MinValue;
            Tick();
        }

        /// <summary>다음에 받을 날 인덱스(0 기반). 오늘 이미 받았으면 오늘 받은 날의 다음.</summary>
        public ReadOnlyReactiveProperty<int> NextRewardDay => _nextRewardDay;
        public ReadOnlyReactiveProperty<bool> ClaimedToday => _claimedToday;
        public ReadOnlyReactiveProperty<bool> FreeCoinAvailable => _freeCoinAvailable;
        public ReadOnlyReactiveProperty<int> FreeCoinRemainingSeconds => _freeCoinRemainingSeconds;

        /// <summary>지금 받을 수 있는 보상 개수. 홈 화면 레드닷에 표시.</summary>
        public ReadOnlyReactiveProperty<int> ClaimableCount => _claimableCount;

        /// <summary>오늘 강조할 날(1 기반 테이블 ID). 받았으면 방금 받은 날, 아니면 받을 날.</summary>
        public int TodayDayId => _claimedToday.Value
            ? ((_nextRewardDay.Value + CycleDays - 1) % CycleDays) + 1
            : _nextRewardDay.Value + 1;

        public bool CanClaimDaily(int dayIndex) => !_claimedToday.Value && dayIndex == _nextRewardDay.Value;

        public bool IsDailyReceived(int dayIndex)
            => dayIndex < _nextRewardDay.Value || (_claimedToday.Value && dayIndex == (_nextRewardDay.Value + CycleDays - 1) % CycleDays);

        public bool TryClaimDaily(int dayIndex)
        {
            if (!CanClaimDaily(dayIndex))
                return false;

            _lastClaimDate = Today();
            _nextRewardDay.Value = (_nextRewardDay.Value + 1) % CycleDays;
            PlayerPrefs.SetInt("daily.next", _nextRewardDay.Value);
            PlayerPrefs.SetString("daily.lastDate", _lastClaimDate);
            Tick();
            return true;
        }

        public bool TryClaimFreeCoin()
        {
            if (!_freeCoinAvailable.Value)
                return false;

            _freeCoinClaimedAtUtc = DateTime.UtcNow;
            PlayerPrefs.SetString("daily.freeAt", _freeCoinClaimedAtUtc.Ticks.ToString());
            Tick();
            return true;
        }

        public void Tick()
        {
            _claimedToday.Value = _lastClaimDate == Today();

            var remaining = _freeCoinClaimedAtUtc + FreeCoinInterval - DateTime.UtcNow;
            _freeCoinAvailable.Value = remaining <= TimeSpan.Zero;
            _freeCoinRemainingSeconds.Value = remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalSeconds);

            _claimableCount.Value = (_claimedToday.Value ? 0 : 1) + (_freeCoinAvailable.Value ? 1 : 0);
        }

        private static string Today() => DateTime.Now.ToString("yyyy-MM-dd");

        public void Dispose()
        {
            _nextRewardDay.Dispose();
            _claimedToday.Dispose();
            _freeCoinAvailable.Dispose();
            _freeCoinRemainingSeconds.Dispose();
            _claimableCount.Dispose();
        }
    }
}
