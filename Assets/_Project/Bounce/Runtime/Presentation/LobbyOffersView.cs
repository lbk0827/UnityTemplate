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
    // Only the icon of the week's active type is shown; the welcome deal is display-only until a store is connected.
    public sealed class LobbyOffersView : MonoBehaviour
    {
        public const string WelcomePrice="$4.99";
        public const int WelcomeBonusPercent=300;
        public static readonly ItemGrant[] WelcomeRewards=
        {
            BounceItems.Gold(3000),BounceItems.Booster(BoosterKind.Missile,3),BounceItems.Booster(BoosterKind.ExtraBall,3),
            BounceItems.Booster(BoosterKind.Bomb,2),BounceItems.Booster(BoosterKind.Laser,2)
        };

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
        private IDisposable progressSubscription;
        public GameObject Popup=>popup;
        public bool IsOpen=>popup!=null;
        public LobbyOfferIcon OpenKind=>openKind;

        public void Initialize(GameObject screen,RectTransform canvas,GamePresentation presentation,Func<bool> blocked,Action<string,string> showInfo)
        {
            app=KitApp.Instance;this.canvas=canvas;this.presentation=presentation;this.blocked=blocked;this.showInfo=showInfo;
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
                else button.onClick.AddListener(()=>showInfo("Offline kit","Ads and real-money offers are not enabled.\nUse Gold earned from stages in the Booster shop."));
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
            progressSubscription=app.Offers.ProgressChanged.Subscribe(_=>Refresh());
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
        private static StepOfferType TypeOf(LobbyOfferIcon kind)
            =>kind==LobbyOfferIcon.EndlessOffer?StepOfferType.Vertical:kind==LobbyOfferIcon.EndlessGift?StepOfferType.Chain:StepOfferType.None;
        private static LobbyOfferIcon IconOf(StepOfferType type)=>type==StepOfferType.Chain?LobbyOfferIcon.EndlessGift:LobbyOfferIcon.EndlessOffer;

        /// <summary>The week's live campaign, or null when locked, finished or outside the schedule.</summary>
        public StepOfferCampaign? Active=>app.Offers.GetActive();
        public bool HasClaimable(LobbyOfferIcon kind)
        {
            var campaign=Active;
            return campaign.HasValue && IconOf(campaign.Value.Definition.Type)==kind && campaign.Value.Current!=null && !campaign.Value.Current.IsPaid;
        }
        private string Remaining(in StepOfferCampaign campaign)=>WeekRotation.FormatRemaining(app.Offers.Remaining(campaign));

        private void Refresh()
        {
            var campaign=Active;
            foreach(var kind in new[]{LobbyOfferIcon.EndlessOffer,LobbyOfferIcon.EndlessGift})
            {
                bool live=campaign.HasValue && IconOf(campaign.Value.Definition.Type)==kind;
                if(icons.TryGetValue(kind,out var roots))foreach(var root in roots)if(root!=null)root.SetActive(live);
            }
            RefreshDots();
            RefreshTimers();
            if(popup!=null && app.IsLoading)Close();
            else if(popup!=null && openKind!=LobbyOfferIcon.WelcomeDeal)BindSteps();
        }
        private void RefreshDots()
        {
            foreach(var pair in redDots){bool active=HasClaimable(pair.Key);foreach(var dot in pair.Value)if(dot!=null)dot.SetActive(active);}
        }
        private void RefreshTimers()
        {
            var campaign=Active;
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
            feedback=Label(popup.transform,"Offer feedback","",new Vector2(820,50),new Vector2(0,-640),26);
            Bind(popupBinding?.Get<Button>("closeButton"),Close);
            Bind(popupBinding?.Get<Button>("dimButton"),Close);
            foreach(var button in popup.GetComponentsInChildren<Button>(true))
                if(button.name=="btn_common_black" || button.name=="BTN_Close")Bind(button,Close);
            if(kind==LobbyOfferIcon.WelcomeDeal)BindWelcomeDeal();else BindSteps();
        }
        public void Close()
        {
            if(popup!=null)Destroy(popup);popup=null;popupBinding=null;feedback=null;
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
            int count=0;while(popupBinding.Get<VisualBindings>("slots."+count)!=null)count++;return count;
        }
        // sf: past steps are not rendered; the window starts at the current step and the rest of the rail stays locked.
        private void BindSteps()
        {
            if(popupBinding==null)return;
            var active=Active;
            if(!active.HasValue || IconOf(active.Value.Definition.Type)!=openKind){Close();return;}
            var campaign=active.Value;
            int slotCount=SlotCount();
            var window=app.Offers.Window(campaign,slotCount);
            Text(popupBinding,"titleText",openKind==LobbyOfferIcon.EndlessGift?"Endless Gifts":"Endless Offer");
            Text(popupBinding,"remainTimeText",Remaining(campaign));
            for(int i=0;i<slotCount;i++)
            {
                var slot=popupBinding.Get<VisualBindings>("slots."+i);
                if(i>=window.Count){SetActive(slot,"root",false);continue;}
                var view=window[i];var step=view.Step;
                SetActive(slot,"root",true);SetActive(slot,"lockRoot",view.IsLocked);SetActive(slot,"currentRoot",view.IsCurrent);SetActive(slot,"checkRoot",false);
                Text(slot,"priceText",step.IsPaid?PriceOf(step.ProductId):"FREE");
                var buy=slot.Get<Button>("buyButton");
                if(buy!=null){int index=i;Bind(buy,()=>OnStep(index));buy.interactable=view.IsCurrent;}
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
            if(window[index].Step.IsPaid){showInfo("Purchase",KitApp.PaymentsNotConnected);return;}
            app.TryClaimStepOffer(campaign,out var message);
            if(feedback!=null)feedback.text=message;
            if(popup!=null)BindSteps();
        }
        private void BindRewards(VisualBindings slot,IReadOnlyList<ItemGrant> rewards)
        {
            var container=slot.Get<Transform>("rewardContainer");var template=slot.Get<GameObject>("rewardTemplate");
            if(container==null || template==null)return;
            template.SetActive(false);
            foreach(Transform child in container.Cast<Transform>().ToArray())if(child.name=="Reward clone")Destroy(child.gameObject);
            foreach(var reward in rewards)
            {
                var clone=Instantiate(template,container,false);clone.name="Reward clone";clone.SetActive(true);
                foreach(var graphic in clone.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                var icon=clone.GetComponentsInChildren<Image>(true).FirstOrDefault(image=>image.name=="IMG_Icon");
                if(icon!=null){var sprite=SpriteFor(reward,slot.Get<Sprite>("currencyIcons.0.sprite"));if(sprite!=null)icon.sprite=sprite;icon.preserveAspect=true;}
                var count=clone.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text=>text.name=="TXT_Count");
                if(count!=null)count.text=BounceItems.IsGold(reward)?reward.amount.ToString():"x"+reward.amount;
            }
        }
        private Sprite SpriteFor(in ItemGrant reward,Sprite gold)
        {
            if(BounceItems.IsGold(reward) || !BounceItems.TryGetBooster(reward,out var kind))return gold;
            if(boosterSprites.TryGetValue(kind,out var cached))return cached;
            var root=presentation.ingame.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="BTN_"+kind);
            var image=root==null?null:root.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="IMG_Item");
            return boosterSprites[kind]=image==null?gold:image.sprite;
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
                Sprite gold=null;
                foreach(var icon in container.GetComponentsInChildren<Image>(true))if(icon.sprite!=null){gold=icon.sprite;break;}
                foreach(var reward in WelcomeRewards)
                {
                    var item=Instantiate(itemPrefab,container,false);item.name="Reward clone";item.SetActive(true);
                    foreach(var graphic in item.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                    var binding=item.GetComponentsInChildren<VisualBindings>(true).FirstOrDefault(b=>b.role=="UIWelcomeDealRewardItem");
                    var icon=binding?.Get<Image>("icon");var sprite=SpriteFor(reward,gold);
                    if(icon!=null && sprite!=null){icon.sprite=sprite;icon.preserveAspect=true;}
                    Text(binding,"countText",BounceItems.IsGold(reward)?reward.amount.ToString():"x"+reward.amount);
                }
            }
            Bind(popupBinding.Get<Button>("buyButton"),()=>showInfo("Purchase",KitApp.PaymentsNotConnected));
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
