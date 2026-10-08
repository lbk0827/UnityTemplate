using System;
using System.Collections;
using System.Linq;
using System.Threading;
using BK.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    public sealed class ImportedSceneView : MonoBehaviour, IUIView
    {
        public GamePresentation presentation;
        public bool lobby;
        private KitApp app;
        private RectTransform canvas;
        private GameObject screen, hud, popup;
        private BounceModule game;
        private AudioSource music, effects;
        private bool resultShown;
        private VisualBindings ingameBindings;
        private StagePathView stagePath;
        private KitDialog dialog;
        private LobbyOffersView offersView;
        private GameObject saveNotice;
        private TMP_Text profileInitials;
        private VisualBindings heartHud;
        private TMP_Text dailyCount;
        private GameObject dailyDot;

        public UILayer Layer => UILayer.Content;
        public bool IsOpen { get; private set; }
        public UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InitializePresentation();
            return UniTask.CompletedTask;
        }
        public UniTask OnOpenAsync(CancellationToken cancellationToken)
        {
            IsOpen = true;
            return UniTask.CompletedTask;
        }
        public UniTask OnCloseAsync(CancellationToken cancellationToken)
        {
            IsOpen = false;
            if (app != null) app.Changed -= Refresh;
            if (game != null) { game.CountersChanged -= UpdateCounts; game.End(); }
            if (music != null) music.Stop();
            Time.timeScale = 1;
            return UniTask.CompletedTask;
        }
        public bool OnBackRequested()
        {
            // Consumed in every case: this view drives its own settings/result back flow
            // and must never be popped by the framework.
            if(app==null || app.IsLoading)return true;
            if(dialog!=null){dialog.Close();return true;}
            if(offersView!=null && offersView.IsOpen){offersView.Close();return true;}
            if(resultShown)
            {
                var reward=popup==null?null:popup.GetComponent<ClearRewardView>();
                if(reward!=null)reward.Collect();else app.GoToLobby();
            }
            else if(popup!=null)ClosePopup();else OpenSettings();
            return true;
        }
        private void InitializePresentation()
        {
            app=KitApp.Instance;
            if(app==null) {Debug.LogError("Open VisualBootstrap first.");return;}
            // UIService already parents this view under its full-screen Content canvas.
            // A nested Canvas would leave a default 100x100 rect and collapse authored anchors.
            canvas=(RectTransform)transform;
            var scaler=canvas.GetComponentInParent<CanvasScaler>();scaler.referenceResolution=new Vector2(900,1600);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            music=gameObject.AddComponent<AudioSource>();music.loop=true;music.clip=lobby?presentation.lobbyMusic:presentation.ingameMusic;music.volume=.65f;music.Play();
            effects=gameObject.AddComponent<AudioSource>();
            gameObject.AddComponent<KitAudioChannel>().Initialize(music,KitAudioChannel.Category.Music);
            gameObject.AddComponent<KitAudioChannel>().Initialize(effects,KitAudioChannel.Category.Effects);
            screen=Mount(lobby?presentation.lobby:presentation.ingame);
            if(lobby)SetupLobby();else SetupGame();
            SetupSaveNotice();
            app.Changed+=Refresh;
            Refresh();
        }

        private GameObject Mount(GameObject prefab)
        {
            var go=Instantiate(prefab,canvas,false);go.SetActive(true);
            var rect=go.GetComponent<RectTransform>();
            if(rect!=null) {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}
            foreach(var graphic in go.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            foreach(var button in go.GetComponentsInChildren<Button>(true))
            {
                button.onClick=new Button.ButtonClickedEvent();
                if(button.targetGraphic!=null)button.targetGraphic.raycastTarget=true;
            }
            return go;
        }
        private static VisualBindings Role(GameObject go,string role)=>go.GetComponentsInChildren<VisualBindings>(true).FirstOrDefault(b=>b.role==role);
        private static Transform Named(GameObject go,string name)=>go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        private static void Active(GameObject go,string name,bool active) {var t=Named(go,name);if(t!=null)t.gameObject.SetActive(active);}
        private static void SetActive(VisualBindings binding,string key,bool active) {var go=binding?.Get<GameObject>(key);if(go!=null)go.SetActive(active);}
        private static void Text(VisualBindings binding,string key,string value) {var text=binding?.Get<TMP_Text>(key);if(text!=null)text.text=value;}
        private void Bind(VisualBindings binding,string key,Action action)
        {
            var button=binding?.Get<Button>(key);if(button==null)return;
            button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{if(app.IsLoading || (stagePath!=null && stagePath.IsAdvancing))return;if(presentation.startSound!=null)effects.PlayOneShot(presentation.startSound,.5f);action();});
            button.interactable=true;if(button.targetGraphic!=null)button.targetGraphic.raycastTarget=true;
        }
        private void SetupLobby()
        {
            // Keep authored objects and RectTransforms; only replace their service bindings.
            foreach(var scroll in screen.GetComponentsInChildren<ScrollRect>(true))
            {
                scroll.enabled=false;
                if(scroll.name=="svl_lobby")scroll.horizontalNormalizedPosition=.5f;
            }
            var stage=Role(screen,"StageInfo");
            stagePath=gameObject.AddComponent<StagePathView>();stagePath.Initialize(stage);
            for(int i=0;i<4;i++)
            {
                int slot=i;
                Bind(stage,"buttons."+i+".button",()=>OpenPrePlay(app.UnlockedLevel));
                var button=stage?.Get<Button>("buttons."+i+".button");if(button!=null)button.gameObject.SetActive(slot==0);
                Text(stage,"buttons."+i+".buttonText","Play");
            }
            Active(screen,"UI_LevelComplete",false);Active(screen,"UI_NextLevel",false);Active(screen,"IMG_ComingSoonBox",false);
            hud=Mount(presentation.hud);
            foreach(var binding in hud.GetComponentsInChildren<VisualBindings>(true))
            {
                if(binding.role=="UIHUDSub_Currency")
                {
                    Text(binding,"currencyCountText",binding.name.ToLowerInvariant().Contains("heart")?"":"0");
                    Text(binding,"fullText","Full");SetActive(binding,"infinityRoot",false);SetActive(binding,"currencyCountRoot",true);
                }
                foreach(var entry in binding.entries)
                {
                    string key=entry.key.ToLowerInvariant();
                    if(key.Contains("setting") || key.Contains("option"))Bind(binding,entry.key,OpenSettings);
                    if(key.Contains("currency") && entry.value is TMP_Text amount)amount.text=key.Contains("heart")?"":"0";
                }
            }
            foreach(var button in hud.GetComponentsInChildren<Button>(true))
                if(button.name.IndexOf("setting",StringComparison.OrdinalIgnoreCase)>=0 || button.name.IndexOf("option",StringComparison.OrdinalIgnoreCase)>=0)button.onClick.AddListener(OpenSettings);
            foreach(var top in hud.GetComponentsInChildren<VisualBindings>(true).Where(b=>b.role=="UIHUDSub_Top"))
            {
                var heart=top.Get<VisualBindings>("heart");
                heartHud=heart;UpdateHeartHud();
                StartCoroutine(HeartTicker());
                Bind(heart,"button",()=>app.Messages.ShowAsync("Hearts",app.HeartsFull?"Hearts are full.":"Next heart in "+KitApp.FormatTimer(app.TimeToNextHeart)+"\nStages 1-"+BounceEntry.FreeUntilStage+" are free.","OK").Forget());
            }
            var navigation=gameObject.AddComponent<LobbyNavigation>();navigation.Initialize(screen,Role(hud,"UIHUDPanel"));
            gameObject.AddComponent<BoosterShopView>().Initialize(screen,presentation,ShowInfo);
            var lobbyTop=Named(hud,"UI_Top");
            navigation.PageChanged+=page=>{if(lobbyTop!=null)lobbyTop.gameObject.SetActive(page!=0);};
            foreach(var top in hud.GetComponentsInChildren<VisualBindings>(true).Where(b=>b.role=="UIHUDSub_Top"))
                Bind(top.Get<VisualBindings>("gold"),"button",()=>navigation.Select(0));
            var profile=Named(hud,"btn_profile")?.GetComponent<Button>();
            if(profile!=null)
            {
                profile.onClick.RemoveAllListeners();profile.onClick.AddListener(OpenProfile);
                var hit=profile.GetComponent<Image>();if(hit==null)hit=profile.gameObject.AddComponent<Image>();
                hit.color=Color.clear;hit.raycastTarget=true;profile.targetGraphic=hit;
                var initials=new GameObject("Local profile initials",typeof(RectTransform),typeof(TextMeshProUGUI));
                initials.transform.SetParent(profile.transform,false);profileInitials=initials.GetComponent<TMP_Text>();
                profileInitials.font=presentation.settings.GetComponentInChildren<TMP_Text>(true).font;
                profileInitials.fontSize=36;profileInitials.alignment=TextAlignmentOptions.Center;profileInitials.raycastTarget=false;
                profileInitials.rectTransform.sizeDelta=new Vector2(100,80);
            }
            var offers=Named(screen,"UI_Right");
            if(offers!=null)
            {
                var side=(RectTransform)offers;
                side.anchoredPosition-=new Vector2(0,200); // Keep the authored offer column below the top HUD.
                offersView=gameObject.AddComponent<LobbyOffersView>();
                offersView.Initialize(screen,canvas,presentation,()=>popup!=null || dialog!=null || (stagePath!=null && stagePath.IsAdvancing),ShowInfo);
            }
            BuildDailyButton(navigation);
            Active(screen,"store_no_wifi",false);
            var pages=screen.GetComponentsInChildren<ScrollRect>(true).First(s=>s.name=="svl_lobby");
            var offlinePage=pages.content.GetChild(2);
            foreach(Transform child in offlinePage)child.gameObject.SetActive(false);
            var note=new GameObject("Offline content notice",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            note.transform.SetParent(offlinePage,false);note.rectTransform.sizeDelta=new Vector2(760,300);
            note.font=presentation.settings.GetComponentInChildren<TMP_Text>(true).font;note.fontSize=38;note.alignment=TextAlignmentOptions.Center;
            note.text="Offline kit\n\nOnline events are not connected.\nPlay stages from Home.";note.raycastTarget=false;
        }
        private void SetupGame()
        {
            ingameBindings=Role(screen,"UIIngamePanel");
            SetActive(ingameBindings,"RemainingMovesRoot",true);
            SetActive(ingameBindings,"LevelBadgeNormal",true);
            foreach(var key in new[]{"LevelBadgeHard","LevelBadgeSuperHard","LevelBadgeBonus","ModeBoxRoot","BoosterTutoRoot","AdLayoutRoot","WinStreakRevealRoot","BoosterRevealRoot"})SetActive(ingameBindings,key,false);
            Active(screen,"NonStagedPanel",false);Active(screen,"UI_CountInfo",true);
            foreach(var t in screen.GetComponentsInChildren<TMP_Text>(true))if(t.name=="TXT_Level")t.text="Level "+app.Session.Level;
            Bind(ingameBindings,"IngameSettingButton",OpenSettings);
            Bind(ingameBindings,"RestartButton",()=>app.Play(app.Session.Level));
            game=GetComponent<BounceModule>();game.CountersChanged+=UpdateCounts;game.Begin(app.Session.Level,app.Complete);
            foreach(var offer in BoosterCatalog.All)
            {
                var root=Named(screen,"BTN_"+offer.Kind).gameObject;
                var hit=root.GetComponent<Image>();if(hit==null)hit=root.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
                var button=root.GetComponent<Button>();if(button==null)button=root.AddComponent<Button>();button.targetGraphic=hit;
                foreach(var entry in ingameBindings.entries)if(entry.key==offer.ButtonKey)entry.value=button;
                Bind(ingameBindings,offer.ButtonKey,()=>game.TryUseBooster(offer.Kind));
            }
            UpdateCounts();
        }
        private void UpdateCounts()
        {
            Text(ingameBindings,"RemainingMovesText",game.BallsRemaining.ToString());RefreshBoosters();
        }
        private void RefreshBoosters()
        {
            if(ingameBindings==null || game==null)return;
            foreach(var offer in BoosterCatalog.All)
            {
                var button=ingameBindings.Get<Button>(offer.ButtonKey);if(button==null)continue;
                button.interactable=game.CanUseBooster(offer.Kind) && app.BoosterCount(offer.Kind)>0;
                foreach(var t in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    if(t.name.Trim()=="txt_itemnum"){t.gameObject.SetActive(true);t.text=app.BoosterCount(offer.Kind).ToString();}
                    if(t.text=="Free" || t.name.Trim()=="txt_itempricenum")t.gameObject.SetActive(false);
                }
                Active(button.gameObject,"UI_ItemPrice",false);Active(button.gameObject,"UI_ItemNum",true);
            }
        }
        private void Refresh()
        {
            RefreshBoosters();
            if(saveNotice!=null)saveNotice.SetActive(app.HasPendingSave);
            UpdateDailyBadge();
            if(profileInitials!=null)profileInitials.text=new string(app.PlayerName.Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();
            if(lobby && hud!=null)
                foreach(var top in hud.GetComponentsInChildren<VisualBindings>(true).Where(b=>b.role=="UIHUDSub_Top"))
                    Text(top.Get<VisualBindings>("gold"),"currencyCountText",app.DisplayedGold.ToString());
            if(lobby)UpdateHeartHud();
            if(!lobby && !resultShown && (app.Session.State==SessionState.Won || app.Session.State==SessionState.Lost))
            {
                resultShown=true;ShowResult(app.Session.State==SessionState.Won);
            }
        }
        private void ShowResult(bool won)
        {
            ClosePopup();screen.SetActive(false);popup=Mount(won?presentation.win:presentation.lose);
            if(won)
            {
                var binding=Role(popup,"StageClearPopup");
                foreach(var variant in new[]{"basic","hard","superHard","bonus"})
                {
                    SetActive(binding,variant+".root",variant=="basic");
                    SetActive(binding,variant+".twoButtonRoot",false);
                }
                SetActive(binding,"basic.claimButton",true);
                foreach(var key in new[]{"claimDoubleButton","bonusChestRoot","moveParticleRoot","winTowerBanner"})SetActive(binding,key,false);
                SetActive(binding,"titleText",false);Active(popup,"UI_Banner_WinTower",false);
                Text(binding,"levelText","Level "+app.Session.Level);
                Text(binding,"rewardAmountText",app.LastGoldReward.ToString());
                Text(binding,"goldAmountText",app.Gold.ToString());
            }
            else
            {
                Active(popup,"UI_Time_Over",false);Active(popup,"UI_Space",false);Active(popup,"UI_MovesZero",true);
            }
            if(won)
            {
                var binding=Role(popup,"StageClearPopup");
                var reward=popup.AddComponent<ClearRewardView>();
                reward.Initialize(binding,presentation,(int)Math.Min(int.MaxValue,app.Gold),app.LastGoldReward,()=>{app.ReleaseRewardDisplay();app.GoToLobby();});
                Bind(binding,"basic.claimButton",reward.Collect);Bind(binding,"closeButton",reward.Collect);
            }
            else
            {
                var binding=Role(popup,"UIFailPopup");
                SetActive(binding,"uiMovesZero",true);SetActive(binding,"uiTimeOver",false);
                var offer=app.ContinueOffer;
                Text(binding,"continuePriceText",offer.HasValue?offer.Value.Price.ToString():"-");SetActive(binding,"playOnLabels.0",offer.HasValue);
                Text(binding,"currencyText",app.Gold.ToString());
                var retry=binding.Get<Button>("replayButton");
                Active(retry.gameObject,"img_icon",false);Active(popup,"IMG_Ad",false);
                var label=binding.Get<TMP_Text>("continuePriceText");
                if(label!=null){var position=label.rectTransform.anchoredPosition;position.x=0;label.rectTransform.anchoredPosition=position;}
                foreach(var button in popup.GetComponentsInChildren<Button>(true))
                    if(button!=retry && button!=binding.Get<Button>("currencyButton"))button.onClick.AddListener(app.GoToLobby);
                Bind(binding,"replayButton",()=>
                {
                    if(app.TryContinue(game,out var why)){resultShown=false;ClosePopup();screen.SetActive(true);RefreshBoosters();}
                    else app.Messages.Toast(why,collapseDuplicate:true);
                });
                Bind(binding,"lobbyButton",app.GoToLobby);Bind(binding,"closeButton",app.GoToLobby);
            }
            foreach(var text in popup.GetComponentsInChildren<TMP_Text>(true))
                if(text.text.Contains("{0}"))text.text=text.text.Replace("{0}",app.LastGoldReward.ToString());
            StartCoroutine(Reveal(popup));
        }
        private IEnumerator Reveal(GameObject go)
        {
            var group=go.GetComponent<CanvasGroup>();if(group==null)group=go.AddComponent<CanvasGroup>();group.alpha=0;
            float elapsed=0;while(go!=null && elapsed<.2f) {elapsed+=Time.unscaledDeltaTime;group.alpha=Mathf.Clamp01(elapsed/.2f);yield return null;}
        }
        private void OpenSettings()
        {
            if(popup!=null || (offersView!=null && offersView.IsOpen))return;
            popup=Mount(lobby?presentation.settings:presentation.ingameSettings);
            var blocker=popup.GetComponent<Image>();if(blocker==null)blocker=popup.AddComponent<Image>();
            blocker.color=Color.clear;blocker.raycastTarget=true;
            Time.timeScale=lobby?1:0;RefreshBoosters();
            foreach(var button in popup.GetComponentsInChildren<Button>(true))
            {
                string name=button.name.ToLowerInvariant();button.onClick.RemoveAllListeners();
                if(name.Contains("home")||name.Contains("quit")||name.Contains("lobby"))button.onClick.AddListener(()=>{ClosePopup();app.GoToLobby();});
                else if(name.Contains("retry")||name.Contains("restart"))button.onClick.AddListener(()=>{ClosePopup();app.Play(app.Session.Level);});
                else if(name.Contains("sound")||name.Contains("music"))button.onClick.AddListener(app.ToggleSound);
                else if(name.Contains("close")||name.Contains("resume")||name.Contains("continue"))button.onClick.AddListener(ClosePopup);
            }
            var settingsBinding=Role(popup,lobby?"Popup_Settings":"UIIngameSettingPopup");
            BindSetting(settingsBinding,lobby?"toggleMusic":"musicToggle",app.MusicEnabled,app.SetMusic);
            BindSetting(settingsBinding,lobby?"toggleSound":"sfxToggle",app.EffectsEnabled,app.SetEffects);
            BindSetting(settingsBinding,lobby?"toggleViberate":"hapticToggle",app.HapticsEnabled,app.SetHaptics);
            Bind(settingsBinding,"btnClose",ClosePopup);
            Bind(settingsBinding,"CloseButton",ClosePopup);Bind(settingsBinding,"BackgroundButton",ClosePopup);
            Bind(settingsBinding,"gotoLobbyButton",()=>{ClosePopup();app.GoToLobby();});
            Bind(settingsBinding,"retryButton",()=>{ClosePopup();app.Play(app.Session.Level);});
            AddLicenseButton();
            if(!lobby)Text(settingsBinding,"txtRetry","Retry");
            if(lobby)
                foreach(var button in popup.GetComponentsInChildren<Button>(true))
                {
                    var name=button.name.ToLowerInvariant();
                    if(name.Contains("language"))SetInfo(button,"Language","This RND template currently uses English UI text.");
                    else if(name.Contains("privacy") || name.Contains("terms"))SetInfo(button,"Local data","Progress, inventory, sound options and player name are stored on this device.\nNo game account, analytics or cloud save service is connected.");
                    else if(name.Contains("update") || name.Contains("restore"))SetInfo(button,"BK_Kit","Offline RND development kit\nUnity 6000.3.21f1\nNo real-money purchases or online update service is connected.");
                }
        }
        private void AddLicenseButton()
        {
            var rect=KitUI.Box((RectTransform)popup.transform,"Font Licenses",new Vector2(330,72),Vector2.zero,new Color(.32f,.14f,.72f));
            rect.anchorMin=rect.anchorMax=lobby?new Vector2(.5f,0):new Vector2(.5f,.5f);
            rect.anchoredPosition=new Vector2(0,lobby?52:-435);
            var image=rect.GetComponent<Image>();
            var win=Role(presentation.win,"StageClearPopup");
            var skin=win.Get<Button>("basic.claimButton").targetGraphic as Image;
            if(skin!=null){image.sprite=skin.sprite;image.type=Image.Type.Sliced;image.color=Color.white;}
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            button.onClick.AddListener(()=>
            {
                if(dialog==null && !app.IsLoading)dialog=KitDialog.ShowLicenses(canvas,presentation);
            });
            var label=new GameObject("License label",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.transform.SetParent(rect,false);label.rectTransform.sizeDelta=new Vector2(310,60);
            var source=presentation.settings.GetComponentInChildren<TMP_Text>(true);
            label.font=source.font;label.fontSharedMaterial=source.fontSharedMaterial;label.fontSize=30;
            label.text="Font Licenses";label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        }
        private void SetInfo(Button button,string title,string message)
        {
            button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>ShowInfo(title,message));
            if(button.targetGraphic==null)
            {
                var hit=button.GetComponent<Image>();if(hit==null)hit=button.gameObject.AddComponent<Image>();
                hit.color=Color.clear;button.targetGraphic=hit;
            }
            button.targetGraphic.raycastTarget=true;
        }
        private void ShowInfo(string title,string message)
        {
            if(dialog!=null || app.IsLoading)return;
            dialog=KitDialog.Show(canvas,presentation,title,message);
        }
        private void OpenProfile()
        {
            if(dialog!=null || app.IsLoading)return;
            dialog=KitDialog.Show(canvas,presentation,"Local profile","Level "+app.UnlockedLevel+"\nGold: "+app.Gold+"\nSaved on this device",true);
        }
        // sf: the Play button opens the win-streak pre-play popup; the popup itself calls KitApp.Play.
        private bool opening; // the view is stacked only after its Addressables load; block a second tap meanwhile
        private bool PopupBlocked=>opening || popup!=null || dialog!=null || (offersView!=null && offersView.IsOpen) || app.UI.Peek(UILayer.Popup)!=null;
        private void OpenPrePlay(int level)
        {
            if(PopupBlocked)return;
            OpenPopup(app.UI.OpenAsync<WinStreakPopup,int>(WinStreakPopup.Address,level)).Forget();
        }
        private void OpenDaily()
        {
            if(app.IsLoading || PopupBlocked)return;
            OpenPopup(app.UI.OpenAsync<DailyRewardPopup>(DailyRewardPopup.Address)).Forget();
        }
        private async UniTask OpenPopup<T>(UniTask<T> open)
        {
            opening=true;
            try{await open;}finally{opening=false;}
        }
        private void UpdateDailyBadge()
        {
            if(dailyCount==null || app==null)return;
            int claimable=app.Daily.ClaimableCount;dailyCount.text=claimable.ToString();dailyDot.SetActive(claimable>0);
        }
        // The imported home panel has no daily entry (sf left it unassigned), so the button is built here and shown on the Home page only.
        private void BuildDailyButton(LobbyNavigation navigation)
        {
            var rect=KitUI.Box(canvas,"Daily button",new Vector2(190,84),Vector2.zero,Color.white);
            rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);rect.pivot=new Vector2(0,.5f);rect.anchoredPosition=new Vector2(20,140);
            var image=rect.GetComponent<Image>();
            var win=Role(presentation.win,"StageClearPopup");
            var skin=win.Get<Button>("basic.claimButton").targetGraphic as Image;
            if(skin!=null){image.sprite=skin.sprite;image.type=Image.Type.Sliced;}
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(OpenDaily);
            var source=presentation.settings.GetComponentInChildren<TMP_Text>(true);
            var label=new GameObject("Daily label",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.transform.SetParent(rect,false);label.rectTransform.sizeDelta=new Vector2(170,70);
            label.font=source.font;label.fontSharedMaterial=source.fontSharedMaterial;label.fontSize=30;
            label.text="Daily";label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
            var dot=KitUI.Box(rect,"Daily red dot",new Vector2(44,44),new Vector2(178,36),new Color(.9f,.15f,.15f));
            dot.GetComponent<Image>().raycastTarget=false;dailyDot=dot.gameObject;
            dailyCount=new GameObject("Daily count",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            dailyCount.transform.SetParent(dot,false);dailyCount.rectTransform.sizeDelta=new Vector2(44,44);
            dailyCount.font=source.font;dailyCount.fontSharedMaterial=source.fontSharedMaterial;dailyCount.fontSize=22;
            dailyCount.alignment=TextAlignmentOptions.Center;dailyCount.raycastTarget=false;
            navigation.PageChanged+=page=>rect.gameObject.SetActive(page==1);
        }
        private void SetupSaveNotice()
        {
            var rect=KitUI.Box(canvas,"Save pending",new Vector2(820,80),Vector2.zero,new Color(.6f,.08f,.08f,.98f));
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,55);
            var layer=rect.gameObject.AddComponent<Canvas>();layer.overrideSorting=true;layer.sortingOrder=29999;
            rect.gameObject.AddComponent<GraphicRaycaster>();
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(()=>app.RetryPendingSave());
            var label=new GameObject("Save message",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.transform.SetParent(rect,false);label.rectTransform.sizeDelta=new Vector2(780,70);
            label.font=presentation.settings.GetComponentInChildren<TMP_Text>(true).font;label.fontSize=26;
            label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
            label.text="Save pending - keep the game open. Tap to retry.";saveNotice=rect.gameObject;saveNotice.SetActive(false);
        }
        private void OnApplicationPause(bool paused)
        {
            if(paused && !lobby && app!=null && !app.IsLoading && !resultShown && popup==null && dialog==null)OpenSettings();
        }
        private static void BindSetting(VisualBindings binding,string key,bool value,Action<bool> changed)
        {
            var toggle=binding.Get<Toggle>(key);if(toggle==null)return;
            toggle.group=null;toggle.onValueChanged=new Toggle.ToggleEvent();toggle.SetIsOnWithoutNotify(value);
            foreach(var graphic in toggle.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=true;
            // The authored checkmark is a red mute slash, not an enabled-state check.
            var mute=toggle.transform.Find("Background/Checkmark");toggle.graphic=null;
            Action<bool> display=enabled=>
            {
                if(mute==null)return;
                mute.gameObject.SetActive(!enabled);
                var image=mute.GetComponent<Graphic>();
                if(image!=null){var color=image.color;color.a=1;image.color=color;}
            };
            display(value);
            toggle.onValueChanged.AddListener(enabled=>{display(enabled);changed(enabled);});
        }
        private void ClosePopup() {if(popup!=null)Destroy(popup);popup=null;Time.timeScale=1;RefreshBoosters();}
        private void UpdateHeartHud()
        {
            if(heartHud==null || app==null)return;
            bool full=app.HeartsFull;
            Text(heartHud,"currencyCountText",app.HeartsInfinite?"∞":app.Hearts.ToString());
            Text(heartHud,"fullText","Full");SetActive(heartHud,"fullText",full);
            SetActive(heartHud,"rechargeTimeText",!full);
            if(!full)Text(heartHud,"rechargeTimeText",KitApp.FormatTimer(app.TimeToNextHeart));
        }
        private IEnumerator HeartTicker()
        {
            var wait=new WaitForSecondsRealtime(.5f);
            while(heartHud!=null){UpdateHeartHud();UpdateDailyBadge();yield return wait;}
        }
        private void OnDestroy()
        {
            if(app!=null)app.Changed-=Refresh;
            if(game!=null)game.CountersChanged-=UpdateCounts;
            Time.timeScale=1;
        }
    }
}
