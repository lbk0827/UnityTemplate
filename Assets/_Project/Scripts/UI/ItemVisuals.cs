using System;
using UnityEngine;

namespace Project
{
    /// <summary>아트가 없는 동안 아이템 ID 를 색과 짧은 라벨로 표현합니다.</summary>
    public static class ItemVisuals
    {
        public static Color ColorOf(string itemId) => itemId switch
        {
            CurrencyId.Gold => new Color(1f, 0.82f, 0.2f),
            CurrencyId.Heart => new Color(0.95f, 0.3f, 0.4f),
            _ => new Color(0.6f, 0.6f, 0.7f),
        };

        public static string ShortLabel(string itemId) => itemId switch
        {
            CurrencyId.Gold => "G",
            CurrencyId.Heart => "♥",
            _ => itemId.Length > 0 ? itemId.Substring(0, 1) : "?",
        };

        public static string Grouped(long value) => value.ToString("N0");

        /// <summary>mm:ss, 한 시간 이상이면 hh:mm:ss.</summary>
        public static string Countdown(int seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{span.Minutes:00}:{span.Seconds:00}";
        }
    }
}
