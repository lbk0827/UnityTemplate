using System;

namespace BK.Meta
{
    /// <summary>Week index math shared by weekly content. The epoch is a UTC instant chosen by the game.</summary>
    public sealed class WeekRotation
    {
        public static readonly TimeSpan Week = TimeSpan.FromDays(7);

        public DateTime EpochUtc { get; }

        public WeekRotation(DateTime epochUtc) => EpochUtc = DateTime.SpecifyKind(epochUtc, DateTimeKind.Utc);

        /// <summary>-1 before the epoch, otherwise the zero-based week.</summary>
        public int IndexOf(DateTime nowUtc) => nowUtc < EpochUtc ? -1 : (int)((nowUtc - EpochUtc).Ticks / Week.Ticks);

        public DateTime StartOf(int index) => EpochUtc + TimeSpan.FromTicks(Week.Ticks * index);
        public DateTime EndOf(int index) => StartOf(index) + Week;

        /// <summary>sf convention: "Xd Yh" from 24h, "Xh Ym" from 1h, else "Xm Ys".</summary>
        public static string FormatRemaining(TimeSpan remaining)
        {
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;
            if (remaining.TotalHours >= 24)
                return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
            if (remaining.TotalMinutes >= 60)
                return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
            return $"{(int)remaining.TotalMinutes}m {remaining.Seconds}s";
        }
    }
}
