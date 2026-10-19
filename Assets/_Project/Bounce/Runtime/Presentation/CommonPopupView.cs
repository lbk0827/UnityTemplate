using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    /// <summary>Local behaviour for the visual-only grp1_common popup prefabs.</summary>
    public sealed class CommonPopupView : MonoBehaviour
    {
        private GamePresentation presentation;
        private float previousTimeScale;
        private Action afterClose;
        private bool closing, restored;
        private PopupTransition transition;
        private Transform content;
        private Sprite Skin(string name)=>presentation.refinedSprites?.FirstOrDefault(s=>s!=null && s.name==name);
        public bool IsClosing => closing;
        public static readonly Color[] ProfileColors={new Color(1,.76f,.22f),new Color(.7f,.42f,1),new Color(.2f,.8f,1)};
        public static CommonPopupView Show(RectTransform canvas,GamePresentation art,GameObject prefab,Action afterClose=null)
        {
            var root=Instantiate(prefab,canvas,false);root.SetActive(true);
            var rect=(RectTransform)root.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var view=root.AddComponent<CommonPopupView>();view.presentation=art;view.afterClose=afterClose;
            view.previousTimeScale=Time.timeScale;Time.timeScale=0;
            foreach(var group in root.GetComponentsInChildren<CanvasGroup>(true)){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}
            foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            foreach(var button in root.GetComponentsInChildren<Button>(true))
            {button.onClick=new Button.ButtonClickedEvent();button.interactable=true;if(button.targetGraphic!=null)button.targetGraphic.raycastTarget=true;button.onClick.AddListener(view.Close);}
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(t.name=="Root" || t.name=="Target")t.localScale=Vector3.one;
            view.transition=PopupTransition.Open(root);
            return view;
        }
        public VisualBindings Binding(string role)=>GetComponentsInChildren<VisualBindings>(true).FirstOrDefault(b=>b.role==role);
        public void Hide(string name)
        {foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name==name)t.gameObject.SetActive(false);}
        public void Text(string name,string value)
        {foreach(var t in GetComponentsInChildren<TMP_Text>(true))if(t.name==name)t.text=value;}
        public void Bind(Button button,Action action)
        {if(button==null)return;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{if(!closing)action();});}
        public void Close()=>CloseThen(null);
        public void CloseThen(Action action)
        {
            if(closing)return;closing=true;
            transition.Close(()=>
            {
                Restore();gameObject.SetActive(false);Destroy(gameObject);
                var next=action??afterClose;afterClose=null;next?.Invoke();
            });
        }
        private void Restore(){if(restored)return;restored=true;Time.timeScale=previousTimeScale;}
        private void OnDestroy()=>Restore();

        private Transform Find(string name)=>GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        private void UseNativeRoot()
        {
            content=transform.Find("Root")??transform.Find("UI_Continue")??transform;
            if(content!=transform)transition.SetContent(content);
        }
        private static void Center(RectTransform rect,Vector2 position,Vector2 size)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;}
        public void ConfigureConfirmation(bool retry,Action confirmed)
        {
            UseNativeRoot();
            var b=Binding(retry?"UIRetryPopup":"UIQuitPopup");
            foreach(string variant in new[]{"hard","superHard"}){var obj=b?.Get<GameObject>(variant+".root");if(obj!=null)obj.SetActive(false);}
            var basic=b?.Get<GameObject>("basic.root");if(basic!=null)basic.SetActive(true);
            if(!retry){content.localScale=Vector3.one*.70f;transition.SetContent(content);}
            Text("TXT_Title",retry?"Try again?":"Leave level?");
            string message=retry?(KitApp.Instance.Session.Level>BounceEntry.FreeUntilStage && !KitApp.Instance.HeartsInfinite?"Restarting uses 1 heart.":"Ready for another shot?"):"Your round will end.\nYour win streak will reset.";
            Text("TXT_Contents",message);Text("TXT_ItemExplain",message);Text("txt_retry","Restart");
            Hide("IMG_Ad");Hide("CurrencyPos");
            var button=b?.Get<Button>(retry?"retryButton":"basic.quitButton");
            if(button==null)button=b?.Get<Button>("basic.retryButton");
            Bind(button,()=>CloseThen(confirmed));
        }
        public void ConfigureHeart()
        {
            UseNativeRoot();
            var b=Binding("UIRefillPopup");
            var buy=b?.Get<Button>("purchaseButton");if(buy!=null)buy.gameObject.SetActive(false);
            var ad=Binding("HeartRechargeExtension")?.Get<Button>("_adButton");if(ad!=null)ad.gameObject.SetActive(false);
            Text("TXT_Title","Your lives");Text("TXT_Info","Time to next life");
            StartCoroutine(HeartTicker());
        }
        private IEnumerator HeartTicker()
        {
            while(!closing)
            {
                var app=KitApp.Instance;
                Text("TXT_HeartCount",app.HeartsInfinite?"\u221e":app.Hearts.ToString());
                Text("TXT_RemainTime",app.HeartsInfinite?"Unlimited":app.HeartsFull?"Full":KitApp.FormatTimer(app.TimeToNextHeart));
                Text("TXT_Info",app.HeartsInfinite?"Keep the good times rolling!":app.HeartsFull?"Ready for your next adventure!":"Time to next life");
                yield return new WaitForSecondsRealtime(.25f);
            }
        }
        public void ConfigureGuide(string title,string description,Sprite icon=null)
        {
            UseNativeRoot();
            if(Binding("UIBoosterUnlockPopup")!=null)
            {
                // Retain the original title, light burst and full-screen reveal. Replace the
                // source game's drawing tutorial with this game's booster illustration.
                Hide("UI_Info_02");Hide("UI_Info_03");Hide("UI_Info_Arrow_R");Hide("UI_Info_Arrow_L");
                var info=(RectTransform)Find("UI_Info_01");Center(info,new Vector2(0,125),new Vector2(660,650));
                var b=Binding("UIBoosterUnlockPopup");var hero=b.Get<Image>("infoImage01");hero.sprite=icon;hero.preserveAspect=true;
                Center(hero.rectTransform,new Vector2(0,80),new Vector2(320,320));
                var name=b.Get<TMP_Text>("infoLabel01");name.text=title;Center(name.rectTransform,new Vector2(0,-170),new Vector2(660,110));
                var glow=Find("IMG_Light") as RectTransform;if(glow!=null)Center(glow,new Vector2(0,80),new Vector2(470,470));
                Text("TXT_Title","Booster unlocked!");
                AddLabel("Guide description",description,new Vector2(0,-230),36,new Vector2(660,180));
            }
            else
            {
                Text("TXT_Title","New discovery!");Text("TXT_Desc_01",title);Text("TXT_Desc_02",description);Text("TXT_Info","Try it in this level");
                var heading=Find("TXT_Title").GetComponent<TMP_Text>();heading.enableAutoSizing=true;heading.fontSizeMax=88;heading.fontSizeMin=44;heading.rectTransform.sizeDelta=new Vector2(810,160);
                var hero=Find("IMG_Gimmick")?.GetComponent<Image>();
                if(hero!=null){hero.sprite=Skin("Guide_"+(title=="Cupboard"?"cupboard":title=="Moving panel"?"panel":"tube"));hero.preserveAspect=true;hero.rectTransform.sizeDelta=new Vector2(510,330);}
            }
        }
        public static Sprite HeartIcon(GamePresentation art)=>art.heart.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="IMG_Heart")?.sprite;
        private Sprite CoinIcon()=>presentation.shopSprites.FirstOrDefault(s=>s!=null && s.name=="IMG_Coin");
        public static Sprite BoosterIcon(GamePresentation art,BoosterKind kind)
        {
            var root=art.ingame.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="BTN_"+kind);
            return root==null?null:root.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="IMG_Item")?.sprite;
        }
        public void ConfigureUseBooster(BoosterOffer offer,Action use)
        {
            UseNativeRoot();
            var goods=(RectTransform)Find("store_goods_item");Center(goods,new Vector2(0,225),new Vector2(320,320));
            var icon=Find("IMG_Icon").GetComponent<Image>();icon.sprite=BoosterIcon(presentation,offer.Kind);icon.preserveAspect=true;
            Center(icon.rectTransform,Vector2.zero,new Vector2(260,260));Find("IMG_BackEff").gameObject.SetActive(true);
            var b=Binding("UIBoosterUsePopup");
            var name=b.Get<TMP_Text>("nameTexts.0");name.text=offer.Title;Center(name.rectTransform,new Vector2(0,15),new Vector2(650,110));
            // The source uses offset copies for a three-line item name. One line fits our labels.
            b.Get<TMP_Text>("nameTexts.1").gameObject.SetActive(false);b.Get<TMP_Text>("nameTexts.2").gameObject.SetActive(false);
            var explanation=b.Get<TMP_Text>("instructionText");explanation.text=offer.Description+"\nOwned: "+KitApp.Instance.BoosterCount(offer.Kind);
            Center(explanation.rectTransform,new Vector2(0,-130),new Vector2(650,160));
            AddAction("Use booster","Use 1",new Vector2(0,-310),()=>CloseThen(use));
            AddAction("Cancel booster","Not now",new Vector2(0,-490),Close);
        }
        public void ConfigureCelebration()
        {
            UseNativeRoot();
            // Keep the authored effects, but remove the source game's wordmark.
            Hide("IMG_Title");
            AddIcon(CoinIcon(),new Vector2(0,140),260);
            AddLabel("Celebration title","Great shot!",new Vector2(0,-100),86,new Vector2(800,150));
            AddLabel("Celebration hint","LEVEL COMPLETE",new Vector2(0,-240),34,new Vector2(650,70));
            foreach(var particles in GetComponentsInChildren<ParticleSystem>(true))
            {var main=particles.main;main.useUnscaledTime=true;if(particles.gameObject.activeInHierarchy)particles.Play();}
            StartCoroutine(AutoClose());
        }
        private IEnumerator AutoClose(){yield return new WaitForSecondsRealtime(1.25f);Close();}
        public void ConfigureReward(string message)
        {
            UseNativeRoot();
            var b=Binding("POPRewardView");b?.Get<GameObject>("hudTop")?.SetActive(false);
            Text("TXT_Title","Nice rewards!");
            var title=Find("TXT_Title").GetComponent<TMP_Text>();title.enableAutoSizing=true;title.fontSizeMax=88;title.fontSizeMin=44;
            var items=b?.Get<Transform>("itemRoot");if(items!=null){foreach(Transform child in items)child.gameObject.SetActive(false);}
            AddIcon(CoinIcon(),new Vector2(0,120),260);
            AddLabel("Reward summary",message,new Vector2(0,-160),48,new Vector2(680,220));
        }
        public void ConfigureProfile()
        {
            UseNativeRoot();
            var b=Binding("UIProfilePopup");
            var close=b.Get<Button>("closeButton");close.name="Popup close";
            var field=b.Get<TMP_InputField>("nickNameText");
            field.characterLimit=24;field.interactable=true;field.readOnly=false;field.text=KitApp.Instance.PlayerName;
            if(field.targetGraphic!=null)field.targetGraphic.raycastTarget=true;
            field.textComponent.raycastTarget=true;
            var edit=b.Get<Button>("changeNickNameButton");
            // The pencil focuses the native input. The explicit save action gives feedback.
            Bind(edit,()=>{field.Select();field.ActivateInputField();});
            var previous=content;content=Find("IMG_ContentsBox");
            var status=AddLabel("Profile details","Make it yours",new Vector2(0,-235),30,new Vector2(620,65));
            AddAction("Save profile","Save name",new Vector2(0,-340),()=>status.text=KitApp.Instance.TrySetPlayerName(field.text)?"Name saved!":"Enter 1-24 characters.");
            content=previous;
            SetupProfileChoices(b);
        }
        private void SetupProfileChoices(VisualBindings b)
        {
            var avatarRoot=b.Get<GameObject>("avatarRoot");var frameRoot=b.Get<GameObject>("frameRoot");
            var avatarToggle=b.Get<Toggle>("avatarToggle");var frameToggle=b.Get<Toggle>("frameToggle");
            var avatarPreview=Find("IMG_Avatar").GetComponent<Image>();var framePreview=Find("IMG_Frame").GetComponent<Image>();
            var choices=new Image[4];var checks=new GameObject[4];
            var avatarContent=b.Get<RectTransform>("avatarContent");var frameContent=b.Get<RectTransform>("frameContent");
            foreach(var root in new[]{avatarContent,frameContent})
            {
                foreach(var layout in root.GetComponents<LayoutGroup>())layout.enabled=false;
                var fitter=root.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
                foreach(Transform child in root)child.gameObject.SetActive(false);
                root.anchorMin=root.anchorMax=new Vector2(.5f,1);root.pivot=new Vector2(.5f,1);root.sizeDelta=new Vector2(640,350);root.anchoredPosition=Vector2.zero;
            }
            void Refresh()
            {
                avatarPreview.sprite=BoosterIcon(presentation,(BoosterKind)KitApp.Instance.AvatarIndex);avatarPreview.preserveAspect=true;
                framePreview.color=ProfileColors[KitApp.Instance.FrameIndex];
                for(int i=0;i<checks.Length;i++)if(checks[i]!=null)checks[i].SetActive(i==KitApp.Instance.AvatarIndex);
            }
            var previous=content;
            for(int i=0;i<4;i++)
            {
                int index=i;content=avatarContent;
                var plate=AddShape("Avatar "+i,new Vector2((i-1.5f)*152,-110),new Vector2(140,150),Skin("AvatarBack"));choices[i]=plate;
                var icon=AddShape("Avatar icon",Vector2.zero,new Vector2(108,108),BoosterIcon(presentation,(BoosterKind)i));icon.transform.SetParent(plate.transform,false);icon.preserveAspect=true;
                var check=AddShape("Avatar selected",Vector2.zero,new Vector2(144,158),Skin("AvatarCheck"));check.transform.SetParent(plate.transform,false);check.transform.SetAsFirstSibling();checks[i]=check.gameObject;
                var button=plate.gameObject.AddComponent<Button>();plate.raycastTarget=true;button.targetGraphic=plate;
                Bind(button,()=>{KitApp.Instance.SetProfileAppearance(index,KitApp.Instance.FrameIndex);Refresh();});
            }
            for(int i=0;i<3;i++)
            {
                int index=i;content=frameContent;
                var plate=AddShape("Frame "+i,new Vector2((i-1)*185,-110),new Vector2(160,170),framePreview.sprite);plate.color=ProfileColors[i];plate.raycastTarget=true;
                var button=plate.gameObject.AddComponent<Button>();button.targetGraphic=plate;
                Bind(button,()=>{KitApp.Instance.SetProfileAppearance(KitApp.Instance.AvatarIndex,index);Refresh();});
                AddLabel("Frame name",new[]{"Sunshine","Lavender","Sky"}[i],new Vector2((i-1)*185,-230),27,new Vector2(180,60));
            }
            content=previous;
            void Tab(bool avatars)
            {
                avatarRoot.SetActive(avatars);frameRoot.SetActive(!avatars);
                avatarToggle.SetIsOnWithoutNotify(avatars);frameToggle.SetIsOnWithoutNotify(!avatars);
                Find("IMG_Active_Avater").gameObject.SetActive(avatars);Find("IMG_Active_Frame").gameObject.SetActive(!avatars);
            }
            avatarToggle.group=frameToggle.group=null;avatarToggle.onValueChanged=new Toggle.ToggleEvent();frameToggle.onValueChanged=new Toggle.ToggleEvent();
            foreach(var toggle in new[]{avatarToggle,frameToggle})
            {if(toggle.targetGraphic!=null)toggle.targetGraphic.raycastTarget=true;toggle.transition=Selectable.Transition.None;toggle.graphic=null;}
            avatarToggle.onValueChanged.AddListener(v=>{if(v)Tab(true);});frameToggle.onValueChanged.AddListener(v=>{if(v)Tab(false);});
            Tab(true);Refresh();
        }
        private Image AddShape(string name,Vector2 position,Vector2 size,Sprite sprite)
        {
            var image=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();image.transform.SetParent(content??transform,false);
            image.rectTransform.sizeDelta=size;image.rectTransform.anchoredPosition=position;image.sprite=sprite;image.raycastTarget=false;
            image.type=sprite!=null && sprite.border!=Vector4.zero?Image.Type.Sliced:Image.Type.Simple;
            return image;
        }
        private void AddIcon(Sprite icon,Vector2 position,float size)
        {
            var light=AddShape("Reward glow",position,Vector2.one*(size*1.6f),Skin("Hero"));light.preserveAspect=true;
            var image=AddShape("Popup icon",position,Vector2.one*size,icon);image.preserveAspect=true;
        }
        private Button AddAction(string name,string title,Vector2 position,Action action)
        {
            var template=presentation.retry.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="UIRetryPopup").Get<Button>("retryButton");
            var button=Instantiate(template,content??transform,false);button.gameObject.SetActive(true);button.name=name;
            Center((RectTransform)button.transform,position,new Vector2(442,156));
            foreach(var image in button.GetComponentsInChildren<Image>(true))image.pixelsPerUnitMultiplier*=176f/156f;
            foreach(var sequence in button.GetComponentsInChildren<VisualSequence>(true))sequence.enabled=false;
            foreach(var graphic in button.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            foreach(var text in button.GetComponentsInChildren<TMP_Text>(true)){text.text=title;text.fontSize=50;text.enableAutoSizing=true;text.fontSizeMax=50;text.fontSizeMin=30;}
            if(name.StartsWith("Cancel"))
                foreach(var image in button.GetComponentsInChildren<Image>(true))if(image.sprite!=null && image.sprite.name=="BTN_CommonGreen")image.sprite=Skin("Secondary");
            if(button.targetGraphic!=null)button.targetGraphic.raycastTarget=true;
            button.onClick=new Button.ButtonClickedEvent();Bind(button,action);
            return button;
        }
        private TMP_Text AddLabel(string name,string value,Vector2 position,float size,Vector2 dimensions)
        {
            var text=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.transform.SetParent(content??transform,false);text.rectTransform.sizeDelta=dimensions;text.rectTransform.anchoredPosition=position;
            var source=presentation.settings.GetComponentsInChildren<TMP_Text>(true).First(t=>t.font!=null);
            text.font=source.font;text.fontSharedMaterial=source.fontSharedMaterial;text.color=Color.white;
            text.fontSize=size;text.enableAutoSizing=true;text.fontSizeMin=24;text.fontSizeMax=size;text.text=value;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            return text;
        }
    }
}
