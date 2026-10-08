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
        private sealed class Row {public BoosterOffer offer;public TMP_Text owned;public Button buy;}
        private readonly List<Row> rows=new List<Row>();
        private KitApp app;
        private TMP_Text feedback;
        private TMP_Text fontSource;
        private Sprite cardSprite,buySprite;
        public void Initialize(GameObject screen,GamePresentation presentation)
        {
            app=KitApp.Instance;
            var store=screen.GetComponentsInChildren<Transform>(true).First(t=>t.name=="store");
            var scroll=store.GetComponentsInChildren<ScrollRect>(true).First();
            var title=store.GetComponentsInChildren<Transform>(true).First(t=>t.name=="IMG_SpecialBundleTitle");
            fontSource=title.GetComponentInChildren<TMP_Text>(true);
            cardSprite=title.GetComponent<Image>().sprite;
            var win=presentation.win.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="StageClearPopup");
            buySprite=(win.Get<Button>("basic.claimButton").targetGraphic as Image)?.sprite;
            var content=scroll.content;
            foreach(var group in content.GetComponents<LayoutGroup>())group.enabled=false;
            var fitter=content.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            foreach(Transform child in content)child.gameObject.SetActive(false);
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
            content.sizeDelta=new Vector2(0,1500);content.anchoredPosition=Vector2.zero;
            var heading=MakeImage(content,"Booster shop",new Vector2(850,120),new Vector2(0,-270),cardSprite);
            Label(heading.transform,"Boosters",new Vector2(780,95),Vector2.zero,50);
            var ingame=presentation.ingame.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="UIIngamePanel");
            for(int i=0;i<BoosterCatalog.All.Count;i++)
            {
                var offer=BoosterCatalog.All[i];
                var card=MakeImage(content,"Shop_"+offer.Kind,new Vector2(850,200),new Vector2(0,-495-i*225),cardSprite);
                var source=ingame.GetComponentsInChildren<Transform>(true).First(t=>t.name=="BTN_"+offer.Kind).GetComponentsInChildren<Image>(true).First(image=>image.name=="IMG_Item");
                var icon=MakeImage(card.transform,"Icon",new Vector2(145,145),new Vector2(-320,0),source.sprite);icon.preserveAspect=true;
                Label(card.transform,offer.Title,new Vector2(310,55),new Vector2(-60,62),34);
                Label(card.transform,offer.Description,new Vector2(310,70),new Vector2(-60,0),25);
                var owned=Label(card.transform,"",new Vector2(310,40),new Vector2(-60,-70),26);
                var buyImage=MakeImage(card.transform,"Buy_"+offer.Kind,new Vector2(220,95),new Vector2(290,0),buySprite);buyImage.raycastTarget=true;
                var buy=buyImage.gameObject.AddComponent<Button>();buy.targetGraphic=buyImage;
                Label(buy.transform,offer.Price+" Gold",new Vector2(205,65),Vector2.zero,30);
                buy.onClick.AddListener(()=>{app.TryBuyBooster(offer.Kind,out var message);feedback.text=message;});
                rows.Add(new Row {offer=offer,owned=owned,buy=buy});
            }
            feedback=Label(content,"Earn Gold by clearing levels",new Vector2(840,35),new Vector2(0,-360),25);
            app.Changed+=Refresh;Refresh();StartCoroutine(ScrollToTop(scroll));
        }
        private static IEnumerator ScrollToTop(ScrollRect scroll)
        {yield return null;Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;}
        private void Refresh()
        {
            foreach(var row in rows)
            {
                row.owned.text="Owned: "+app.Progress.Count(row.offer.Kind);
                row.buy.interactable=!app.IsLoading && app.Progress.Count(row.offer.Kind)<int.MaxValue;
                row.buy.GetComponent<Image>().color=app.Progress.gold>=row.offer.Price?Color.white:new Color(.6f,.6f,.6f,1);
            }
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 size,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)go.transform;rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=parent.name=="Content"?new Vector2(.5f,1):new Vector2(.5f,.5f);
            rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        private static Image MakeImage(Transform parent,string name,Vector2 size,Vector2 position,Sprite sprite)
        {
            var image=Rect(parent,name,size,position).gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.raycastTarget=false;return image;
        }
        private TMP_Text Label(Transform parent,string text,Vector2 size,Vector2 position,float fontSize)
        {
            var label=Rect(parent,"Label",size,position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font=fontSource.font;label.fontSharedMaterial=fontSource.fontSharedMaterial;label.fontSize=fontSize;
            label.alignment=TextAlignmentOptions.Center;label.text=text;label.raycastTarget=false;return label;
        }
        private void OnDestroy(){if(app!=null)app.Changed-=Refresh;}
    }
}
