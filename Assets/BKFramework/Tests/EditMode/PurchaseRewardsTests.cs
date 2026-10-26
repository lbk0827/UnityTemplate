using System;
using System.Collections.Generic;
using System.IO;
using BK.Meta;
using BK.Save;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class PurchaseRewardsTests
    {
        private string directory;
        private sealed class Catalog : ICurrencyCatalog
        { public IReadOnlyList<CurrencyDefinition> Definitions { get; } = new[] { new CurrencyDefinition { Id = "Gold" } }; }
        [SetUp] public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "BKPurchaseTests", Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        private Wallet Wallet(ISaveService saves) => new(new Catalog(), saves, new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        [Test]
        public void DuplicateReceiptDoesNotGrantAgainAfterReload()
        {
            var saves = new SaveService(directory);
            using (var wallet = Wallet(saves))
            {
                var rewards = new PurchaseRewards(wallet, saves);
                Assert.That(rewards.Grant("tx1", "coins", "campaign", new[] { new ItemGrant("Gold", 100) }), Is.True);
                Assert.That(saves.Flush(), Is.True);
            }
            var reloaded = new SaveService(directory);
            using var again = Wallet(reloaded);
            var ledger = new PurchaseRewards(again, reloaded);
            Assert.That(ledger.Grant("tx1", "coins", "campaign", new[] { new ItemGrant("Gold", 100) }), Is.False);
            Assert.That(again.ValueOf("Gold"), Is.EqualTo(100));
            Assert.That(ledger.Count("coins"), Is.EqualTo(1));
            Assert.That(ledger.Find("tx1").context, Is.EqualTo("campaign"));
            Assert.Throws<InvalidOperationException>(() => ledger.Grant("tx1", "other", "", Array.Empty<ItemGrant>()));
        }

        [Test]
        public void InvalidBasketCannotPartiallyGrantOrConsumeReceipt()
        {
            var saves = new SaveService(directory);
            using var wallet = Wallet(saves);
            var ledger = new PurchaseRewards(wallet, saves);
            Assert.Throws<KeyNotFoundException>(() => ledger.Grant("tx", "bundle", "", new[] { new ItemGrant("Gold", 100), new ItemGrant("missing", 1) }));
            Assert.That(wallet.ValueOf("Gold"), Is.Zero);
            Assert.That(ledger.Find("tx"), Is.Null);
        }
    }
}
