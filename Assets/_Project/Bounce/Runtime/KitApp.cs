using System;
using System.Collections;
using System.IO;
using UnityEngine;
using BK.Scene;
using BK.UI;
using Cysharp.Threading.Tasks;

namespace BK.Kit
{
    public sealed class KitApp : MonoBehaviour
    {
        public static KitApp Instance { get; private set; }
        public GameSession Session { get; } = new GameSession();
        public PlayerProgress Progress { get; private set; }
        public bool IsLoading { get; private set; }
        public const int ClearGoldReward = 50;
        public int LastGoldReward { get; private set; }
        public bool HasPendingSave { get; private set; }
        public string LastSaveError { get; private set; }
        public event Action Changed;
        private LocalSaveStore saves;
        private int pendingStageAdvance;
        private float nextSaveAttempt;
        public int ConsumeStageAdvance() {int previous=pendingStageAdvance;pendingStageAdvance=0;return previous;}
        [SerializeField] private string lobbyScene = "Lobby";
        [SerializeField] private string ingameScene = "Ingame";
        public void ConfigureScenes(string lobby, string ingame) { lobbyScene = lobby; ingameScene = ingame; }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Tests receive a separate directory, never read/write the real player profile.
            string folder = Environment.GetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR");
            saves = new LocalSaveStore(string.IsNullOrEmpty(folder)
                ? Path.Combine(Application.persistentDataPath, "Bounce", "Profiles", "local") : folder);
            Progress = saves.Load();
            AudioListener.volume = Progress.soundEnabled ? 1 : 0;
        }

        private ISceneService scenes;
        private IUIService ui;
        public bool UsesFrameworkServices => scenes != null && ui != null;
        public void Initialize(ISceneService sceneService, IUIService uiService)
        {
            scenes = sceneService;
            ui = uiService;
            ConfigureScenes("VisualLobby", "VisualIngame");
            GoToLobby();
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Play(int level)
        {
            if (IsLoading) return;
            level = Mathf.Clamp(level, 1, Progress.unlockedLevel);
            pendingStageAdvance=0;
            LastGoldReward=0;
            Time.timeScale = 1;
            Load(ingameScene, () => Session.Start(level)).Forget();
        }

        public void GoToLobby()
        {
            Time.timeScale = 1;
            if (!IsLoading) Load(lobbyScene, Session.ReturnToLobby).Forget();
        }

        private async UniTask Load(string scene, Action updateSession)
        {
            if (!UsesFrameworkServices) throw new InvalidOperationException("Open the integrated bootstrap first.");
            IsLoading = true;
            Changed?.Invoke();
            try
            {
                // Close the outgoing game view before changing its world scene.
                await ui.CloseAllAsync(UILayer.Content);
                updateSession();
                await scenes.TransitionToAsync("Bounce/Scenes/" + scene);
                await ui.OpenAsync<ImportedSceneView>("Bounce/UI/" + scene);
            }
            finally
            {
                IsLoading = false;
                Changed?.Invoke();
            }
        }

        public void Complete(bool won)
        {
            if (IsLoading || !Session.Finish(won)) return;
            LastGoldReward=0;
            if (won)
            {
                LastGoldReward=Math.Min(ClearGoldReward,int.MaxValue-Progress.gold);
                Progress.gold+=LastGoldReward;
                if(Session.Level==Progress.unlockedLevel && Session.Level<int.MaxValue)pendingStageAdvance=Session.Level;
                if(Session.Level<int.MaxValue)Progress.unlockedLevel = Math.Max(Progress.unlockedLevel, Session.Level + 1);
                SaveProgressWithRetry();
            }
            Changed?.Invoke();
        }

        public void ToggleSound()
        {
            Progress.soundEnabled = !Progress.soundEnabled;
            AudioListener.volume = Progress.soundEnabled ? 1 : 0;
            SaveProgressWithRetry();
            Changed?.Invoke();
        }

        public void SetMusic(bool enabled) {Progress.musicEnabled=enabled;SaveSettings();}
        public void SetEffects(bool enabled) {Progress.effectsEnabled=enabled;SaveSettings();}
        public void SetHaptics(bool enabled) {Progress.hapticsEnabled=enabled;SaveSettings();}
        private void SaveSettings() {SaveProgressWithRetry();Changed?.Invoke();}

        public bool TrySetPlayerName(string value)
        {
            value=(value??"").Trim();
            if(value.Length<1 || value.Length>24 || Array.Exists(value.ToCharArray(),char.IsControl))return false;
            string previous=Progress.playerName;Progress.playerName=value;
            if(!Persist()){Progress.playerName=previous;return false;}
            Changed?.Invoke();return true;
        }

        // Local development tooling. No remote account or production currency is involved.
        public bool TrySetSandboxProgress(int level,int gold,int boosters)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(IsLoading || level<1 || level>1000000 || gold<0 || boosters<0)return false;
            var previous=JsonUtility.FromJson<PlayerProgress>(JsonUtility.ToJson(Progress));
            Progress.unlockedLevel=level;Progress.gold=gold;
            foreach(var offer in BoosterCatalog.All)Progress.SetCount(offer.Kind,boosters);
            if(!Persist()){Progress=previous;return false;}
            pendingStageAdvance=0;Changed?.Invoke();return true;
#else
            return false;
#endif
        }

        private void SaveProgressWithRetry()
        {
            HasPendingSave=!Persist();nextSaveAttempt=Time.unscaledTime+2;
        }
        public bool RetryPendingSave()
        {
            if(!HasPendingSave)return true;
            bool saved=Persist(false);nextSaveAttempt=Time.unscaledTime+2;
            Changed?.Invoke();return saved;
        }
        private void Update() {if(HasPendingSave && Time.unscaledTime>=nextSaveAttempt)RetryPendingSave();}
        private void OnApplicationPause(bool paused) {if(paused && HasPendingSave)RetryPendingSave();}
        private void OnApplicationQuit() {if(HasPendingSave)RetryPendingSave();}

        public bool TryBuyBooster(BoosterKind kind,out string message)
        {
            message="";var offer=BoosterCatalog.Find(kind);
            if(IsLoading || Session.State!=SessionState.Lobby || offer==null){message="Return to the shop to buy.";return false;}
            if(Progress.gold<offer.Price){message="Not enough Gold";return false;}
            int count=Progress.Count(kind);
            if(count==int.MaxValue){message="Inventory full";return false;}
            Progress.gold-=offer.Price;Progress.SetCount(kind,count+1);
            if(!Persist()) {Progress.gold+=offer.Price;Progress.SetCount(kind,count);message="Could not save. Please try again.";return false;}
            message=offer.Title+" +1";Changed?.Invoke();return true;
        }

        public bool TryConsumeBooster(BoosterKind kind)
        {
            if(IsLoading || Session.State!=SessionState.Playing || BoosterCatalog.Find(kind)==null || Progress.Count(kind)<=0)return false;
            int count=Progress.Count(kind);Progress.SetCount(kind,count-1);
            if(!Persist()){Progress.SetCount(kind,count);return false;}
            Changed?.Invoke();return true;
        }

        private bool Persist(bool logError=true)
        {
            try { saves.Save(Progress);HasPendingSave=false;LastSaveError=null;return true; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { LastSaveError=error.Message;if(logError)Debug.LogError("BK_Kit: progress could not be saved. " + error.Message);return false; }
        }
    }
}
