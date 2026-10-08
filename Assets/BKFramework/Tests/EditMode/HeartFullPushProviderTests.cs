using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BK.Meta;
using BK.Save;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class HeartFullPushProviderTests
    {
        private static readonly DateTime T0 = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        private string _dir;

        private sealed class Catalog : ICurrencyCatalog
        {
            public IReadOnlyList<CurrencyDefinition> Definitions { get; } = new[]
            {
                new CurrencyDefinition
                {
                    Id = "Heart", Kind = CurrencyKind.Rechargeable, InitialValue = 5, RechargeMax = 5, RechargeAmount = 1,
                    RechargeInterval = TimeSpan.FromMinutes(30), InfiniteBuffIds = new[] { "InfiniteHeart" },
                },
                new CurrencyDefinition { Id = "InfiniteHeart", Kind = CurrencyKind.Buff },
            };
        }

        [SetUp] public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [Test]
        public void SchedulesAtFullTimeOnlyWhileRecharging()
        {
            var clock = new FakeClock(T0);
            using var wallet = new Wallet(new Catalog(), new SaveService(_dir, 0f), clock);
            var provider = new HeartFullPushProvider(wallet, "Heart", 1001, "Hearts", "Full again");
            var nowLocal = T0.ToLocalTime();
            Assert.That(provider.Build(nowLocal), Is.Empty, "full: nothing to announce");

            wallet.Add("Heart", -2, "test"); // 3 hearts, anchor = now → full in 60 minutes
            var requests = provider.Build(nowLocal).ToList();
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests[0].Id, Is.EqualTo(1001));
            Assert.That(requests[0].FireAtLocal, Is.EqualTo(T0.AddMinutes(60).ToLocalTime()));

            wallet.Set("InfiniteHeart", 600, "buff");
            Assert.That(provider.Build(nowLocal), Is.Empty, "infinite buff: hearts do not matter");
        }
    }
}
