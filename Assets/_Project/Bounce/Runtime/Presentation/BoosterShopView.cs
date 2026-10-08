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
        private sealed class Row { public BoosterOffer offer; public TMP_Text owned; public Button buy; }
        private readonly List<Row> rows=new List<Row>();
        private readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        private KitApp app;
        private TMP_Text fontSource,feedback,goldBalance;
        private RectTransform content;
        private Action<string,string> showInfo;
        private float cursor;
        public ScrollRect Scroll { get; private set; }
        public RectTransform CoinsSection { get; private set; }
        public RectTransform BoostersSection { get; private set; }

        public void Initialize(GameObject screen,GamePresentation presentation,Action<string,string> showInfo)
        {
            this.showInfo=showInfo;app=KitApp.Instance;
            foreach(var sprite in presentation.shopSprites)if(sprite!=null)sprites[sprite.name]=sprite;
            var store=screen.GetComponentsInChildren<Transform>(true).First(t=>t.name=="store");
            Scroll=store.GetComponentsInChildren<ScrollRect>(true).First();
            fontSource=store.GetComponentsInChildren<Transform>(true).First(t=>t.name=="IMG_SpecialBundleTitle").GetComponentInChildren<TMP_Text>(true);
            content=Scroll.content;
            foreach(var group in content.GetComponents<LayoutGroup>())group.enabled=false;
            var fitter=content.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            foreach(Transform child in content)child.gameObject.SetActive(false);
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
            content.anchoredPosition=Vector2.zero;
            Scroll.horizontal=false;Scroll.vertical=true;Scroll.movementType=ScrollRect.MovementType.Elastic;
            // Keep the scrolling viewport between the fixed canopy and navigation on every aspect ratio.
            SetupHeader(store);
            var scrollRect=(RectTransform)Scroll.transform;scrollRect.SetParent(transform,false);
            scrollRect.anchorMin=Vector2.zero;scrollRect.anchorMax=Vector2.one;
            scrollRect.offsetMin=new Vector2(0,285);scrollRect.offsetMax=new Vector2(0,-300);
            Scroll.viewport.anchorMin=Vector2.zero;Scroll.viewport.anchorMax=Vector2.one;
            Scroll.viewport.offsetMin=Scroll.viewport.offsetMax=Vector2.zero;
            Scroll.gameObject.SetActive(false);
            GetComponent<LobbyNavigation>().PageChanged+=page=>Scroll.gameObject.SetActive(page==0);
            cursor=20;
            var ingame=presentation.ingame.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="UIIngamePanel");
            var boosterIcons=BoosterCatalog.All.Select(offer=>ingame.GetComponentsInChildren<Transform>(true)
                .First(t=>t.name=="BTN_"+offer.Kind).GetComponentsInChildren<Image>(true).First(i=>i.name=="IMG_Item").sprite).ToArray();
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
                        Icon(card,product.icon,new Vector2(225,180),new Vector2(0,56));
                        Label(card,app.Shop.Amount(reward),new Vector2(252,60),new Vector2(0,-30),41,Color.white);
                        Buy(card,"Preview_Coins_"+reward.amount,ShopCatalog.Price(product),new Vector2(240,96),new Vector2(0,-127),38,
                            ()=>Preview(product));
                    }
                    else RenderPackage(product,boosterIcons);
                    index++;
                }
                if(coins)
                {
                    float height=Mathf.Ceil(index/3f)*390;cursor=top+height;
                    var background=Image(content,"Coin section background",new Vector2(940,height+180),new Vector2(0,-top-height/2+80),null);
                    background.color=new Color(.32f,.045f,.015f);background.transform.SetAsFirstSibling();
                }
                Gap(30);
            }
            BoostersSection=Section("Boosters","IMG_ShopTitle_Package");
            feedback=Label(content,"Use Gold earned from levels",new Vector2(840,50),new Vector2(0,-cursor-25),28,Color.white);cursor+=65;
            for(int i=0;i<BoosterCatalog.All.Count;i++)
            {
                var offer=BoosterCatalog.All[i];var card=Card("Shop_"+offer.Kind,290,"IMG_Bundle_Bg");
                Image(card,"Icon",new Vector2(130,130),new Vector2(-320,40),boosterIcons[i],false).preserveAspect=true;
                Label(card,offer.Title,new Vector2(390,52),new Vector2(-32,83),35,Color.white);
                Label(card,offer.Description,new Vector2(580,58),new Vector2(55,27),27,Color.white);
                var owned=Label(card,"",new Vector2(450,50),new Vector2(-170,-93),29,Color.white);
                var buy=Buy(card,"Buy_"+offer.Kind,app.Shop.BoosterPrice(offer.Kind)+" Gold",new Vector2(290,90),new Vector2(250,-92),32,
                    ()=>{app.TryBuyBooster(offer.Kind,out var message);feedback.text=message;});
                rows.Add(new Row {offer=offer,owned=owned,buy=buy});
            }
            content.sizeDelta=new Vector2(0,cursor+30);
            app.Changed+=Refresh;Refresh();StartCoroutine(ScrollToTop());
        }
        private void SetupHeader(Transform store)
        {
            foreach(var child in store.GetComponentsInChildren<Transform>(true))
                if(child.name=="UI_ShopTitle" || child.name=="IMG_Bg_Blue" || child.name=="IMG_Bg_Red")child.gameObject.SetActive(false);
            var background=Image(store,"Shop background",Vector2.zero,Vector2.zero,null);
            background.color=new Color(.27f,.055f,.38f);
            background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;
            background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;background.transform.SetAsFirstSibling();
            var header=Image(transform,"Shop canopy",new Vector2(900,300),new Vector2(0,-150),Sprite("IMG_ShopBox")).rectTransform;
            header.GetComponent<Image>().type=UnityEngine.UI.Image.Type.Tiled;
            header.anchorMin=new Vector2(0,1);header.anchorMax=Vector2.one;header.sizeDelta=new Vector2(0,300);
            header.gameObject.SetActive(false);
            GetComponent<LobbyNavigation>().PageChanged+=page=>header.gameObject.SetActive(page==0);
            Label(header,"Shop",new Vector2(400,110),new Vector2(0,15),70,Color.white);
            var balance=Image(header,"Gold balance",new Vector2(235,75),new Vector2(-275,10),Sprite("IMG_TimeBox"));
            Icon(balance.transform,"IMG_Coin",new Vector2(85,85),new Vector2(-103,0));
            goldBalance=Label(balance.transform,"",new Vector2(165,55),new Vector2(21,0),32,new Color(.26f,.09f,0));
        }
        private void RenderPackage(ShopProductRow product,Sprite[] boosterIcons)
        {
            bool noAds=product.layout=="NoAds";
            var card=Card((noAds?"NoAds_":"Bundle_")+product.id,noAds?410:420,noAds?"IMG_NoADBundle_Bg":"IMG_Bundle_Bg");
            if(noAds)
            {
                Icon(card,product.icon,new Vector2(245,230),new Vector2(-235,65));
                Label(card,product.rewards.Length>1?app.Shop.Describe(product):product.description,
                    new Vector2(490,170),new Vector2(120,75),43,new Color(1,.88f,.25f));
            }
            else
            {
                var main=app.Shop.Rewards(product,0).First();
                Icon(card,"IMG_GoodsLight",new Vector2(335,245),new Vector2(-230,65));
                Icon(card,product.icon,new Vector2(280,235),new Vector2(-235,68));
                Label(card,app.Shop.Amount(main),new Vector2(320,64),new Vector2(-230,-34),52,Color.white);
                var secondary=app.Shop.Rewards(product,1).ToArray();
                for(int i=0;i<secondary.Length;i++)
                {
                    float x=180+(i-(secondary.Length-1)*.5f)*108;
                    var definition=app.Shop.Currencies.Get(secondary[i].currencyId);
                    Icon(card,definition.icon,new Vector2(92,80),new Vector2(x,140));
                    if(definition.duration)Icon(card,"IMG_HeartInfinite",new Vector2(76,44),new Vector2(x,146));
                    Label(card,app.Shop.Amount(secondary[i]),new Vector2(110,46),new Vector2(x,86),33,Color.white);
                }
                var extras=app.Shop.Rewards(product,2).ToArray();
                float width=Mathf.Min(100,400f/Mathf.Max(1,extras.Length));
                for(int i=0;i<extras.Length;i++)
                {
                    var definition=app.Shop.Currencies.Get(extras[i].currencyId);
                    var icon=string.IsNullOrEmpty(definition.boosterKind)?Sprite(definition.icon):
                        boosterIcons[BoosterCatalog.All.ToList().FindIndex(o=>o.Kind.ToString()==definition.boosterKind)];
                    float x=180+(i-(extras.Length-1)*.5f)*width;
                    Image(card,"Reward_"+extras[i].currencyId,new Vector2(width-4,96),new Vector2(x,3),icon,false).preserveAspect=true;
                    Label(card,"x"+app.Shop.Amount(extras[i]),new Vector2(width,38),new Vector2(x,-42),30,Color.white);
                }
            }
            if(!string.IsNullOrEmpty(product.badge))
            {
                var tag=Icon(card,"IMG_Shop_Tag",new Vector2(175,175),new Vector2(-336,127));
                var badge=Label(tag.transform,product.badge,new Vector2(180,42),new Vector2(-10,8),27,Color.white);
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
            Label(heading,title,new Vector2(790,90),new Vector2(0,5),53,new Color(1,1,.82f));
            cursor+=165;return heading;
        }
        private RectTransform Card(string name,float height,string sprite)
        {
            var card=Image(content,name,new Vector2(850,height),new Vector2(0,-cursor-height/2),Sprite(sprite)).rectTransform;
            cursor+=height+30;return card;
        }
        private void Footer(RectTransform card,string title,string price,Action click)
        {
            float y=-card.sizeDelta.y/2+84;
            var label=Label(card,title,new Vector2(490,84),new Vector2(-155,y),49,new Color(1,1,.82f));
            label.alignment=TextAlignmentOptions.Left;
            Buy(card,"Preview_"+card.name,price,new Vector2(305,120),new Vector2(250,y),51,click);
        }
        private Button Buy(Transform parent,string name,string text,Vector2 size,Vector2 position,float fontSize,Action click)
        {
            var image=Image(parent,name,size,position,Sprite("IMG_Bundle_Goods_Btn"));image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            Label(button.transform,text,size-new Vector2(28,18),Vector2.zero,fontSize,new Color(1,1,.84f));
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
            foreach(var row in rows)
            {
                row.owned.text="Owned: "+app.BoosterCount(row.offer.Kind);
                row.buy.interactable=!app.IsLoading && app.BoosterCount(row.offer.Kind)<int.MaxValue;
                row.buy.GetComponent<Image>().color=app.Gold>=app.Shop.BoosterPrice(row.offer.Kind)?Color.white:new Color(.6f,.6f,.6f,1);
            }
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
            image.type=sliced?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.raycastTarget=false;return image;
        }
        private TMP_Text Label(Transform parent,string text,Vector2 size,Vector2 position,float fontSize,Color color)
        {
            var label=Rect(parent,"Label",size,position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font=fontSource.font;label.fontSharedMaterial=fontSource.fontSharedMaterial;label.fontSize=fontSize;
            label.enableAutoSizing=true;label.fontSizeMin=fontSize*.75f;label.fontSizeMax=fontSize;label.color=color;
            label.alignment=TextAlignmentOptions.Center;label.text=text;label.raycastTarget=false;return label;
        }
        private void OnDestroy(){if(app!=null)app.Changed-=Refresh;}
    }
}
