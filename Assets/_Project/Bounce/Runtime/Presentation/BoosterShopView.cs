using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    public sealed class BoosterShopView : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.19f,.09f,.36f);
        private readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        private KitApp app;
        private TMP_Text fontSource,goldBalance;
        private Material shopFontMaterial;
        private RectTransform content;
        private Action<string,string> showInfo;
        private float cursor;
        public ScrollRect Scroll { get; private set; }
        public RectTransform CoinsSection { get; private set; }

        public void Initialize(GameObject screen,GamePresentation presentation,Action<string,string> showInfo)
        {
            this.showInfo=showInfo;app=KitApp.Instance;
            foreach(var sprite in presentation.shopSprites)if(sprite!=null)sprites[sprite.name]=sprite;
            var store=screen.GetComponentsInChildren<Transform>(true).First(t=>t.name=="store");
            Scroll=store.GetComponentsInChildren<ScrollRect>(true).First();
            fontSource=store.GetComponentsInChildren<Transform>(true).First(t=>t.name=="IMG_SpecialBundleTitle").GetComponentInChildren<TMP_Text>(true);
            shopFontMaterial=new Material(fontSource.fontSharedMaterial);
            shopFontMaterial.SetFloat("_OutlineWidth",0);
            shopFontMaterial.SetColor("_FaceColor",Color.white);
            shopFontMaterial.DisableKeyword("UNDERLAY_ON");
            shopFontMaterial.DisableKeyword("UNDERLAY_INNER");
            content=Scroll.content;
            foreach(var group in content.GetComponents<LayoutGroup>())group.enabled=false;
            var fitter=content.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            foreach(Transform child in content)child.gameObject.SetActive(false);
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
            content.anchoredPosition=Vector2.zero;
            Scroll.horizontal=false;Scroll.vertical=true;Scroll.movementType=ScrollRect.MovementType.Elastic;
            // Keep the scrolling viewport between the fixed canopy and navigation on every aspect ratio.
            // Both sit inside the safe area like the HUD; only the canopy art bleeds up under the notch.
            var safeArea=Rect(transform,"Shop safe area",Vector2.zero,Vector2.zero);
            safeArea.anchorMin=Vector2.zero;safeArea.anchorMax=Vector2.one;
            SetupHeader(store,safeArea);
            var scrollRect=(RectTransform)Scroll.transform;scrollRect.SetParent(safeArea,false);
            scrollRect.anchorMin=Vector2.zero;scrollRect.anchorMax=Vector2.one;
            scrollRect.offsetMin=new Vector2(0,285);scrollRect.offsetMax=new Vector2(0,-300);
            Scroll.viewport.anchorMin=Vector2.zero;Scroll.viewport.anchorMax=Vector2.one;
            Scroll.viewport.offsetMin=Scroll.viewport.offsetMax=Vector2.zero;
            Scroll.gameObject.SetActive(false);
            GetComponent<LobbyNavigation>().PageChanged+=page=>Scroll.gameObject.SetActive(page==0);
            cursor=20;
            var boosterIcons=BoosterCatalog.All.Select(offer=>Sprite("Shop_"+offer.Kind)).ToArray();
            foreach(var category in app.Shop.VisibleProducts().GroupBy(p=>p.category))
            {
                bool coins=category.Key=="Coins";
                var heading=Section(category.Key,coins?"IMG_ShopTitle_Coin":category.Key=="No-Ad Offers"?"IMG_ShopTitle_NoAd":"IMG_ShopTitle_Package");
                if(coins)CoinsSection=heading;
                float top=cursor;int index=0;
                foreach(var product in category)
                {
                    if(product.layout=="Coin")
                    {
                        var reward=app.Shop.Rewards(product,0).First();
                        float x=(index%3-1)*287,y=top+index/3*390;
                        var card=Image(content,"Coins_"+reward.amount,new Vector2(274,370),new Vector2(x,-y-185),Sprite("IMG_Coin_Goods_Bg")).rectTransform;
                        // The existing card geometry also reserves the footer for the purchase action.
                        Icon(card,product.icon,new Vector2(200,160),new Vector2(0,78));
                        Label(card,app.Shop.Amount(reward),new Vector2(240,46),new Vector2(0,-30),40,Ink);
                        Buy(card,"Preview_Coins_"+reward.amount,ShopCatalog.Price(product),new Vector2(240,90),new Vector2(0,-114),38,
                            ()=>Preview(product));
                    }
                    else RenderPackage(product,boosterIcons);
                    index++;
                }
                if(coins)
                {
                    float height=Mathf.Ceil(index/3f)*390;cursor=top+height;
                    var background=Image(content,"Coin section background",new Vector2(940,height+180),new Vector2(0,-top-height/2+80),null);
                    background.color=new Color(.24f,.14f,.56f);background.transform.SetAsFirstSibling();
                }
                Gap(30);
            }
            content.sizeDelta=new Vector2(0,cursor+30);
            app.Changed+=Refresh;Refresh();StartCoroutine(ScrollToTop());
        }
        private void SetupHeader(Transform store,RectTransform safeArea)
        {
            foreach(var child in store.GetComponentsInChildren<Transform>(true))
                if(child.name=="UI_ShopTitle" || child.name=="IMG_Bg_Blue" || child.name=="IMG_Bg_Red")child.gameObject.SetActive(false);
            var background=Image(store,"Shop background",Vector2.zero,Vector2.zero,null);
            background.color=new Color(.24f,.14f,.56f);
            background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;
            background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;background.transform.SetAsFirstSibling();
            var header=Rect(safeArea,"Shop canopy",new Vector2(900,300),new Vector2(0,-150));
            header.anchorMin=new Vector2(0,1);header.anchorMax=Vector2.one;header.sizeDelta=new Vector2(0,300);
            var art=Image(header,"Canopy art",Vector2.zero,Vector2.zero,Sprite("Canopy"),false);
            art.rectTransform.anchorMin=Vector2.zero;art.rectTransform.anchorMax=Vector2.one;
            safeArea.gameObject.AddComponent<SafeAreaFit>().Configure(true,false,art.rectTransform);
            header.gameObject.SetActive(false);
            GetComponent<LobbyNavigation>().PageChanged+=page=>header.gameObject.SetActive(page==0);
            var sign=Image(header,"Shop sign",new Vector2(280,100),new Vector2(0,5),Sprite("Shop_Sign"));
            Label(sign.transform,"Shop",new Vector2(240,80),Vector2.zero,60,Color.white);
            var balance=Image(header,"Gold balance",new Vector2(235,75),new Vector2(-275,10),Sprite("IMG_TimeBox"));
            Icon(balance.transform,"IMG_Coin",new Vector2(85,85),new Vector2(-103,0));
            goldBalance=Label(balance.transform,"",new Vector2(165,55),new Vector2(21,0),32,Ink);
        }
        private void RenderPackage(ShopProductRow product,Sprite[] boosterIcons)
        {
            bool noAds=product.layout=="NoAds";
            var card=Card((noAds?"NoAds_":"Bundle_")+product.id,noAds?410:420,noAds?"IMG_NoADBundle_Bg":"IMG_Bundle_Bg");
            // Reward wells group the contents without moving the existing items or hit targets.
            if(!noAds)Image(card,"Reward well",new Vector2(405,260),new Vector2(178,66),Sprite("Shop_RewardWell"));
            if(noAds)
            {
                Icon(card,product.icon,new Vector2(220,198),new Vector2(-235,72));
                Label(card,product.rewards.Length>1?app.Shop.Describe(product):product.description,
                    new Vector2(490,170),new Vector2(120,72),43,Ink);
            }
            else
            {
                var main=app.Shop.Rewards(product,0).First();
                Icon(card,"IMG_GoodsLight",new Vector2(335,245),new Vector2(-230,100));
                Icon(card,product.icon,new Vector2(300,190),new Vector2(-230,100));
                Label(card,app.Shop.Amount(main),new Vector2(320,46),new Vector2(-230,-18),44,Ink);
                var secondary=app.Shop.Rewards(product,1).ToArray();
                for(int i=0;i<secondary.Length;i++)
                {
                    float x=180+(i-(secondary.Length-1)*.5f)*108;
                    var definition=app.Shop.Currencies.Get(secondary[i].currencyId);
                    Icon(card,definition.icon,new Vector2(92,80),new Vector2(x,150));
                    if(definition.duration)Icon(card,"IMG_HeartInfinite",new Vector2(76,44),new Vector2(x,156));
                    Label(card,app.Shop.Amount(secondary[i]),new Vector2(110,40),new Vector2(x,95),33,Ink);
                }
                var extras=app.Shop.Rewards(product,2).ToArray();
                float width=Mathf.Min(100,400f/Mathf.Max(1,extras.Length));
                for(int i=0;i<extras.Length;i++)
                {
                    var definition=app.Shop.Currencies.Get(extras[i].currencyId);
                    var icon=string.IsNullOrEmpty(definition.boosterKind)?Sprite(definition.icon):
                        boosterIcons[BoosterCatalog.All.ToList().FindIndex(o=>o.Kind.ToString()==definition.boosterKind)];
                    float x=180+(i-(extras.Length-1)*.5f)*width;
                    Image(card,"Reward_"+extras[i].currencyId,new Vector2(width-4,86),new Vector2(x,30),icon,false).preserveAspect=true;
                    Label(card,"x"+app.Shop.Amount(extras[i]),new Vector2(width,34),new Vector2(x,-22),30,Ink);
                }
            }
            if(!string.IsNullOrEmpty(product.badge))
            {
                var tag=Icon(card,"IMG_Shop_Tag",new Vector2(175,175),new Vector2(-336,127));
                var badge=Label(tag.transform,product.badge,new Vector2(180,42),new Vector2(-17,23),27,Color.white);
                badge.rectTransform.localEulerAngles=new Vector3(0,0,45);
            }
            Footer(card,product.name,ShopCatalog.Price(product),()=>Preview(product));
        }
        private void Preview(ShopProductRow product)
        {
            showInfo(product.name,app.Shop.Describe(product)+"\n\nPurchases are not available yet.\nNo payment will be taken.");
        }
        private Sprite Sprite(string name)=>sprites[name];
        private void Gap(float height)=>cursor+=height;
        private RectTransform Section(string title,string sprite)
        {
            var heading=Image(content,"Category_"+title,new Vector2(860,124),new Vector2(0,-cursor-62),Sprite(sprite)).rectTransform;
            Label(heading,title,new Vector2(790,90),new Vector2(0,5),53,Color.white);
            cursor+=165;return heading;
        }
        private RectTransform Card(string name,float height,string sprite)
        {
            var card=Image(content,name,new Vector2(850,height),new Vector2(0,-cursor-height/2),Sprite(sprite)).rectTransform;
            cursor+=height+30;return card;
        }
        private void Footer(RectTransform card,string title,string price,Action click)
        {
            float y=-card.sizeDelta.y*.285f;
            var strip=Image(card,"Product footer",new Vector2(814,117),new Vector2(0,y-5),Sprite("Shop_Footer"));
            strip.color=new Color(.75f,.9f,1);
            var label=Label(card,title,new Vector2(455,84),new Vector2(-140,y),49,Color.white);
            label.alignment=TextAlignmentOptions.Left;
            Buy(card,"Preview_"+card.name,price,new Vector2(305,120),new Vector2(250,y),51,click);
        }
        private Button Buy(Transform parent,string name,string text,Vector2 size,Vector2 position,float fontSize,Action click)
        {
            var image=Image(parent,name,size,position,Sprite("IMG_Bundle_Goods_Btn"));image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            Label(button.transform,text,size-new Vector2(28,18),new Vector2(0,3),fontSize,Color.white);
            button.onClick.AddListener(()=>click());return button;
        }
        private Image Icon(Transform parent,string sprite,Vector2 size,Vector2 position)
        {
            var icon=Image(parent,sprite,size,position,Sprite(sprite),false);icon.preserveAspect=true;return icon;
        }
        private IEnumerator ScrollToTop()
        { yield return null;Canvas.ForceUpdateCanvases();Scroll.verticalNormalizedPosition=1; }
        private void Refresh()
        {
            if(goldBalance!=null)goldBalance.text=app.Gold.ToString();
        }
        private RectTransform Rect(Transform parent,string name,Vector2 size,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)go.transform;rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=parent==content?new Vector2(.5f,1):new Vector2(.5f,.5f);
            rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        private Image Image(Transform parent,string name,Vector2 size,Vector2 position,Sprite sprite,bool sliced=true)
        {
            var image=Rect(parent,name,size,position).gameObject.AddComponent<Image>();image.sprite=sprite;
            image.type=sliced && sprite!=null && sprite.border!=Vector4.zero?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            // Full-height end caps scale uniformly with the control's height. Only the middle stretches.
            // A conventional four-sided slice cuts through the rounded ends of these illustrated pills.
            if(image.type==UnityEngine.UI.Image.Type.Sliced && sprite.border.y==0 && sprite.border.w==0 && size.y>0)
                image.pixelsPerUnitMultiplier=sprite.rect.height/(size.y*image.pixelsPerUnit);
            image.raycastTarget=false;return image;
        }
        private TMP_Text Label(Transform parent,string text,Vector2 size,Vector2 position,float fontSize,Color color)
        {
            var label=Rect(parent,"Label",size,position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font=fontSource.font;label.fontSharedMaterial=shopFontMaterial;label.fontSize=fontSize;
            label.enableAutoSizing=true;label.fontSizeMin=fontSize*.75f;label.fontSizeMax=fontSize;label.color=color;
            label.alignment=TextAlignmentOptions.Center;label.text=text;label.raycastTarget=false;return label;
        }
        private void OnDestroy(){if(app!=null)app.Changed-=Refresh;if(shopFontMaterial!=null)Destroy(shopFontMaterial);}
    }
}
