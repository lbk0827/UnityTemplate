using System;
using System.Collections.Generic;
using R3;
using BK.Core.Time;
using BK.Save;

namespace BK.Meta
{
    /// <summary>
    /// Weekly rotating, strictly linear step chains (sf "endless offer"). Free steps are
    /// claimed here; paid steps are confirmed by the game after its store succeeds.
    /// </summary>
    public sealed class StepOffers : IDisposable
    {
        public const string ReasonClaim = "step_offer";

        private readonly IStepOfferCatalog _catalog;
        private readonly StepOfferData _data;
        private readonly IWallet _wallet;
        private readonly IClock _clock;
        private readonly IStageProgress _progress;
        private readonly WeekRotation _weeks;
        private readonly Subject<Unit> _progressChanged = new();

        public StepOffers(IStepOfferCatalog catalog, ISaveService saves, IWallet wallet, IClock clock, IStageProgress progress)
        {
            _catalog = catalog;
            _data = saves.Get<StepOfferData>();
            _wallet = wallet;
            _clock = clock;
            _progress = progress;
            _weeks = new WeekRotation(catalog.EpochUtc);
        }

        public bool IsUnlocked => _progress.CurrentStage.CurrentValue >= _catalog.UnlockStage;

        /// <summary>Local step advance. Lobby buttons refresh on it; an open popup should not close on it.</summary>
        public Observable<Unit> ProgressChanged => _progressChanged;

        /// <summary>
        /// Evaluates "now": may begin a new round and mark the save dirty. Call at event points
        /// (lobby entry, popup open, after a claim), never from a timer.
        /// </summary>
        public StepOfferCampaign? GetActive()
        {
            if (!IsUnlocked)
                return null;
            var now = _clock.UtcNow;
            var week = _weeks.IndexOf(now);
            var type = StepOfferLogic.TypeOfWeek(week);
            if (type == StepOfferType.None)
                return null;
            var definition = StepOfferLogic.Select(_catalog.Offers, type);
            if (definition == null)
                return null;

            var slot = _data.Find(type);
            if (slot == null)
            {
                slot = new StepOfferData.Slot { type = (int)type };
                _data.slots.Add(slot);
                _data.MarkDirty();
            }
            if (StepOfferLogic.BeginRound(slot, definition, week, _weeks.StartOf(week)))
                _data.MarkDirty();

            var campaign = new StepOfferCampaign(definition, week, slot.nextStep, _weeks.EndOf(week), slot.autoOpened);
            if (campaign.IsCompleted || now >= campaign.EndUtc)
                return null;
            return campaign;
        }

        public IReadOnlyList<StepOfferSlotView> Window(in StepOfferCampaign campaign, int slotCount)
            => StepOfferLogic.VisibleWindow(campaign.Definition, campaign.NextStep, slotCount);

        /// <summary>Time left on the clock the service runs on; zero once the week has ended.</summary>
        public TimeSpan Remaining(in StepOfferCampaign campaign) => campaign.Remaining(_clock.UtcNow);

        /// <summary>Free step only: advances first, then grants. False on a stale campaign, a paid step or completion.</summary>
        public bool TryClaimFree(in StepOfferCampaign campaign)
        {
            var step = campaign.Current;
            if (step == null || step.IsPaid)
                return false;
            if (!TryAdvance(campaign))
                return false;
            _wallet.GrantAll(step.Rewards, ReasonClaim);
            return true;
        }

        /// <summary>The game calls this after a successful purchase of the current paid step. Advances only; the store grants.</summary>
        public bool ConfirmPaid(in StepOfferCampaign campaign, string productId)
        {
            var step = campaign.Current;
            if (step == null || !step.IsPaid || step.ProductId != productId)
                return false;
            return TryAdvance(campaign);
        }

        public void MarkAutoOpened(in StepOfferCampaign campaign)
        {
            var slot = _data.Find(campaign.Definition.Type);
            if (slot == null || slot.offerId != campaign.Definition.OfferId || slot.weekIndex != campaign.WeekIndex || slot.autoOpened)
                return;
            slot.autoOpened = true;
            _data.MarkDirty();
        }

        private bool TryAdvance(in StepOfferCampaign campaign)
        {
            // Re-resolve so a card rendered for an older round or an already-advanced step cannot claim twice.
            var live = GetActive();
            if (live == null)
                return false;
            var current = live.Value;
            if (current.Definition.OfferId != campaign.Definition.OfferId || current.WeekIndex != campaign.WeekIndex || current.NextStep != campaign.NextStep)
                return false;

            var slot = _data.Find(campaign.Definition.Type);
            slot.nextStep++;
            _data.MarkDirty();
            _progressChanged.OnNext(Unit.Default);
            return true;
        }

        public void Dispose() => _progressChanged.Dispose();
    }
}
