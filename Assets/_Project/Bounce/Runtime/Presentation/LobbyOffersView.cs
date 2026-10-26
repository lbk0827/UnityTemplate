using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BK.Meta;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    public enum LobbyOfferIcon { WelcomeDeal, EndlessOffer, EndlessGift }

    // Lobby floating offer icons (UI_Right) and the imported WelcomeDeal / EndlessOffer / EndlessGifts popups.
    // Step data and claiming go through BK.Meta.StepOffers (weekly rotation, sf rules); this class only binds authored prefabs.
    // Only the icon of the week's active type is shown; purchases go through BouncePurchases.
    public sealed class LobbyOffersView : MonoBehaviour
    {
        public const string WelcomePrice="$4.99";
        public const int WelcomeBonusPercent=300;
        public static readonly ItemGrant[] WelcomeRewards=BouncePurchases.WelcomeRewards;

        private KitApp app;
        private GamePresentation presentation;
        private RectTransform canvas;
        private Func<bool> blocked;
        private Action<string,string> showInfo;
        private readonly Dictionary<LobbyOfferIcon,List<GameObject>> icons=new Dictionary<LobbyOfferIcon,List<GameObject>>();
        private readonly Dictionary<LobbyOfferIcon,List<GameObject>> redDots=new Dictionary<LobbyOfferIcon,List<GameObject>>();
        private readonly Dictionary<LobbyOfferIcon,List<TMP_Text>> remainTexts=new Dictionary<LobbyOfferIcon,List<TMP_Text>>();
        private readonly Dictionary<BoosterKind,Sprite> boosterSprites=new Dictionary<BoosterKind,Sprite>();
        private GameObject popup;
        private VisualBindings popupBinding;
        private LobbyOfferIcon openKind;
        private TMP_Text feedback;
        private TMP_Text textStyle;
        private IDisposable progressSubscription;
        private StepOfferCampaign? current; // resolved at event points only; the timer never calls GetActive (it may start a new round)
        public GameObject Popup=>popup;
        public bool IsOpen=>popup!=null;
        public LobbyOfferIcon OpenKind=>openKind;

        public void Initialize(GameObject screen,RectTransform canvas,GamePresentation presentation,Func<bool> blocked,Action<string,string> showInfo)
        {
            app=KitApp.Instance;this.canvas=canvas;this.presentation=presentation;this.blocked=blocked;this.showInfo=showInfo;
            textStyle=presentation.settings.GetComponentsInChildren<TMP_Text>(true).First(t=>t.font!=null);
            var offers=screen.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="UI_Right");
            if(offers==null)return;
            foreach(var button in offers.GetComponentsInChildren<Button>(true))
            {
                var kind=KindOf(button.transform,offers);
                button.onClick.RemoveAllListeners();
                if(kind.HasValue)
                {
                    var value=kind.Value;button.onClick.AddListener(()=>Open(value));
                    Add(icons,value,IconRoot(button.transform,offers));
                }
                else button.onClick.AddListener(()=>showInfo("Offline kit","Ads and real-money offers are not enabled."));
                EnsureHit(button);
            }
            foreach(var marker in offers.GetComponentsInChildren<Transform>(true))
            {
                if(marker.name!="IMG_RedDot" && marker.name!="UI_RedDot")continue;
                var kind=KindOf(marker,offers);
                if(kind.HasValue)Add(redDots,kind.Value,marker.gameObject);else marker.gameObject.SetActive(false);
            }
            foreach(var text in offers.GetComponentsInChildren<TMP_Text>(true))
            {
                if(text.name!="TXT_Remain")continue;
                var kind=KindOf(text.transform,offers);
                if(kind.HasValue)Add(remainTexts,kind.Value,text);else text.text="Offline";
            }
            app.Changed+=Refresh;
            progressSubscription=app.Offers.ProgressChanged.Subscribe(_=>{Refresh();if(popup!=null && openKind!=LobbyOfferIcon.WelcomeDeal)BindSteps();});
            StartCoroutine(Tick());
            Refresh();
        }

        private static void Add<T>(Dictionary<LobbyOfferIcon,List<T>> map,LobbyOfferIcon kind,T value)
        {
            if(value==null)return;
            if(!map.TryGetValue(kind,out var list))map[kind]=list=new List<T>();
            if(!list.Contains(value))list.Add(value);
        }
        private static LobbyOfferIcon? KindOf(Transform target,Transform root)
        {
            for(var current=target;current!=null && current!=root;current=current.parent)
            {
                string name=current.name;
                // The authored icon is btn_common_lobbyicon named BTN_Welcome with Lobby_WelcomeDeal art nested inside.
                if(name.IndexOf("Welcome",StringComparison.OrdinalIgnoreCase)>=0)return LobbyOfferIcon.WelcomeDeal;
                if(name.IndexOf("EndlessGift",StringComparison.OrdinalIgnoreCase)>=0)return LobbyOfferIcon.EndlessGift;
                if(name.IndexOf("EndlessOffer",StringComparison.OrdinalIgnoreCase)>=0)return LobbyOfferIcon.EndlessOffer;
            }
            return null;
        }
        /// <summary>The authored icon root: the UI_Right child that contains the target.</summary>
        private static GameObject IconRoot(Transform target,Transform root)
        {
            for(var current=target;current!=null;current=current.parent)
                if(current.parent==root)return current.gameObject;
            return null;
        }
        private static void EnsureHit(Button button)
        {
            if(button.targetGraphic==null)
            {
                var hit=button.GetComponent<Image>();if(hit==null)hit=button.gameObject.AddComponent<Image>();
                hit.color=Color.clear;button.targetGraphic=hit;
            }
            button.targetGraphic.raycastTarget=true;button.interactable=true;
        }
        private static LobbyOfferIcon IconOf(StepOfferType type)=>type==StepOfferType.Chain?LobbyOfferIcon.EndlessGift:LobbyOfferIcon.EndlessOffer;

        /// <summary>Re-resolves the week's live campaign (null when locked, finished or outside the schedule).</summary>
        public StepOfferCampaign? Active=>current=app.Offers.GetActive();
        public bool HasClaimable(LobbyOfferIcon kind)
        {
            var campaign=current;
            return campaign.HasValue && IconOf(campaign.Value.Definition.Type)==kind && campaign.Value.Current!=null && !campaign.Value.Current.IsPaid;
        }
        private string Remaining(in StepOfferCampaign campaign)=>WeekRotation.FormatRemaining(app.Offers.Remaining(campaign));

        private void Refresh()
        {
            if(icons.TryGetValue(LobbyOfferIcon.WelcomeDeal,out var welcomeRoots))
                foreach(var root in welcomeRoots)if(root!=null)root.SetActive(!app.Purchases.WelcomeOwned);
            if(popup!=null && openKind==LobbyOfferIcon.WelcomeDeal && app.Purchases.WelcomeOwned)Close();
            var campaign=Active;
            foreach(var kind in new[]{LobbyOfferIcon.EndlessOffer,LobbyOfferIcon.EndlessGift})
            {
                bool live=campaign.HasValue && IconOf(campaign.Value.Definition.Type)==kind;
                if(icons.TryGetValue(kind,out var roots))foreach(var root in roots)if(root!=null)root.SetActive(live);
            }
            RefreshDots();
            RefreshTimers();
            if(popup!=null && app.IsLoading)Close();
        }
        private void RefreshDots()
        {
            foreach(var pair in redDots){bool active=HasClaimable(pair.Key);foreach(var dot in pair.Value)if(dot!=null)dot.SetActive(active);}
        }
        private void RefreshTimers()
        {
            var campaign=current;
            string remaining=campaign.HasValue?Remaining(campaign.Value):"";
            foreach(var pair in remainTexts)
            {
                string text=pair.Key==LobbyOfferIcon.WelcomeDeal?"No limit":remaining;
                foreach(var label in pair.Value)if(label!=null)label.text=text;
            }
            if(popup!=null && openKind!=LobbyOfferIcon.WelcomeDeal && campaign.HasValue)Text(popupBinding,"remainTimeText",remaining);
        }
        private IEnumerator Tick()
        {
            var wait=new WaitForSecondsRealtime(.5f);
            while(true){yield return wait;if(app!=null)RefreshTimers();}
        }

        public void Open(LobbyOfferIcon kind)
        {
            if(popup!=null || app.IsLoading || (blocked!=null && blocked()))return;
            if(kind!=LobbyOfferIcon.WelcomeDeal)
            {
                var campaign=Active;
                if(!campaign.HasValue || IconOf(campaign.Value.Definition.Type)!=kind)return;
            }
            var prefab=kind==LobbyOfferIcon.WelcomeDeal?presentation.welcomeDeal:kind==LobbyOfferIcon.EndlessGift?presentation.endlessGifts:presentation.endlessOffer;
            if(prefab==null){showInfo("Offer","The popup prefab is missing. Run BK > Integration > Ensure Offer Popups once.");return;}
            popup=Instantiate(prefab,canvas,false);popup.name="Offer "+kind;popup.SetActive(true);openKind=kind;
            var rect=popup.GetComponent<RectTransform>();
            if(rect!=null){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}
            foreach(var group in popup.GetComponentsInChildren<CanvasGroup>(true)){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}
            foreach(var graphic in popup.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            foreach(var button in popup.GetComponentsInChildren<Button>(true)){button.onClick=new Button.ButtonClickedEvent();if(button.targetGraphic!=null)button.targetGraphic.raycastTarget=true;}
            var blocker=popup.GetComponent<Image>();if(blocker==null)blocker=popup.AddComponent<Image>();
            blocker.color=Color.clear;blocker.raycastTarget=true;
            string role=kind==LobbyOfferIcon.WelcomeDeal?"UIWelcomeDealPopup":kind==LobbyOfferIcon.EndlessGift?"UIEndlessGiftsPopup":"UIEndlessOfferPopup";
            popupBinding=popup.GetComponentsInChildren<VisualBindings>(true).FirstOrDefault(b=>b.role==role);
            // The authored "Target" node starts at scale 0 and the original HeightBasedPopupScaler (not imported) sized it
            // to the host: fit a 900 x 1950 reference by height, shrinking further only on narrow screens.
            float fit=Mathf.Min(canvas.rect.height/1950f,canvas.rect.width/900f);
            var anchor=popupBinding?.Get<VisualBindings>("slots.0")?.transform??popupBinding?.Get<Button>("closeButton")?.transform;
            for(var current=anchor;current!=null && current!=popup.transform;current=current.parent)
                if(current.localScale==Vector3.zero)current.localScale=Vector3.one*fit;
            if(kind==LobbyOfferIcon.WelcomeDeal)
            {
                var content=popup.transform.Find("Root");
                content.localScale=Vector3.one*Mathf.Min(canvas.rect.height/1720f,canvas.rect.width/960f);
            }
            feedback=Label(popup.transform,"Offer feedback","",new Vector2(820,50),new Vector2(0,-canvas.rect.height*.46f),26);
            Bind(popupBinding?.Get<Button>("closeButton"),Close);
            Bind(popupBinding?.Get<Button>("dimButton"),Close);
            foreach(var button in popup.GetComponentsInChildren<Button>(true))
                if(button.name=="btn_common_black" || button.name=="BTN_Close")Bind(button,Close);
            if(kind==LobbyOfferIcon.WelcomeDeal)BindWelcomeDeal();else BindSteps();
            PopupTransition.Open(popup);
        }
        public void Close()
        {
            if(popup!=null)
            {
                var target=popup;var transition=target.GetComponent<PopupTransition>();
                if(transition!=null){transition.Close(()=>{if(target!=null)Destroy(target);if(popup==target){popup=null;popupBinding=null;feedback=null;}RefreshDots();});return;}
                Destroy(target);
            }
            popup=null;popupBinding=null;feedback=null;
            RefreshDots();
        }
        private static void Bind(Button button,Action action)
        {
            if(button==null)return;
            button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>action());button.interactable=true;
            EnsureHit(button);
        }
        private static void Text(VisualBindings binding,string key,string value){var text=binding?.Get<TMP_Text>(key);if(text!=null)text.text=value;}
        private static void SetActive(VisualBindings binding,string key,bool active){var go=binding?.Get<GameObject>(key);if(go!=null)go.SetActive(active);}

        private int SlotCount()
        {
            // The source keeps one extra slot outside the viewport for its slide animation.
            // This view rebinds a moving window instead; only the three/six visible cards participate.
            return openKind==LobbyOfferIcon.EndlessGift?6:3;
        }
        // sf: past steps are not rendered; the window starts at the current step and the rest of the rail stays locked.
        private void BindSteps()
        {
            if(popupBinding==null)return;
            var active=Active;
            if(!active.HasValue || IconOf(active.Value.Definition.Type)!=openKind){Close();return;}
            var campaign=active.Value;
            int slotCount=SlotCount();
            for(int i=slotCount;popupBinding.Get<VisualBindings>("slots."+i)!=null;i++)
                popupBinding.Get<VisualBindings>("slots."+i).gameObject.SetActive(false);
            var window=app.Offers.Window(campaign,slotCount);
            Text(popupBinding,"titleText",openKind==LobbyOfferIcon.EndlessGift?"Endless Gifts":"Endless Offer");
            if(window.Count==1)
            {
                Text(popupBinding,"titleText",openKind==LobbyOfferIcon.EndlessGift?"Final Gift!":"Final Offer!");
                var last=(RectTransform)popupBinding.Get<VisualBindings>("slots.0").transform;
                var position=last.anchoredPosition;position.x=0;last.anchoredPosition=position;
                var instructions=popup.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="Offer instructions");
                if(instructions!=null)instructions.text="One last step to complete the collection";
            }
            Text(popupBinding,"remainTimeText",Remaining(campaign));
            for(int i=0;i<slotCount;i++)
            {
                var slot=popupBinding.Get<VisualBindings>("slots."+i);
                if(i>=window.Count){SetActive(slot,"root",false);continue;}
                var view=window[i];var step=view.Step;
                SetActive(slot,"root",true);SetActive(slot,"lockRoot",view.IsLocked);SetActive(slot,"currentRoot",view.IsCurrent);SetActive(slot,"checkRoot",false);
                var currentFrame=slot.Get<GameObject>("currentRoot").GetComponent<Image>();
                if(currentFrame!=null){currentFrame.enabled=true;currentFrame.color=Color.white;currentFrame.canvasRenderer.SetAlpha(1);}
                var productFrame=slot.transform.GetComponentsInChildren<Image>(true).FirstOrDefault(image=>image.name=="IMG_ProductBg");
                if(productFrame!=null)productFrame.gameObject.SetActive(!view.IsCurrent);
                Text(slot,"priceText",step.IsPaid?PriceOf(step.ProductId):"FREE");
                var buy=slot.Get<Button>("buyButton");
                if(buy!=null)
                {
                    int index=i;Bind(buy,()=>OnStep(index));buy.interactable=view.IsCurrent;
                    var image=buy.targetGraphic as Image;
                    if(image!=null)image.sprite=presentation.refinedSprites.First(s=>s.name==(view.IsCurrent?"Primary":"Secondary"));
                }
                BindRewards(slot,RewardsOf(step));
            }
            for(int i=0;;i++){var connector=popupBinding.Get<GameObject>("stepConnectors."+i);if(connector==null)break;connector.SetActive(i<window.Count-1);}
        }
        private string PriceOf(string productId)
            =>int.TryParse(productId,out var id) && app.Shop.Products.Contains(id)?ShopCatalog.Price(app.Shop.Products.Get(id)):"-";
        /// <summary>Paid steps show the linked shop product's contents; free steps their own grants.</summary>
        private IReadOnlyList<ItemGrant> RewardsOf(StepOfferStep step)
        {
            if(!step.IsPaid)return step.Rewards;
            if(!int.TryParse(step.ProductId,out var id) || !app.Shop.Products.Contains(id))return Array.Empty<ItemGrant>();
            return app.Shop.Products.Get(id).rewards.Select(r=>new ItemGrant(r.currencyId,r.amount)).ToList();
        }
        private void OnStep(int index)
        {
            var active=Active;if(!active.HasValue){Close();return;}
            var campaign=active.Value;
            var window=app.Offers.Window(campaign,SlotCount());
            if(index>=window.Count || !window[index].IsCurrent)return;
            if(window[index].Step.IsPaid){app.BuyStep(campaign);return;}
            bool claimed=app.TryClaimStepOffer(campaign,out var message); // a successful claim rebinds through ProgressChanged
            if(feedback!=null)feedback.text=claimed?"":message;
            if(claimed)showInfo("Rewards",message);
        }
        private void BindRewards(VisualBindings slot,IReadOnlyList<ItemGrant> rewards)
        {
            var container=slot.Get<Transform>("rewardContainer");var template=slot.Get<GameObject>("rewardTemplate");
            if(container==null || template==null)return;
            template.SetActive(false);
            foreach(Transform child in container.Cast<Transform>().ToArray())if(child.name=="Reward clone"){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            DisableLayout(container);
            float available=openKind==LobbyOfferIcon.EndlessGift?350:720;
            float itemWidth=Mathf.Min(openKind==LobbyOfferIcon.EndlessGift?130:180,available/Mathf.Max(1,rewards.Count));
            Center((RectTransform)container,new Vector2(0,63),new Vector2(available,175));
            for(int i=0;i<rewards.Count;i++)
            {
                var reward=rewards[i];
                var clone=Instantiate(template,container,false);clone.name="Reward clone";clone.SetActive(true);
                Center((RectTransform)clone.transform,new Vector2((i-(rewards.Count-1)*.5f)*itemWidth,0),new Vector2(itemWidth,175));
                foreach(var graphic in clone.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                var icon=clone.GetComponentsInChildren<Image>(true).FirstOrDefault(image=>image.name=="IMG_Icon");
                if(icon!=null){icon.sprite=SpriteFor(reward);icon.preserveAspect=true;Center(icon.rectTransform,new Vector2(0,20),Vector2.one*Mathf.Min(132,itemWidth-8));}
                var count=clone.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text=>text.name=="TXT_Count");
                if(count!=null){count.text=AmountFor(reward);Center(count.rectTransform,new Vector2(0,-57),new Vector2(itemWidth,50));count.enableAutoSizing=true;count.fontSizeMin=24;count.fontSizeMax=40;}
            }
        }
        private Sprite SpriteFor(in ItemGrant reward)
        {
            string id=reward.itemId;
            if(BounceItems.IsGold(reward))return presentation.shopSprites.First(s=>s.name=="Goods_Coin_02");
            var row=app.Shop.Currencies.Contains(id)?app.Shop.Currencies.Get(id):null;
            if(!BounceItems.TryGetBooster(reward,out var kind) && (row==null || !Enum.TryParse(row.boosterKind,out kind)))
                return presentation.shopSprites.FirstOrDefault(s=>s.name==(id==BounceCurrencies.InfiniteHeart?"IMG_HeartInfinite":row?.icon));
            if(boosterSprites.TryGetValue(kind,out var cached))return cached;
            return boosterSprites[kind]=CommonPopupView.BoosterIcon(presentation,kind);
        }
        private string AmountFor(in ItemGrant reward)
        {
            if(app.Shop.Currencies.Contains(reward.itemId) && app.Shop.Currencies.Get(reward.itemId).duration)
                return reward.amount%3600==0?reward.amount/3600+"h":reward.amount/60+"m";
            return BounceItems.IsGold(reward)?reward.amount.ToString("N0",System.Globalization.CultureInfo.InvariantCulture):"x"+reward.amount;
        }
        private void BindWelcomeDeal()
        {
            if(popupBinding==null)return;
            Text(popupBinding,"titleText","Welcome Deal");Text(popupBinding,"priceText",WelcomePrice);Text(popupBinding,"percentText",WelcomeBonusPercent+"%");
            SetActive(popupBinding,"discountMarkRoot",WelcomeBonusPercent>0);
            var container=popupBinding.Get<Transform>("rewardContainer");var itemPrefab=popupBinding.Get<GameObject>("rewardItemPrefab");
            if(container!=null && itemPrefab!=null)
            {
                foreach(Transform child in container)child.gameObject.SetActive(false);
                DisableLayout(container);
                for(int i=0;i<WelcomeRewards.Length;i++)
                {
                    var reward=WelcomeRewards[i];
                    var item=Instantiate(itemPrefab,container,false);item.name="Reward clone";item.SetActive(true);
                    // Gold leads on its own row; the four boosters share the second row.
                    Center((RectTransform)item.transform,i==0?new Vector2(0,75):new Vector2((i-2.5f)*148,-95),new Vector2(140,145));
                    foreach(var graphic in item.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                    var binding=item.GetComponentsInChildren<VisualBindings>(true).FirstOrDefault(b=>b.role=="UIWelcomeDealRewardItem");
                    var icon=binding?.Get<Image>("icon");var sprite=SpriteFor(reward);
                    if(icon!=null && sprite!=null){icon.sprite=sprite;icon.preserveAspect=true;Center(icon.rectTransform,new Vector2(0,18),new Vector2(122,122));}
                    var count=binding?.Get<TMP_Text>("countText");
                    if(count!=null){count.text=AmountFor(reward);count.font=textStyle.font;count.fontSharedMaterial=textStyle.fontSharedMaterial;count.color=new Color(1,1,.8f);count.enableVertexGradient=false;Center(count.rectTransform,new Vector2(0,-60),new Vector2(140,55));count.enableAutoSizing=true;count.fontSizeMin=28;count.fontSizeMax=40;}
                }
            }
            Bind(popupBinding.Get<Button>("buyButton"),app.BuyWelcome);
        }
        private static void Center(RectTransform rect,Vector2 position,Vector2 size)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;}
        private static void DisableLayout(Transform target)
        {
            foreach(var layout in target.GetComponents<LayoutGroup>())layout.enabled=false;
            foreach(var fitter in target.GetComponents<ContentSizeFitter>())fitter.enabled=false;
        }
        private TMP_Text Label(Transform parent,string name,string text,Vector2 size,Vector2 position,float fontSize)
        {
            var sourceText=presentation.settings.GetComponentInChildren<TMP_Text>(true);
            var label=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent,false);label.rectTransform.sizeDelta=size;label.rectTransform.anchoredPosition=position;
            label.font=sourceText.font;label.fontSharedMaterial=sourceText.fontSharedMaterial;label.fontSize=fontSize;
            label.alignment=TextAlignmentOptions.Center;label.text=text;label.raycastTarget=false;return label;
        }
        private void OnDestroy(){if(app!=null)app.Changed-=Refresh;progressSubscription?.Dispose();}
    }
}
