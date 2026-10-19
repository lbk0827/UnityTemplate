using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit.Editor
{
    public static class RemainingPopupSetup
    {
        [MenuItem("BK/Integration/Refine Remaining Popups")]
        public static void Run()
        {
            var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            var goods=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Bounce/Content/Shop/Original/ShopIcons.png").OfType<Sprite>().ToDictionary(s=>s.name);
            foreach(var prefab in new[]{p.lose,p.welcomeDeal,p.endlessOffer,p.endlessGifts})
            {
                string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if(prefab==p.lose)RefineFailure(root,p,goods);
                    else if(prefab==p.welcomeDeal)RefineWelcome(root,goods);
                    else RefineSteps(root,p,goods,prefab==p.endlessGifts);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();Debug.Log("BK_REMAINING_POPUPS_OK");
        }
        private static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        private static void Place(Transform target,float x,float y,float w,float h)
        {var r=(RectTransform)target;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;}
        private static Image Shape(Transform parent,string name,Sprite sprite,float x,float y,float w,float h)
        {
            var t=parent.Find(name);var image=t!=null?t.GetComponent<Image>():new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent,false);image.gameObject.SetActive(true);Place(image.transform,x,y,w,h);image.sprite=sprite;image.color=Color.white;image.raycastTarget=false;
            image.type=sprite.border!=Vector4.zero?Image.Type.Sliced:Image.Type.Simple;image.preserveAspect=image.type==Image.Type.Simple;return image;
        }
        private static TMP_Text Title(Transform parent,string name,TMP_Text source,string value,float x,float y,float w,float h,float size)
        {
            var t=parent.Find(name);var text=t!=null?t.GetComponent<TMP_Text>():Object.Instantiate(source,parent,false);
            text.name=name;text.gameObject.SetActive(true);Place(text.transform,x,y,w,h);text.text=value;text.enableAutoSizing=true;text.fontSizeMin=size*.75f;text.fontSizeMax=size;
            text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;
        }
        private static void RefineFailure(GameObject root,GamePresentation p,Dictionary<string,Sprite> goods)
        {
            var frame=Find(root,"Root");var content=Find(root,"UI_Contents");content.SetParent(frame,false);Place(content,0,0,900,1600);
            var panel=Shape(frame,"Continue frame",p.heart.GetComponentsInChildren<Image>(true).First(i=>i.name=="IMG_Popup").sprite,0,0,820,1140);panel.transform.SetAsFirstSibling();
            var b=root.GetComponentsInChildren<VisualBindings>(true).First(v=>v.role=="UIFailPopup");
            var title=Find(root,"txt_moveszero01").GetComponent<TMP_Text>();Place(title.transform,0,365,720,120);title.fontSizeMax=72;title.fontSizeMin=48;title.enableAutoSizing=true;
            foreach(Transform child in title.transform)child.gameObject.SetActive(false);
            var hero=Find(root,"img_moveszero").GetComponent<Image>();hero.sprite=goods["Shop_ExtraBall"];hero.preserveAspect=true;Place(hero.transform,0,70,350,350);
            var source=p.settings.GetComponentsInChildren<TMP_Text>(true).First(t=>t.font!=null);
            title.font=source.font;title.fontSharedMaterial=source.fontSharedMaterial;title.color=new Color(1,1,.78f);title.enableVertexGradient=false;
            var well=Shape(content,"Continue reward well",p.refinedSprites.First(s=>s.name=="Well"),0,100,680,400);well.transform.SetAsFirstSibling();
            var glow=Shape(content,"Continue reward glow",p.refinedSprites.First(s=>s.name=="Hero"),0,110,360,360);glow.color=new Color(1,1,1,.28f);glow.transform.SetSiblingIndex(1);
            Title(content,"Continue description",source,"5 extra balls",0,-125,680,80,48);
            Title(content,"Continue hint",source,"Keep your progress and finish the level!",0,-205,680,65,28);
            var buy=b.Get<Button>("replayButton");Place(buy.transform,0,-338,570,164);
            foreach(var image in buy.GetComponentsInChildren<Image>(true))if(image.sprite!=null && image.sprite.border!=Vector4.zero)image.pixelsPerUnitMultiplier=176f/164f;
            var label=b.Get<TMP_Text>("playOnLabels.0");label.text="Continue";Place(label.transform,-110,3,280,100);label.enableAutoSizing=true;label.fontSizeMax=48;label.fontSizeMin=34;
            var price=b.Get<TMP_Text>("continuePriceText");Place(price.transform,170,3,140,100);price.enableAutoSizing=true;price.fontSizeMax=48;price.fontSizeMin=34;
            var coin=Find(buy.gameObject,"img_icon").GetComponent<Image>();coin.sprite=goods["IMG_Coin"];coin.preserveAspect=true;Place(coin.transform,65,3,66,66);
            Place(b.Get<Button>("closeButton").transform,360,520,120,120);
            Place(b.Get<Button>("currencyButton").transform,-220,487,224,96);
            b.Get<GameObject>("currencyPlusIcon").SetActive(false);
            root.GetComponentsInChildren<VisualBindings>(true).First(v=>v.role=="UIHUDSub_Currency").Get<Image>("icon").sprite=goods["IMG_Coin"];
            var leave=frame.Find("End level")?.GetComponent<Button>();
            if(leave==null)
            {
                var native=p.retry.GetComponentsInChildren<VisualBindings>(true).First(v=>v.role=="UIRetryPopup").Get<Button>("retryButton");
                leave=Object.Instantiate(native,frame,false);leave.name="End level";
            }
            Place(leave.transform,0,-490,330,104);
            foreach(var image in leave.GetComponentsInChildren<Image>(true))
            {image.pixelsPerUnitMultiplier=176f/104f;if(image.sprite!=null && image.sprite.name=="BTN_CommonGreen")image.sprite=p.refinedSprites.First(s=>s.name=="Secondary");}
            var leaveText=leave.GetComponentInChildren<TMP_Text>(true);leaveText.text="End level";leaveText.enableAutoSizing=true;leaveText.fontSizeMax=36;leaveText.fontSizeMin=28;
            b.entries.First(e=>e.key=="lobbyButton").value=leave;
        }
        private static void RefineWelcome(GameObject root,Dictionary<string,Sprite> goods)
        {
            var group=Find(root,"IMG_Bg_Goods");foreach(Transform child in group)child.gameObject.SetActive(false);
            Shape(group,"Welcome bundle",goods["IMG_Bundle_Item_00"],40,-30,530,470);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("IMG_FrontCoin"))t.gameObject.SetActive(false);
            var bg=Find(root,"IMG_WelcomeDealBg_T").GetComponent<Image>();bg.color=new Color(.84f,.83f,1);
            Place(Find(root,"IMG_Mark"),-275,330,230,232);
            var title=Find(root,"TXT_Title").GetComponent<TMP_Text>();title.enableAutoSizing=true;title.fontSizeMax=76;title.fontSizeMin=50;
            Place(title.transform,0,53,660,112);
            Place(Find(root,"UI_Reward"),0,-316,650,300);
            foreach(var layout in Find(root,"UI_Reward").GetComponents<LayoutGroup>())layout.enabled=false;
            var buy=Find(root,"BTN_Purchase");Place(buy,0,-606,436,176);
            foreach(var image in buy.GetComponentsInChildren<Image>(true))if(image.sprite!=null && image.sprite.name=="BTN_CommonGreen")image.pixelsPerUnitMultiplier=1;
        }
        private static void RefineSteps(GameObject root,GamePresentation p,Dictionary<string,Sprite> goods,bool chain)
        {
            var target=Find(root,"Target");var header=Find(root,"UI_Title");foreach(Transform child in header)child.gameObject.SetActive(false);
            var source=p.settings.GetComponentsInChildren<TMP_Text>(true).First(t=>t.font!=null);
            Shape(header,"Offer glow",p.refinedSprites.First(s=>s.name=="Hero"),0,50,470,470).color=new Color(1,1,1,.5f);
            Shape(header,"Offer hero",goods[chain?"IMG_Bundle_Item_00":"Goods_Coin_06"],0,60,350,330);
            Shape(header,"Offer ribbon",p.refinedSprites.First(s=>s.name=="Header"),0,-170,700,168);
            var title=Title(header,"Offer heading",source,chain?"Endless Gifts":"Endless Offer",0,-160,540,100,62);
            var binding=root.GetComponentsInChildren<VisualBindings>(true).First(v=>v.role==(chain?"UIEndlessGiftsPopup":"UIEndlessOfferPopup"));
            binding.entries.First(e=>e.key=="titleText").value=title;
            Title(header,"Offer instructions",source,"Claim each step to unlock the next",0,-285,780,70,30);
            Place(Find(root,"UI_Timer"),0,165,260,74);
            Place(Find(root,"BTN_Close"),385,825,120,120);
            foreach(var slot in root.GetComponentsInChildren<VisualBindings>(true).Where(b=>b.role=="UIEndlessOfferSlotView"))
            {
                var buy=slot.Get<Button>("buyButton");var image=buy.GetComponent<Image>();
                if(image!=null)image.pixelsPerUnitMultiplier=176f/((RectTransform)buy.transform).sizeDelta.y;
                var locked=slot.Get<GameObject>("lockRoot");
                foreach(var i in locked.GetComponentsInChildren<Image>(true))
                {
                    if(i.name=="IMG_UnLock")i.gameObject.SetActive(false);
                    if(i.name=="IMG_Lock"){i.gameObject.SetActive(true);Place(i.transform,0,0,58,73);}
                }
                // Draw the current gold frame above the blue frame, below rewards.
                slot.Get<GameObject>("currentRoot").transform.SetSiblingIndex(1);
            }
            foreach(var arrow in root.GetComponentsInChildren<Image>(true).Where(i=>i.name.StartsWith("IMG_Arrow")))
            {
                if(!chain)arrow.rectTransform.localEulerAngles=Vector3.zero;
                else
                {
                    var rail=arrow.GetComponentsInParent<Transform>(true).First(t=>t.name.StartsWith("RailGroup_"));
                    int index=int.Parse(rail.name.Substring("RailGroup_".Length));
                    arrow.rectTransform.localEulerAngles=new Vector3(0,0,index%2==1?-90:index%4==0?0:180);
                }
            }
        }
        public static void Inspect()
        {
            var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
            var lines=new List<string>();
            foreach(var prefab in new[]{p.heart,p.retry,p.lose,p.welcomeDeal,p.endlessOffer,p.endlessGifts})
            {
                lines.Add("PREFAB "+prefab.name);
                foreach(var t in prefab.GetComponentsInChildren<RectTransform>(true))
                {
                    var text=t.GetComponent<TMP_Text>();var image=t.GetComponent<Image>();
                    lines.Add(PathOf(t)+" | pos="+t.anchoredPosition+" size="+t.sizeDelta+" scale="+t.localScale+" anchors="+t.anchorMin+","+t.anchorMax+" active="+t.gameObject.activeSelf+" | "+(text!=null?text.text:image!=null && image.sprite!=null?image.sprite.name+" "+image.sprite.rect+" border="+image.sprite.border:""));
                }
                foreach(var b in prefab.GetComponentsInChildren<VisualBindings>(true))
                    foreach(var e in b.entries)lines.Add("BIND "+b.role+" "+e.key+" => "+(e.value is Component c?PathOf(c.transform):e.value is GameObject g?PathOf(g.transform):e.value!=null?e.value.name:"none"));
            }
            Directory.CreateDirectory("TestResults");File.WriteAllLines("TestResults/remaining-popup-inventory.txt",lines);
        }
        private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    }
}
