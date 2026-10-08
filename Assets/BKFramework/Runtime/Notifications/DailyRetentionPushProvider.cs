using System;
using System.Collections.Generic;

namespace BK.Notifications
{
    public sealed class DailyRetentionConfig
    {
        public int IdBase = 100200;
        public int Days = 30;
        public int Hour = 18;
        public int Minute = 40;
        public string Title = "";
        /// <summary>Message pool; shuffled once per build and cycled across the slots.</summary>
        public string[] Bodies = Array.Empty<string>();
    }

    /// <summary>
    /// sf's daily retention fan-out: one one-time notification per day for N days at a fixed
    /// local time, starting today (or tomorrow when the time has passed), bodies drawn from a
    /// shuffled pool so consecutive days never repeat while the pool lasts.
    /// </summary>
    public sealed class DailyRetentionPushProvider : IPushProvider
    {
        private readonly DailyRetentionConfig _config;
        private readonly Func<DateTime, int> _seedOf;

        public DailyRetentionPushProvider(DailyRetentionConfig config, Func<DateTime, int> seedOf = null)
        {
            _config = config;
            _seedOf = seedOf ?? (now => now.Date.GetHashCode());
        }

        public IEnumerable<NotificationRequest> Build(DateTime nowLocal)
        {
            if (_config.Bodies.Length == 0 || _config.Days <= 0)
                yield break;

            var first = nowLocal.Date.AddHours(_config.Hour).AddMinutes(_config.Minute);
            if (first <= nowLocal)
                first = first.AddDays(1);

            var order = Shuffle(_config.Bodies.Length, _seedOf(nowLocal));
            for (var day = 0; day < _config.Days; day++)
            {
                var body = _config.Bodies[order[day % order.Length]];
                yield return new NotificationRequest(_config.IdBase + day, _config.Title, body, first.AddDays(day));
            }
        }

        /// <summary>Fisher-Yates over indices with a fixed seed so the same day always builds the same plan.</summary>
        public static int[] Shuffle(int count, int seed)
        {
            var order = new int[count];
            for (var i = 0; i < count; i++) order[i] = i;
            var random = new System.Random(seed);
            for (var i = count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }
    }
}
