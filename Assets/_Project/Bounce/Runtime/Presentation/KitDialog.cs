using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    public sealed class KitDialog : MonoBehaviour
    {
        private TMP_FontAsset font;
        private Material fontMaterial;
        private Sprite buttonSprite;
        private RectTransform panel;
        private float previousTimeScale;
        private bool closed;

        public static KitDialog Show(RectTransform parent, GamePresentation presentation, string title, string message, bool profile=false)
        {
            var root=new GameObject("Kit Dialog",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster),typeof(Image),typeof(KitDialog));
            var rect=(RectTransform)root.transform;rect.SetParent(parent,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var canvas=root.GetComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=30000;
            root.GetComponent<Image>().color=new Color(0,0,0,.75f);
            var view=root.GetComponent<KitDialog>();view.previousTimeScale=Time.timeScale;Time.timeScale=0;
            var source=presentation.settings.GetComponentInChildren<TMP_Text>(true);
            view.font=source.font;view.fontMaterial=source.fontSharedMaterial;
            var skin=presentation.settings.GetComponentsInChildren<Image>(true).First(i=>i.name=="UI_Setting_01");
            view.panel=view.Image(rect,"Dialog panel",new Vector2(780,760),Vector2.zero,skin.sprite,new Color(.45f,.4f,1)).rectTransform;
            var win=presentation.win.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="StageClearPopup");
            view.buttonSprite=(win.Get<Button>("basic.claimButton").targetGraphic as Image)?.sprite;
            view.Label(view.panel,"Title",title,new Vector2(680,90),new Vector2(0,285),44);
            var body=view.Label(view.panel,"Message",message,new Vector2(670,profile?220:330),new Vector2(0,profile?120:55),28);
            body.enableAutoSizing=true;body.fontSizeMin=22;body.fontSizeMax=28;
            view.MakeButton("Dialog Close","Close",new Vector2(0,-275),view.Close);
            if(profile)view.AddNameEditor();
            return view;
        }

        public static KitDialog ShowLicenses(RectTransform parent, GamePresentation presentation)
        {
            string message = "Fonts / SIL Open Font License 1.1\n\n";
            foreach (string family in new[] { "LilitaOne", "Jua" })
            {
                var license = Resources.Load<TextAsset>("FontLicenses/" + family + "/OFL");
                if (license == null) throw new InvalidOperationException("Missing bundled OFL license: " + family);
                message += family + "\nSource: https://github.com/google/fonts/tree/main/ofl/" + family.ToLowerInvariant()
                    + "\nFont license: SIL Open Font License 1.1\n\n" + license.text + "\n\n";
            }
            var view = Show(parent, presentation, "Font Licenses", "");
            view.panel.sizeDelta = new Vector2(780, 1080);
            view.panel.Find("Title").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 455);
            view.panel.Find("Dialog Close").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -445);
            var viewport = view.Image(view.panel, "License viewport", new Vector2(690, 750), Vector2.zero, null, new Color(.08f, .06f, .2f, .95f)).rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45; scroll.viewport = viewport;
            var body = view.panel.Find("Message").GetComponent<TMP_Text>();
            body.transform.SetParent(viewport, false);
            body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.pivot = new Vector2(.5f, 1);
            body.rectTransform.sizeDelta = new Vector2(-40, 0); body.rectTransform.anchoredPosition = Vector2.zero;
            body.font = view.font.fallbackFontAssetTable.FirstOrDefault() ?? view.font;
            body.fontSharedMaterial = body.font.material;
            body.enableAutoSizing = false; body.fontSize = 26;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            body.margin = new Vector4(0, 14, 0, 14); body.text = message;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = body.rectTransform;
            var rail = view.Image(view.panel, "License scrollbar", new Vector2(14, 750), new Vector2(356, 0), null, new Color(.12f,.08f,.3f));
            var handle = view.Image(rail.transform, "Handle", Vector2.zero, Vector2.zero, null, new Color(.7f,.65f,1));
            var bar = rail.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform; bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(body.rectTransform);
            scroll.verticalNormalizedPosition = 1;
            return view;
        }

        private Image Image(Transform parent,string label,Vector2 size,Vector2 position,Sprite sprite,Color color)
        {
            var image=new GameObject(label,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent,false);image.rectTransform.sizeDelta=size;image.rectTransform.anchoredPosition=position;
            image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=color;return image;
        }
        private TMP_Text Label(Transform parent,string label,string text,Vector2 size,Vector2 position,int fontSize)
        {
            var value=new GameObject(label,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            value.transform.SetParent(parent,false);value.rectTransform.sizeDelta=size;value.rectTransform.anchoredPosition=position;
            value.font=font;value.fontSharedMaterial=fontMaterial;value.fontSize=fontSize;value.text=text;
            value.alignment=TextAlignmentOptions.Center;value.raycastTarget=false;value.richText=false;return value;
        }
        private void MakeButton(string name,string text,Vector2 position,Action callback)
        {
            var image=Image(panel,name,new Vector2(360,100),position,buttonSprite,Color.white);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>callback());
            Label(image.transform,"Label",text,new Vector2(320,85),Vector2.zero,32);
        }
        private void AddNameEditor()
        {
            var box=Image(panel,"Player name",new Vector2(610,85),new Vector2(0,-55),null,new Color(.1f,.13f,.3f));
            var field=box.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=box;
            var text=Label(box.transform,"Name text","",new Vector2(560,75),Vector2.zero,30);
            field.textViewport=text.rectTransform;field.textComponent=(TextMeshProUGUI)text;
            field.characterLimit=24;field.lineType=TMP_InputField.LineType.SingleLine;field.text=KitApp.Instance.PlayerName;
            var feedback=Label(panel,"Profile feedback","",new Vector2(650,55),new Vector2(0,-140),24);
            MakeButton("Save name","Save name",new Vector2(0,-195),()=>
            {
                feedback.text=KitApp.Instance.TrySetPlayerName(field.text)?"Name saved":"Use 1-24 characters. Check local save access.";
            });
            // Make room for separate save and close controls.
            panel.Find("Dialog Close").GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-310);
        }
        public void Close()
        {
            if(closed)return;closed=true;Time.timeScale=previousTimeScale;Destroy(gameObject);
        }
        private void OnDestroy() {if(!closed)Time.timeScale=1;}
    }
}
