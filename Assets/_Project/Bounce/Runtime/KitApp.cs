using System;
using BK.Meta;
using BK.Scene;
using BK.UI;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace BK.Kit
{
    /// <summary>
    /// The game hub the imported views talk to. Owns no state of its own: progress lives in
    /// IStageProgress, currencies in IWallet, settings in IOptionsService, the player name and
    /// offer steps in save slots. Every mutation flushes the save immediately.
    /// </summary>
    public sealed class KitApp : MonoBehaviour
    {
        public static KitApp Instance { get; private set; }
        public const int ClearGoldReward = 50;
        public const string ReasonClear = "stage_clear";
        public const string ReasonBooster = "booster";
        public const string ReasonStreak = "win_streak";
        public const string SaveFailedLog = "BK_Kit: progress could not be saved.";
        /// <summary>Clearing this stage (or later) is when we first ask for notification permission (sf asks at 15; Bounce is short).</summary>
        public const int PushPermissionStage = 3;

        public ShopCatalog Shop { get; private set; }
        public GameSession Session { get; } = new GameSession();
        public bool IsLoading { get; private set; }
        public int LastGoldReward { get; private set; }
        public bool HasPendingSave { get; private set; }
        public string LastSaveError { get; private set; }
        public event Action Changed;

        private KitServices services;
        private BounceProfileData profile;
        private readonly CompositeDisposable subscriptions = new();
        private int pendingStageAdvance;
        private float nextSaveAttempt;
        [SerializeField] private string lobbyScene = "Lobby";
        [SerializeField] private string ingameScene = "Ingame";

        public bool UsesFrameworkServices => services != null;
        public IWallet Wallet => services.Wallet;
        public IStageProgress Progress => services.Progress;
        public PendingRewardQueue Rewards => services.Rewards;
        public CurrencyDisplayLock DisplayLock => services.DisplayLock;
        public ContinueOffers Continues => services.Continues;
        public IMessageService Messages => services.Messages;
        public IUIService UI => services.UI;
        public StepOffers Offers => services.Offers;
        public DailyRewards Daily => services.Daily;
        public WinStreak Streak => services.Streak;

        // Read model for views. Kept flat so the imported bindings stay simple.
        public int UnlockedLevel => services.Progress.CurrentStage.CurrentValue;
        public long Gold => services.Wallet.ValueOf(BounceCurrencies.Gold);
        public long DisplayedGold => services.DisplayLock.GetDisplayValueOr(BounceCurrencies.Gold, Gold);
        public int BoosterCount(BoosterKind kind) => (int)Math.Min(int.MaxValue, services.Wallet.ValueOf(kind.ToString()));
        public string PlayerName => profile.playerName;
        public bool MusicEnabled => services.Options.Music.Value;
        public bool EffectsEnabled => services.Options.Sfx.Value;
        public bool HapticsEnabled => services.Options.Haptics.Value;
        public long Hearts => services.Wallet.ValueOf(BounceCurrencies.Heart);
        public bool HeartsInfinite => services.Wallet.IsInfinite(BounceCurrencies.Heart);
        public bool HeartsFull => HeartsInfinite || Hearts >= BounceCurrencies.HeartMax;
        public TimeSpan TimeToNextHeart => services.Wallet.TimeToNext(BounceCurrencies.Heart);
        public bool CanEnter(int level) => services.Progress.CanEnter(level);

        public void ConfigureScenes(string lobby, string ingame) { lobbyScene = lobby; ingameScene = ingame; }
        public int ConsumeStageAdvance() { int previous = pendingStageAdvance; pendingStageAdvance = 0; return previous; }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Initialize(KitServices kitServices)
        {
            services = kitServices;
            Shop = new ShopCatalog(services.Tables.Get<int, ShopProductRow>(), services.Tables.Get<string, CurrencyDefinitionRow>());
            profile = services.Saves.Get<BounceProfileData>();
            services.Wallet.Changed.Subscribe(_ => Changed?.Invoke()).AddTo(subscriptions);
            services.Progress.CurrentStage.Subscribe(_ => Changed?.Invoke()).AddTo(subscriptions);
            services.DisplayLock.Changed.Subscribe(_ => Changed?.Invoke()).AddTo(subscriptions);
            ConfigureScenes("VisualLobby", "VisualIngame");
            GoToLobby();
        }

        private void OnDestroy()
        {
            subscriptions.Dispose();
            if (Instance == this) Instance = null;
        }

        // ----- scene flow -----

        /// <summary>The single entry gate: charges a heart (stages above the free range) and loads the board.</summary>
        public void Play(int level)
        {
            if (IsLoading || !UsesFrameworkServices) return;
            level = Mathf.Clamp(level, 1, UnlockedLevel);
            if (!services.Progress.TryStart(level))
            {
                services.Messages.ShowAsync("No hearts", "Next heart in " + FormatTimer(TimeToNextHeart), "OK").Forget();
                return;
            }
            GrantStreakRewards();
            Flush();
            pendingStageAdvance = 0;
            LastGoldReward = 0;
            Time.timeScale = 1;
            Load(ingameScene, () => Session.Start(level)).Forget();
        }

        public void GoToLobby()
        {
            Time.timeScale = 1;
            if (IsLoading) return;
            if (Session.State != SessionState.Lobby) services.Streak.Abandon(); // leaving a round forfeits the streak (sf); no-op after a clear
            Load(lobbyScene, Session.ReturnToLobby).Forget();
        }

        // sf hands milestone rewards to the next attempt as entry boosters; Bounce credits them to the inventory instead.
        private void GrantStreakRewards()
        {
            var pending = services.Streak.ConsumePending();
            if (pending.Count > 0) services.Wallet.GrantAll(pending, ReasonStreak);
        }

        private async UniTask Load(string scene, Action updateSession)
        {
            if (!UsesFrameworkServices) throw new InvalidOperationException("Open the integrated bootstrap first.");
            IsLoading = true;
            Changed?.Invoke();
            // Leaving a round before the coin flight landed: the HUD must not keep showing the pre-reward balance.
            ReleaseRewardDisplay();
            try
            {
                // Close the outgoing game view before changing its world scene.
                await services.UI.CloseAllAsync(UILayer.Content);
                updateSession();
                await services.Scenes.TransitionToAsync("Bounce/Scenes/" + scene);
                await services.UI.OpenAsync<ImportedSceneView>("Bounce/UI/" + scene);
            }
            finally
            {
                IsLoading = false;
                Changed?.Invoke();
            }
            // The OS prompt is shown in the lobby, after the player has seen a few stages, never mid-round.
            if (scene == lobbyScene && services.Push.IsPermissionPending)
            {
                await services.Push.TryRequestPendingPermissionAsync();
                if (this != null) Flush(); // the prompt can outlive this hub (scene teardown, test cleanup)
            }
        }

        // ----- round results -----

        /// <summary>Credits the clear reward immediately (display-locked until the coin flight) and advances the saved stage.</summary>
        public void Complete(bool won)
        {
            if (IsLoading || !Session.Finish(won)) return;
            LastGoldReward = 0;
            if (won)
            {
                long from = Gold;
                LastGoldReward = ClearGoldReward;
                services.Wallet.Add(BounceCurrencies.Gold, ClearGoldReward, ReasonClear);
                services.DisplayLock.Engage(BounceCurrencies.Gold, from);
                services.Rewards.Enqueue(new PendingReward(BounceCurrencies.Gold, ClearGoldReward, from, from + ClearGoldReward, alreadyCredited: true));
                if (Session.Level == UnlockedLevel) pendingStageAdvance = Session.Level;
                services.Progress.Clear(Session.Level); // pointer first, then the heart refund
                if (Session.Level >= PushPermissionStage) services.Push.MarkPendingPermissionRequest();
                Flush();
            }
            Changed?.Invoke();
        }

        /// <summary>Called when the reward presentation has landed on the HUD (or was skipped).</summary>
        public void ReleaseRewardDisplay()
        {
            services.Rewards.Clear();
            services.DisplayLock.Release(BounceCurrencies.Gold);
        }

        /// <summary>Price of the next continue for the current failed round, or null when none is offered.</summary>
        public ContinueOffer? ContinueOffer
            => Session.State == SessionState.Lost && services.Continues.TryGetOffer(StageFailReason.MovesZero, out var offer) ? offer : null;

        /// <summary>Buys the continue and resumes the lost round with extra balls. False when not lost, unaffordable or the module cannot resume.</summary>
        public bool TryContinue(GameModule game, out string message)
        {
            message = "";
            var offer = ContinueOffer;
            if (offer == null || game == null || !game.CanContinue) { message = "No continue available."; return false; }
            if (!services.Continues.TryPurchase(offer.Value)) { message = "Not enough Gold"; return false; }
            Session.Resume();
            game.Continue(offer.Value.Definition.AddMoves);
            Flush();
            Changed?.Invoke();
            return true;
        }

        // ----- settings / profile -----

        public void ToggleSound() => SetMusic(!MusicEnabled);
        public void SetMusic(bool enabled) { services.Options.Music.Value = enabled; SaveSettings(); }
        public void SetEffects(bool enabled) { services.Options.Sfx.Value = enabled; SaveSettings(); }
        public void SetHaptics(bool enabled) { services.Options.Haptics.Value = enabled; SaveSettings(); }
        private void SaveSettings() { Flush(); Changed?.Invoke(); }

        public bool TrySetPlayerName(string value)
        {
            value = (value ?? "").Trim();
            if (value.Length < 1 || value.Length > 24 || Array.Exists(value.ToCharArray(), char.IsControl)) return false;
            profile.playerName = value;
            profile.MarkDirty();
            Flush();
            Changed?.Invoke();
            return true;
        }

        // Local development tooling. No remote account or production currency is involved.
        public bool TrySetSandboxProgress(int level, int gold, int boosters)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsLoading || level < 1 || level > 1000000 || gold < 0 || boosters < 0) return false;
            services.Progress.SetCurrentStage(level);
            services.Wallet.Set(BounceCurrencies.Gold, gold, "sandbox");
            foreach (var offer in BoosterCatalog.All) services.Wallet.Set(offer.Kind.ToString(), boosters, "sandbox");
            // Sandbox play is free: refill hearts and waive the entry cost for an hour.
            services.Wallet.Set(BounceCurrencies.Heart, BounceCurrencies.HeartMax, "sandbox");
            services.Wallet.Set(BounceCurrencies.InfiniteHeart, 3600, "sandbox");
            Flush();
            pendingStageAdvance = 0;
            Changed?.Invoke();
            return true;
#else
            return false;
#endif
        }

        // ----- saving -----

        /// <summary>Writes every dirty slot now. A failure keeps the data dirty and schedules a retry (no rollback).</summary>
        public bool Flush()
        {
            if (!UsesFrameworkServices) return false;
            var ok = services.Saves.Flush();
            HasPendingSave = !ok;
            if (!ok)
            {
                LastSaveError = "write failed";
                nextSaveAttempt = Time.unscaledTime + 2;
                Debug.LogError(SaveFailedLog);
            }
            else LastSaveError = null;
            return ok;
        }

        public bool RetryPendingSave()
        {
            if (!HasPendingSave) return true;
            var ok = services.Saves.Flush();
            HasPendingSave = !ok;
            nextSaveAttempt = Time.unscaledTime + 2;
            Changed?.Invoke();
            return ok;
        }

        private void Update() { if (HasPendingSave && Time.unscaledTime >= nextSaveAttempt) RetryPendingSave(); }
        private void OnApplicationPause(bool paused) { if (paused && HasPendingSave) RetryPendingSave(); }
        private void OnApplicationQuit() { if (HasPendingSave) RetryPendingSave(); }

        // ----- economy -----

        public bool TryBuyBooster(BoosterKind kind, out string message)
        {
            message = "";
            var offer = BoosterCatalog.Find(kind);
            if (IsLoading || Session.State != SessionState.Lobby || offer == null) { message = "Return to the shop to buy."; return false; }
            int price = Shop.BoosterPrice(kind);
            if (!services.Wallet.CanAfford(BounceCurrencies.Gold, price)) { message = "Not enough Gold"; return false; }
            if (BoosterCount(kind) == int.MaxValue) { message = "Inventory full"; return false; }
            services.Wallet.Add(BounceCurrencies.Gold, -price, ReasonBooster);
            services.Wallet.Add(kind.ToString(), 1, ReasonBooster);
            Flush();
            message = offer.Title + " +1";
            Changed?.Invoke();
            return true;
        }

        public const string PaymentsNotConnected = "Payments are not connected in this offline kit.\nConnect a store backend to enable purchases.";

        // Free steps grant through StepOffers; paid steps wait for a store backend (ConfirmPaid hook).
        public bool TryClaimStepOffer(in StepOfferCampaign campaign, out string message)
        {
            message = "";
            if (IsLoading || Session.State != SessionState.Lobby) { message = "Return to the lobby to collect."; return false; }
            var step = campaign.Current;
            if (step == null) { message = "All rewards collected."; return false; }
            if (step.IsPaid) { message = PaymentsNotConnected; return false; }
            if (!services.Offers.TryClaimFree(campaign)) { message = "This offer has changed. Reopen it."; return false; }
            Flush();
            message = "Collected " + BounceItems.Describe(step.Rewards);
            Changed?.Invoke();
            return true;
        }

        public bool TryClaimDaily(out string message) => Claim(services.Daily.TryClaimDaily, out message);
        public bool TryClaimDailyBonus(int index, out string message) => Claim((out ItemGrant[] g) => services.Daily.TryClaimBonus(index, out g), out message);
        public bool TryClaimHourly(out string message) => Claim(services.Daily.TryClaimHourly, out message);

        private delegate bool ClaimFunc(out ItemGrant[] granted);
        private bool Claim(ClaimFunc claim, out string message)
        {
            message = "";
            if (IsLoading || Session.State != SessionState.Lobby) { message = "Return to the lobby to collect."; return false; }
            if (!claim(out var granted)) { message = "Not available yet."; return false; }
            Flush();
            message = "Collected " + BounceItems.Describe(granted);
            Changed?.Invoke();
            return true;
        }

        public bool TryConsumeBooster(BoosterKind kind)
        {
            if (IsLoading || Session.State != SessionState.Playing || BoosterCatalog.Find(kind) == null || BoosterCount(kind) <= 0) return false;
            services.Wallet.Add(kind.ToString(), -1, ReasonBooster);
            Flush();
            Changed?.Invoke();
            return true;
        }

        public static string FormatTimer(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}
