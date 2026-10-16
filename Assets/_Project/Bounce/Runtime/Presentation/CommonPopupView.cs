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
        private static readonly Color Ink=new Color(.16f,.09f,.34f);
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

        public void ConfigureConfirmation(bool retry,Action confirmed)
        {
            var b=Binding(retry?"UIRetryPopup":"UIQuitPopup");
            foreach(string variant in new[]{"hard","superHard"}){var obj=b?.Get<GameObject>(variant+".root");if(obj!=null)obj.SetActive(false);}
            var basic=b?.Get<GameObject>("basic.root");if(basic!=null)basic.SetActive(true);
            Text("TXT_Title",retry?"Restart level?":"Leave level?");
            string message=retry?(KitApp.Instance.Session.Level>BounceEntry.FreeUntilStage && !KitApp.Instance.HeartsInfinite?"Restarting uses 1 heart.":"Start this level again?"):"Your current round will end.\nYour win streak will reset.";
            Text("TXT_Contents",message);Text("TXT_ItemExplain",message);
            Hide("IMG_Ad");Hide("CurrencyPos");
            var button=b?.Get<Button>(retry?"retryButton":"basic.quitButton");
            if(button==null)button=b?.Get<Button>("basic.retryButton");
            Bind(button,()=>CloseThen(confirmed));
            if(!retry)
            {
                BuildCard("Leave level?");AddIcon(CommonPopupView.HeartIcon(presentation),new Vector2(0,170),200);
                AddLabel("Exit explanation",message,new Vector2(0,-30),36);
                AddAction("Confirm quit","Leave",new Vector2(0,-240),()=>CloseThen(confirmed));
                AddAction("Cancel quit","Keep playing",new Vector2(0,-360),Close);
            }
            else foreach(var text in GetComponentsInChildren<TMP_Text>(true))if(text.name=="TXT_ItemExplain"){text.color=Ink;text.fontSharedMaterial=text.font.material;}
        }
        public void ConfigureHeart()
        {
            var b=Binding("UIRefillPopup");
            var buy=b?.Get<Button>("purchaseButton");if(buy!=null)buy.gameObject.SetActive(false);
            var ad=Binding("HeartRechargeExtension")?.Get<Button>("_adButton");if(ad!=null)ad.gameObject.SetActive(false);
            Text("TXT_Title","Hearts");Text("TXT_Info","Hearts recharge automatically.\nEarly levels are free to play.");
            Hide("txt_refill");Hide("txt_num");StartCoroutine(HeartTicker());
        }
        private IEnumerator HeartTicker()
        {
            while(!closing)
            {
                var app=KitApp.Instance;
                Text("TXT_HeartCount",app.HeartsInfinite?"∞":app.Hearts+" / "+BounceCurrencies.HeartMax);
                Text("TXT_RemainTime",app.HeartsInfinite?"Unlimited hearts":app.HeartsFull?"Full":"Next heart in "+KitApp.FormatTimer(app.TimeToNextHeart));
                yield return new WaitForSecondsRealtime(.25f);
            }
        }
        public void ConfigureGuide(string title,string description,Sprite icon=null)
        {
            BuildCard(title);
            if(icon!=null)AddIcon(icon,new Vector2(0,160),230);
            else AddGimmickDiagram(title);
            AddLabel("Guide description",description,new Vector2(0,-70),34);
            AddAction("Guide continue","Got it!",new Vector2(0,-300),Close);
        }
        private void AddGimmickDiagram(string title)
        {
            if(title=="Moving panel")
            {AddLabel("Panel arrows","<     >",new Vector2(0,180),70);AddShape("Moving panel",new Vector2(0,150),new Vector2(240,35),new Color(.2f,.7f,.9f));return;}
            int count=title=="Cupboard"?6:4;
            for(int i=0;i<count;i++)AddShape("Gimmick block "+i,new Vector2(title=="Cupboard"?(i%3-1)*90:(i-1.5f)*90,title=="Cupboard"?240-(i/3)*90:190),new Vector2(75,75),title=="Cupboard"?new Color(.95f,.65f,.3f):new Color(.4f,.72f,.96f));
            if(title!="Cupboard")AddLabel("Furnace label","Tube  >  Furnace",new Vector2(0,90),32).rectTransform.sizeDelta=new Vector2(650,65);
        }
        private Image AddShape(string name,Vector2 position,Vector2 size,Color color)
        {
            var image=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();image.transform.SetParent(content??transform,false);
            image.rectTransform.sizeDelta=size;image.rectTransform.anchoredPosition=position;image.color=color;image.raycastTarget=false;return image;
        }
        public static Sprite HeartIcon(GamePresentation art)=>art.heart.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="IMG_Heart")?.sprite;
        private Sprite CoinIcon()=>presentation.hud.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.sprite!=null && i.sprite.name=="IMG_Coin")?.sprite;
        private void AddIcon(Sprite icon,Vector2 position,float size)
        {var image=AddShape("Popup icon",position,Vector2.one*size,Color.white);image.sprite=icon;image.preserveAspect=true;}
        private void BuildCard(string title)
        {
            foreach(Transform child in transform)child.gameObject.SetActive(false);
            var dim=new GameObject("Popup dim",typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<Image>();dim.transform.SetParent(transform,false);
            dim.rectTransform.anchorMin=Vector2.zero;dim.rectTransform.anchorMax=Vector2.one;dim.rectTransform.offsetMin=dim.rectTransform.offsetMax=Vector2.zero;dim.color=new Color(.025f,.01f,.09f,.68f);
            var backdrop=dim.GetComponent<Button>();backdrop.targetGraphic=dim;Bind(backdrop,Close);
            var panel=new GameObject("Adapted content",typeof(RectTransform),typeof(Image)).GetComponent<Image>();panel.transform.SetParent(transform,false);content=panel.transform;
            panel.rectTransform.sizeDelta=new Vector2(780,940);
            panel.sprite=presentation.heart.GetComponentsInChildren<Image>(true).First(i=>i.name=="IMG_Popup").sprite;panel.type=Image.Type.Sliced;panel.raycastTarget=true;
            var header=AddShape("Popup header",new Vector2(0,410),new Vector2(660,150),Color.white);
            header.sprite=presentation.heart.GetComponentsInChildren<Image>(true).First(i=>i.name=="IMG_PopRibbon").sprite;header.type=Image.Type.Sliced;
            var label=AddLabel("Popup title",title,new Vector2(0,410),54);label.rectTransform.sizeDelta=new Vector2(590,110);label.color=new Color(1,1,.82f);
            var close=AddAction("Popup close","×",new Vector2(345,430),Close);close.GetComponent<RectTransform>().sizeDelta=new Vector2(80,80);content.Find("Popup close label").GetComponent<RectTransform>().sizeDelta=new Vector2(70,70);
            var closeArt=presentation.settings.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.sprite!=null && i.sprite.name=="Close");
            if(closeArt!=null){var img=(Image)close.targetGraphic;img.sprite=closeArt.sprite;img.type=Image.Type.Simple;img.preserveAspect=true;content.Find("Popup close label").gameObject.SetActive(false);}
            transition.SetContent(content);
        }
        public static Sprite BoosterIcon(GamePresentation art,BoosterKind kind)
        {
            var root=art.ingame.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="BTN_"+kind);
            return root==null?null:root.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="IMG_Item")?.sprite;
        }
        public void ConfigureUseBooster(BoosterOffer offer,Action use)
        {
            BuildCard(offer.Title);
            AddIcon(BoosterIcon(presentation,offer.Kind),new Vector2(0,180),220);
            AddLabel("Booster description",offer.Description+"\nOwned: "+KitApp.Instance.BoosterCount(offer.Kind),new Vector2(0,-50),36);
            AddAction("Use booster","Use 1",new Vector2(0,-240),()=>CloseThen(use));
            AddAction("Cancel booster","Cancel",new Vector2(0,-360),Close);
        }
        private Button AddAction(string name,string title,Vector2 position,Action action)
        {
            var source=presentation.win.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="StageClearPopup").Get<Button>("basic.claimButton");
            var image=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();image.transform.SetParent(transform,false);
            image.transform.SetParent(content??transform,false);image.rectTransform.sizeDelta=new Vector2(350,120);image.rectTransform.anchoredPosition=position;image.sprite=presentation.shopSprites.FirstOrDefault(s=>s.name=="BTN_CoinBuy")??(source.targetGraphic as Image)?.sprite;image.type=Image.Type.Sliced;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;Bind(button,action);
            var label=AddLabel(name+" label",title,position,34);label.rectTransform.sizeDelta=new Vector2(320,90);label.color=new Color(1,1,.82f);
            return button;
        }
        public void ConfigureCelebration()
        {
            BuildCard("Level complete!");
            AddIcon(CoinIcon(),new Vector2(0,160),250);
            AddLabel("Celebration title","Great shot!",new Vector2(0,-70),52);
            AddLabel("Celebration hint","Tap to continue",new Vector2(0,-270),30);
            var button=gameObject.GetComponent<Button>();if(button==null)button=gameObject.AddComponent<Button>();button.targetGraphic=GetComponent<Image>();Bind(button,Close);
            StartCoroutine(Confetti());
            StartCoroutine(AutoClose());
        }
        private IEnumerator Confetti()
        {
            var pieces=new RectTransform[24];
            for(int i=0;i<pieces.Length;i++){var image=AddShape("Confetti "+i,new Vector2((i%8-3.5f)*85,360+(i/8)*65),new Vector2(12,24),ProfileColors[i%3]);pieces[i]=image.rectTransform;}
            while(!closing){foreach(var piece in pieces){piece.anchoredPosition+=new Vector2(Mathf.Sin(Time.unscaledTime*3+piece.GetSiblingIndex())*45,-170)*Time.unscaledDeltaTime;piece.Rotate(0,0,130*Time.unscaledDeltaTime);}yield return null;}
        }
        private IEnumerator AutoClose(){yield return new WaitForSecondsRealtime(1.25f);Close();}
        public void ConfigureReward(string message)
        {
            BuildCard("Rewards collected");
            AddIcon(CoinIcon(),new Vector2(0,180),220);
            AddLabel("Reward summary",message,new Vector2(0,-30),36);AddAction("Collect done","Continue",new Vector2(0,-300),Close);
        }
        public void ConfigureProfile()
        {
            BuildCard("Profile");
            var box=AddShape("Player name",new Vector2(0,240),new Vector2(620,90),new Color(.91f,.86f,1));box.raycastTarget=true;
            var field=box.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=box;
            var name=AddLabel("Name text",KitApp.Instance.PlayerName,Vector2.zero,34);name.transform.SetParent(box.transform,false);name.rectTransform.sizeDelta=new Vector2(570,80);
            field.textViewport=name.rectTransform;field.textComponent=(TextMeshProUGUI)name;field.characterLimit=24;field.text=KitApp.Instance.PlayerName;
            var info=AddLabel("Profile details","Level "+KitApp.Instance.UnlockedLevel,new Vector2(0,330),28);info.rectTransform.sizeDelta=new Vector2(640,55);
            AddAction("Save profile","Save name",new Vector2(0,110),()=>info.text=KitApp.Instance.TrySetPlayerName(field.text)?"Name saved":"Enter 1–24 characters.");
            AddProfileChoices();
        }
        private void AddProfileChoices()
        {
            AddLabel("Avatar heading","Choose your icon",new Vector2(0,-40),30).rectTransform.sizeDelta=new Vector2(650,65);
            var choices=new Image[4];
            for(int i=0;i<4;i++)
            {
                int index=i;
                var image=new GameObject("Avatar "+i,typeof(RectTransform),typeof(Image)).GetComponent<Image>();image.transform.SetParent(content??transform,false);
                image.rectTransform.sizeDelta=new Vector2(110,110);image.rectTransform.anchoredPosition=new Vector2((i-1.5f)*145,-150);
                image.sprite=BoosterIcon(presentation,(BoosterKind)i);image.preserveAspect=true;
                var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;choices[i]=image;
                Bind(button,()=>{KitApp.Instance.SetProfileAppearance(index,KitApp.Instance.FrameIndex);RefreshProfileChoices(choices);});
            }
            for(int i=0;i<3;i++)
            {
                int index=i;var button=AddAction("Frame "+i,new[]{"Gold","Purple","Blue"}[i],new Vector2((i-1)*210,-310),()=>{KitApp.Instance.SetProfileAppearance(KitApp.Instance.AvatarIndex,index);RefreshProfileChoices(choices);});
                button.GetComponent<RectTransform>().sizeDelta=new Vector2(195,80);button.targetGraphic.color=ProfileColors[i];
                ((Image)button.targetGraphic).sprite=presentation.shopSprites.FirstOrDefault(s=>s.name=="Tile");
                var label=(content??transform).Find("Frame "+i+" label").GetComponent<RectTransform>();label.sizeDelta=new Vector2(180,70);
            }
            RefreshProfileChoices(choices);
        }
        private static void RefreshProfileChoices(Image[] choices)
        {for(int i=0;i<choices.Length;i++){var outline=choices[i].GetComponent<Outline>();if(outline==null)outline=choices[i].gameObject.AddComponent<Outline>();outline.effectColor=ProfileColors[KitApp.Instance.FrameIndex];outline.effectDistance=new Vector2(4,-4);outline.enabled=i==KitApp.Instance.AvatarIndex;}}
        private TMP_Text AddLabel(string name,string value,Vector2 position,float size)
        {
            var text=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.transform.SetParent(content??transform,false);text.rectTransform.sizeDelta=new Vector2(650,240);text.rectTransform.anchoredPosition=position;
            var source=presentation.settings.GetComponentInChildren<TMP_Text>(true);text.font=source.font;text.fontSharedMaterial=source.font.material;if(content!=null)text.color=Ink;
            text.fontSize=size;text.enableAutoSizing=true;text.fontSizeMin=24;text.fontSizeMax=size;text.text=value;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            return text;
        }
    }
}
