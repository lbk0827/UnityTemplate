using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    // Lobby floating offer icons (UI_Right) and the imported WelcomeDeal / EndlessOffer / EndlessGifts popups.
    // Data and claiming go through IOfferSource; this class only binds authored prefabs.
    public sealed class LobbyOffersView : MonoBehaviour
    {
        private KitApp app;
        private IOfferSource source;
        private GamePresentation presentation;
        private RectTransform canvas;
        private Func<bool> blocked;
        private Action<string,string> showInfo;
        private readonly Dictionary<OfferKind,List<GameObject>> redDots=new Dictionary<OfferKind,List<GameObject>>();
        private readonly Dictionary<BoosterKind,Sprite> boosterSprites=new Dictionary<BoosterKind,Sprite>();
        private GameObject popup;
        private VisualBindings popupBinding;
        private OfferKind openKind;
        private TMP_Text feedback;
        private Vector2? railHome;
        public GameObject Popup=>popup;
        public bool IsOpen=>popup!=null;
        public OfferKind OpenKind=>openKind;
        public IOfferSource Source=>source;

        public void Initialize(GameObject screen,RectTransform canvas,GamePresentation presentation,Func<bool> blocked,Action<string,string> showInfo,IOfferSource source=null)
        {
            app=KitApp.Instance;this.canvas=canvas;this.presentation=presentation;this.blocked=blocked;this.showInfo=showInfo;
            this.source=source??new LocalOfferSource(app);
            var offers=screen.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="UI_Right");
            if(offers==null)return;
            foreach(var button in offers.GetComponentsInChildren<Button>(true))
            {
                var kind=KindOf(button.transform,offers);
                button.onClick.RemoveAllListeners();
                if(kind.HasValue){var value=kind.Value;button.onClick.AddListener(()=>Open(value));}
                else button.onClick.AddListener(()=>showInfo("Offline kit","Ads and real-money offers are not enabled.\nUse Gold earned from stages in the Booster shop."));
                EnsureHit(button);
            }
            foreach(var marker in offers.GetComponentsInChildren<Transform>(true))
            {
                if(marker.name!="IMG_RedDot" && marker.name!="UI_RedDot")continue;
                var kind=KindOf(marker,offers);
                if(kind.HasValue){if(!redDots.TryGetValue(kind.Value,out var list))redDots[kind.Value]=list=new List<GameObject>();list.Add(marker.gameObject);}
                else marker.gameObject.SetActive(false);
            }
            foreach(var text in offers.GetComponentsInChildren<TMP_Text>(true))
                if(text.name=="TXT_Remain")text.text=KindOf(text.transform,offers).HasValue?"No limit":"Offline";
            app.Changed+=Refresh;Refresh();
        }

        private static OfferKind? KindOf(Transform target,Transform root)
        {
            for(var current=target;current!=null && current!=root;current=current.parent)
            {
                string name=current.name;
                // The authored icon is btn_common_lobbyicon named BTN_Welcome with Lobby_WelcomeDeal art nested inside.
                if(name.IndexOf("Welcome",StringComparison.OrdinalIgnoreCase)>=0)return OfferKind.WelcomeDeal;
                if(name.IndexOf("EndlessGift",StringComparison.OrdinalIgnoreCase)>=0)return OfferKind.EndlessGift;
                if(name.IndexOf("EndlessOffer",StringComparison.OrdinalIgnoreCase)>=0)return OfferKind.EndlessOffer;
            }
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
        public bool HasClaimable(OfferKind kind)
        {
            var steps=source.Steps(kind);int current=source.CurrentStep(kind);
            return current<steps.Count && !steps[current].Paid;
        }
        private void Refresh()
        {
            foreach(var pair in redDots){bool active=HasClaimable(pair.Key);foreach(var dot in pair.Value)if(dot!=null)dot.SetActive(active);}
            if(popup!=null && app.IsLoading)Close();
            else if(popup!=null && openKind!=OfferKind.WelcomeDeal)BindSteps();
        }

        public void Open(OfferKind kind)
        {
            if(popup!=null || app.IsLoading || (blocked!=null && blocked()))return;
            var prefab=kind==OfferKind.WelcomeDeal?presentation.welcomeDeal:kind==OfferKind.EndlessGift?presentation.endlessGifts:presentation.endlessOffer;
            if(prefab==null){showInfo("Offer","The popup prefab is missing. Run BK Kit > Open Migrated Bootstrap once.");return;}
            popup=Instantiate(prefab,canvas,false);popup.name="Offer "+kind;popup.SetActive(true);openKind=kind;
            var rect=popup.GetComponent<RectTransform>();
            if(rect!=null){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}
            foreach(var graphic in popup.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            foreach(var button in popup.GetComponentsInChildren<Button>(true)){button.onClick=new Button.ButtonClickedEvent();if(button.targetGraphic!=null)button.targetGraphic.raycastTarget=true;}
            var blocker=popup.GetComponent<Image>();if(blocker==null)blocker=popup.AddComponent<Image>();
            blocker.color=Color.clear;blocker.raycastTarget=true;
            string role=kind==OfferKind.WelcomeDeal?"UIWelcomeDealPopup":kind==OfferKind.EndlessGift?"UIEndlessGiftsPopup":"UIEndlessOfferPopup";
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
            if(kind==OfferKind.WelcomeDeal)BindWelcomeDeal();else BindSteps();
        }
        public void Close()
        {
            if(popup!=null)Destroy(popup);popup=null;popupBinding=null;feedback=null;railHome=null;
            foreach(var pair in redDots){bool active=HasClaimable(pair.Key);foreach(var dot in pair.Value)if(dot!=null)dot.SetActive(active);}
        }
        private static void Bind(Button button,Action action)
        {
            if(button==null)return;
            button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>action());button.interactable=true;
            EnsureHit(button);
        }
        private static void Text(VisualBindings binding,string key,string value){var text=binding?.Get<TMP_Text>(key);if(text!=null)text.text=value;}
        private static void SetActive(VisualBindings binding,string key,bool active){var go=binding?.Get<GameObject>(key);if(go!=null)go.SetActive(active);}

        private void BindSteps()
        {
            if(popupBinding==null)return;
            var kind=openKind;var steps=source.Steps(kind);int current=source.CurrentStep(kind);
            Text(popupBinding,"titleText",kind==OfferKind.EndlessGift?"Endless Gifts":"Endless Offer");
            Text(popupBinding,"remainTimeText","No limit");
            for(int i=0;;i++)
            {
                var slot=popupBinding.Get<VisualBindings>("slots."+i);if(slot==null)break;
                if(i>=steps.Count){SetActive(slot,"root",false);continue;}
                var step=steps[i];bool done=i<current,isCurrent=i==current;
                SetActive(slot,"root",true);SetActive(slot,"lockRoot",i>current);SetActive(slot,"currentRoot",isCurrent);SetActive(slot,"checkRoot",done);
                Text(slot,"priceText",done?"Collected":step.Paid?step.Price:"FREE");
                var buy=slot.Get<Button>("buyButton");
                if(buy!=null){int index=i;Bind(buy,()=>OnStep(index));buy.interactable=isCurrent;}
                BindRewards(slot,step.Rewards);
            }
            for(int i=0;;i++){var connector=popupBinding.Get<GameObject>("stepConnectors."+i);if(connector==null)break;connector.SetActive(true);}
            FocusSlot(popupBinding.Get<VisualBindings>("slots."+Mathf.Min(current,steps.Count-1)));
        }
        // The authored rail is taller than the screen; the original moved it with a tween so the current step stays visible.
        private void FocusSlot(VisualBindings slot)
        {
            if(slot==null)return;
            // Slots sit directly under the masked UI_Contents; moving that would move its clip region as well.
            // Gather the slots and the authored rail art into a runtime container inside the clip and move that instead.
            var clip=slot.transform.parent as RectTransform;
            if(clip==null || clip==popup.transform)return;
            var rail=clip.Find("Kit rail") as RectTransform;
            if(rail==null)
            {
                rail=new GameObject("Kit rail",typeof(RectTransform)).GetComponent<RectTransform>();
                rail.SetParent(clip,false);rail.anchorMin=Vector2.zero;rail.anchorMax=Vector2.one;rail.offsetMin=rail.offsetMax=Vector2.zero;
                rail.SetAsFirstSibling();
                var slotTransforms=new HashSet<Transform>();
                for(int i=0;;i++){var view=popupBinding.Get<VisualBindings>("slots."+i);if(view==null)break;slotTransforms.Add(view.transform);}
                foreach(var child in clip.Cast<Transform>().ToArray())
                    if(child.name=="UI_Rail" || slotTransforms.Contains(child))child.SetParent(rail,true);
            }
            if(clip.GetComponent<RectMask2D>()==null && clip.GetComponent<Mask>()==null)clip.gameObject.AddComponent<RectMask2D>();
            if(!railHome.HasValue)railHome=rail.anchoredPosition;
            rail.anchoredPosition=railHome.Value;
            Canvas.ForceUpdateCanvases();
            var slotRect=(RectTransform)slot.transform;
            float center=canvas.InverseTransformPoint(slotRect.TransformPoint(slotRect.rect.center)).y;
            float half=slotRect.rect.height*.5f*(slotRect.lossyScale.y/canvas.lossyScale.y);
            float bottom=-canvas.rect.height*.5f+40,top=canvas.rect.height*.5f-420;
            float shift=0;
            if(center-half<bottom)shift=bottom-(center-half);
            else if(center+half>top)shift=top-(center+half);
            if(Mathf.Abs(shift)>.5f)
            {
                rail.anchoredPosition=railHome.Value+new Vector2(0,shift*canvas.lossyScale.y/rail.lossyScale.y);
                // The authored mask bleeds 200 units upward for intro tweens; keep the moved rail art out of the title.
                var mask=clip.GetComponent<RectMask2D>();if(mask!=null)mask.padding=Vector4.zero;
            }
            // Like the original window logic, slots pushed outside the content area are switched off instead of drawn over the title.
            if(clip==null || clip==popup.transform)return;
            Canvas.ForceUpdateCanvases();
            for(int i=0;;i++)
            {
                var view=popupBinding.Get<VisualBindings>("slots."+i);if(view==null)break;
                var root=view.Get<GameObject>("root");if(root==null || !root.activeSelf)continue;
                var viewRect=(RectTransform)view.transform;
                Vector2 local=clip.InverseTransformPoint(viewRect.TransformPoint(viewRect.rect.center));
                if(!clip.rect.Contains(local))root.SetActive(false);
            }
        }
        private void OnStep(int index)
        {
            var steps=source.Steps(openKind);if(index>=steps.Count)return;
            string message;
            if(steps[index].Paid){source.TryPurchase(openKind,index,out message);showInfo("Purchase",message);return;}
            source.TryClaim(openKind,index,out message);
            if(feedback!=null)feedback.text=message;
            BindSteps();
        }
        private void BindRewards(VisualBindings slot,IReadOnlyList<OfferReward> rewards)
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
                if(count!=null)count.text=reward.IsGold?reward.Count.ToString():"x"+reward.Count;
            }
        }
        private Sprite SpriteFor(OfferReward reward,Sprite gold)
        {
            if(reward.IsGold || !reward.TryGetBooster(out var kind))return gold;
            if(boosterSprites.TryGetValue(kind,out var cached))return cached;
            var root=presentation.ingame.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="BTN_"+kind);
            var image=root==null?null:root.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="IMG_Item");
            return boosterSprites[kind]=image==null?gold:image.sprite;
        }
        private void BindWelcomeDeal()
        {
            if(popupBinding==null)return;
            var deal=source.WelcomeDeal;
            Text(popupBinding,"titleText",deal.Title);Text(popupBinding,"priceText",deal.Price);Text(popupBinding,"percentText",deal.BonusPercent+"%");
            SetActive(popupBinding,"discountMarkRoot",deal.BonusPercent>0);
            var container=popupBinding.Get<Transform>("rewardContainer");var itemPrefab=popupBinding.Get<GameObject>("rewardItemPrefab");
            if(container!=null && itemPrefab!=null)
            {
                foreach(Transform child in container)child.gameObject.SetActive(false);
                Sprite gold=null;
                foreach(var icon in container.GetComponentsInChildren<Image>(true))if(icon.sprite!=null){gold=icon.sprite;break;}
                foreach(var reward in deal.Rewards)
                {
                    var item=Instantiate(itemPrefab,container,false);item.name="Reward clone";item.SetActive(true);
                    foreach(var graphic in item.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                    var binding=item.GetComponentsInChildren<VisualBindings>(true).FirstOrDefault(b=>b.role=="UIWelcomeDealRewardItem");
                    var icon=binding?.Get<Image>("icon");var sprite=SpriteFor(reward,gold);
                    if(icon!=null && sprite!=null){icon.sprite=sprite;icon.preserveAspect=true;}
                    Text(binding,"countText",reward.IsGold?reward.Count.ToString():"x"+reward.Count);
                }
            }
            Bind(popupBinding.Get<Button>("buyButton"),()=>{source.TryPurchase(OfferKind.WelcomeDeal,0,out var message);showInfo("Purchase",message);});
        }
        private TMP_Text Label(Transform parent,string name,string text,Vector2 size,Vector2 position,float fontSize)
        {
            var sourceText=presentation.settings.GetComponentInChildren<TMP_Text>(true);
            var label=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent,false);label.rectTransform.sizeDelta=size;label.rectTransform.anchoredPosition=position;
            label.font=sourceText.font;label.fontSharedMaterial=sourceText.fontSharedMaterial;label.fontSize=fontSize;
            label.alignment=TextAlignmentOptions.Center;label.text=text;label.raycastTarget=false;return label;
        }
        private void OnDestroy(){if(app!=null)app.Changed-=Refresh;}
    }
}
