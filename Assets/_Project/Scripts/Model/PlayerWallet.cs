using System;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Project
{
    /// <summary>
    /// 골드와 하트 잔액. 하트는 시간 충전형이라 Tick 에서 충전을 진행합니다.
    /// 서버가 없으므로 기기 시각(UTC) 기준이며 PlayerPrefs 에 저장합니다.
    /// </summary>
    public sealed class PlayerWallet : ITickable, IDisposable
    {
        private const string GoldKey = "wallet.gold";
        private const string HeartKey = "wallet.heart";
        private const string HeartNextKey = "wallet.heart.next";
        private const long StartGold = 1000;

        public const int HeartMax = 5;
        public static readonly TimeSpan HeartRechargeInterval = TimeSpan.FromMinutes(5);

        private readonly ReactiveProperty<long> _gold;
        private readonly ReactiveProperty<long> _heart;
        private readonly ReactiveProperty<int> _heartRechargeRemainingSeconds = new(0);
        private DateTime? _nextHeartAtUtc;

        public PlayerWallet()
        {
            _gold = new ReactiveProperty<long>(long.Parse(PlayerPrefs.GetString(GoldKey, StartGold.ToString())));
            _heart = new ReactiveProperty<long>(PlayerPrefs.GetInt(HeartKey, HeartMax));

            var next = PlayerPrefs.GetString(HeartNextKey, string.Empty);
            if (long.TryParse(next, out var ticks))
                _nextHeartAtUtc = new DateTime(ticks, DateTimeKind.Utc);

            Tick();
        }

        public ReadOnlyReactiveProperty<long> Gold => _gold;
        public ReadOnlyReactiveProperty<long> Heart => _heart;
        public bool IsHeartFull => _heart.Value >= HeartMax;

        /// <summary>다음 하트까지 남은 초. 가득 찼으면 0.</summary>
        public ReadOnlyReactiveProperty<int> HeartRechargeRemainingSeconds => _heartRechargeRemainingSeconds;

        public long Get(string currencyId) => currencyId switch
        {
            CurrencyId.Gold => _gold.Value,
            CurrencyId.Heart => _heart.Value,
            _ => 0,
        };

        public bool CanAfford(string currencyId, long amount) => Get(currencyId) >= amount;

        /// <summary>증감. 잔액이 음수가 되면 false 를 돌려주고 아무것도 바꾸지 않습니다.</summary>
        public bool Add(string currencyId, long delta)
        {
            switch (currencyId)
            {
                case CurrencyId.Gold:
                    if (_gold.Value + delta < 0) return false;
                    _gold.Value += delta;
                    PlayerPrefs.SetString(GoldKey, _gold.Value.ToString());
                    return true;

                case CurrencyId.Heart:
                    if (_heart.Value + delta < 0) return false;
                    var wasFull = IsHeartFull;
                    _heart.Value += delta;
                    if (wasFull && !IsHeartFull)
                        _nextHeartAtUtc = DateTime.UtcNow + HeartRechargeInterval;
                    if (IsHeartFull)
                        _nextHeartAtUtc = null;
                    SaveHeart();
                    return true;

                default:
                    return false;
            }
        }

        public void Tick()
        {
            if (IsHeartFull || _nextHeartAtUtc == null)
            {
                _heartRechargeRemainingSeconds.Value = 0;
                return;
            }

            var now = DateTime.UtcNow;
            var changed = false;
            while (_nextHeartAtUtc != null && now >= _nextHeartAtUtc.Value && !IsHeartFull)
            {
                _heart.Value += 1;
                _nextHeartAtUtc = IsHeartFull ? null : _nextHeartAtUtc.Value + HeartRechargeInterval;
                changed = true;
            }
            if (changed)
                SaveHeart();

            _heartRechargeRemainingSeconds.Value = _nextHeartAtUtc == null
                ? 0
                : Mathf.Max(0, (int)Math.Ceiling((_nextHeartAtUtc.Value - now).TotalSeconds));
        }

        private void SaveHeart()
        {
            PlayerPrefs.SetInt(HeartKey, (int)_heart.Value);
            PlayerPrefs.SetString(HeartNextKey, _nextHeartAtUtc?.Ticks.ToString() ?? string.Empty);
        }

        public void Dispose()
        {
            _gold.Dispose();
            _heart.Dispose();
            _heartRechargeRemainingSeconds.Dispose();
        }
    }
}
