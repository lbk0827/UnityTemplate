using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    public sealed class LobbyNavigation : MonoBehaviour
    {
        public int SelectedPage { get; private set; } = 1;
        public bool IsMoving { get; private set; }
        public event System.Action<int> PageChanged;
        private ScrollRect pages;
        private Toggle[] tabs;
        private Coroutine movement;
        private RectTransform[] tabIcons;
        private CanvasGroup[] tabPlates;
        private TabMotion[] motion;
        private TMPro.TMP_Text[] tabLabels;
        private Image[] plateImages;
        private readonly Vector3[] contentCorners=new Vector3[4];
        // GRP1_Common HUD FX_Click: 0.2s icon rise, 0.25s plate squash/release,
        // then the title fades in after 0.1s. Keep one timeline per tab so interrupted
        // clicks cannot run a stale FX_Selected callback over a newer deselection.
        private static readonly AnimationCurve Rise=new AnimationCurve(
            new Keyframe(0,0,-.07973566f,-.07973566f),
            new Keyframe(.30493808f,1.0912429f,1.4799817f,1.4799817f),
            new Keyframe(1,1,1.0741609f,1.0741609f));
        private static readonly AnimationCurve Release=new AnimationCurve(
            new Keyframe(0,0,2,2),new Keyframe(.4542177f,1.1364927f,.95191765f,.95191765f),new Keyframe(1,1,0,0));
        private sealed class TabMotion
        {
            public bool selected;
            public float elapsed=1, y, plate, label, scale=.9f;
            public float fromY,fromPlate,fromLabel,fromScale;
        }

        public void Initialize(GameObject screen, VisualBindings hud)
        {
            pages=screen.GetComponentsInChildren<ScrollRect>(true).First(s=>s.name=="svl_lobby");
            pages.enabled=false;
            tabs=new[]{hud.Get<Toggle>("StoreToggle"),hud.Get<Toggle>("HomeToggle"),hud.Get<Toggle>("ContentToggle")};
            tabIcons=new RectTransform[tabs.Length];tabPlates=new CanvasGroup[tabs.Length];motion=new TabMotion[tabs.Length];
            tabLabels=new TMPro.TMP_Text[tabs.Length];plateImages=new Image[tabs.Length];
            for(int i=0;i<tabs.Length;i++)
            {
                int page=i;var tab=tabs[i];
                motion[i]=new TabMotion();
                tab.onValueChanged=new Toggle.ToggleEvent();tab.group=null;
                tab.transition=Selectable.Transition.None;tab.graphic=null;
                foreach(var sequence in tab.GetComponentsInChildren<VisualSequence>(true))sequence.enabled=false;
                var hit=tab.GetComponent<Image>();if(hit==null)hit=tab.gameObject.AddComponent<Image>();
                hit.color=Color.clear;hit.raycastTarget=true;tab.targetGraphic=hit;
                var background=tab.transform.Find("Background");
                var check=(RectTransform)tab.transform.Find("Checkmark");
                background.gameObject.SetActive(false);
                check.gameObject.SetActive(true);check.anchorMin=check.anchorMax=new Vector2(.5f,.5f);
                check.pivot=new Vector2(.5f,0);check.sizeDelta=new Vector2(286,218);check.anchoredPosition=new Vector2(0,-104);
                var plate=check.GetComponent<Image>();plate.raycastTarget=false;plateImages[i]=plate;
                // At this fixed size use the complete illustrated silhouette, not stretched corners.
                plate.type=Image.Type.Simple;plate.preserveAspect=false;
                var icon=(RectTransform)check.Find("IMG_On");
                icon.SetParent(tab.transform,false);icon.anchorMin=icon.anchorMax=new Vector2(.5f,.5f);
                icon.sizeDelta=new Vector2(140,130);icon.anchoredPosition=Vector2.zero;tabIcons[i]=icon;
                var iconImage=icon.GetComponent<Image>();iconImage.enabled=true;iconImage.color=Color.white;
                foreach(var graphic in icon.GetComponentsInChildren<Graphic>(true)){graphic.canvasRenderer.SetAlpha(1);graphic.raycastTarget=false;}
                var label=icon.Find("TXT_On") as RectTransform;
                if(label!=null){label.anchoredPosition=new Vector2(0,-95);label.localScale=Vector3.one*.8f;}
                tabLabels[i]=icon.GetComponentInChildren<TMPro.TMP_Text>(true);
                tabPlates[i]=check.gameObject.AddComponent<CanvasGroup>();tabPlates[i].blocksRaycasts=false;tabPlates[i].interactable=false;
                tab.onValueChanged.AddListener(value=>{if(value)Select(page);else UpdateTabs();});
            }
            // The store's own vertical scroll remains independent of horizontal page navigation.
            foreach(var scroll in screen.GetComponentsInChildren<ScrollRect>(true))
                if(scroll!=pages && scroll.transform.IsChildOf(pages.content.GetChild(0)))
                {
                    scroll.enabled=true;
                    var surface=scroll.viewport.GetComponent<Image>();
                    if(surface==null)surface=scroll.viewport.gameObject.AddComponent<Image>();
                    surface.color=Color.clear;surface.raycastTarget=true;
                }
            Select(1,false);
            Canvas.willRenderCanvases+=AlignSelectedPage;
        }

        private void AlignSelectedPage()
        {
            // Page widths change with the canvas aspect ratio; the disabled ScrollRect won't retain a tab itself.
            // Do not call ScrollRect.SetNormalizedPosition here: it forces another canvas update when disabled.
            if(pages==null || IsMoving)return;
            var viewport=pages.viewport;var content=pages.content;
            content.GetWorldCorners(contentCorners);
            float min=float.PositiveInfinity,max=float.NegativeInfinity;
            foreach(var corner in contentCorners)
            {
                float x=viewport.InverseTransformPoint(corner).x;min=Mathf.Min(min,x);max=Mathf.Max(max,x);
            }
            float desired=viewport.rect.xMin-SelectedPage*.5f*Mathf.Max(0,max-min-viewport.rect.width);
            var position=content.localPosition;position.x+=desired-min;content.localPosition=position;
        }
        private void OnDestroy()=>Canvas.willRenderCanvases-=AlignSelectedPage;

        public void Select(int page,bool animate=true)
        {
            if(page<0 || page>=tabs.Length || (animate && KitApp.Instance.IsLoading))return;
            if(animate && SelectedPage==page)return;
            SelectedPage=page;UpdateTabs();PageChanged?.Invoke(page);
            for(int i=0;i<tabs.Length;i++)
            {
                var m=motion[i];bool selected=i==page;
                if(animate && m.selected==selected)continue;
                m.selected=selected;m.elapsed=animate?0:1;
                m.fromY=m.y;m.fromPlate=m.plate;m.fromLabel=m.label;m.fromScale=m.scale;
                DrawTab(i);
            }
            if(movement!=null)StopCoroutine(movement);
            Canvas.ForceUpdateCanvases();
            if(animate)movement=StartCoroutine(Move(page*.5f));
            else {pages.horizontalNormalizedPosition=page*.5f;IsMoving=false;}
        }
        private void UpdateTabs()
        {
            for(int i=0;i<tabs.Length;i++)
            {
                bool selected=i==SelectedPage;var tab=tabs[i];tab.SetIsOnWithoutNotify(selected);
            }
        }
        private void Update()
        {
            if(tabs==null)return;
            for(int i=0;i<tabs.Length;i++)
            {
                motion[i].elapsed+=Time.unscaledDeltaTime;
                DrawTab(i);
            }
        }
        private void DrawTab(int i)
        {
            var m=motion[i];float time=m.elapsed;
            if(m.selected)
            {
                m.y=Mathf.LerpUnclamped(m.fromY,49,Rise.Evaluate(Mathf.Clamp01(time/.2f)));
                m.scale=Mathf.LerpUnclamped(m.fromScale,1,Release.Evaluate(Mathf.Clamp01(time/.25f)));
                m.plate=Mathf.Lerp(m.fromPlate,1,1-Mathf.Pow(1-Mathf.Clamp01(time/.25f),2));
                m.label=Mathf.Lerp(m.fromLabel,1,1-Mathf.Pow(1-Mathf.Clamp01((time-.1f)/.2f),2));
            }
            else
            {
                m.y=Mathf.Lerp(m.fromY,0,Mathf.SmoothStep(0,1,time/.1f));
                float t=Mathf.Clamp01((time-.05f)/.15f);
                m.plate=Mathf.Lerp(m.fromPlate,0,t);m.scale=Mathf.Lerp(m.fromScale,.9f,t);
                m.label=Mathf.Lerp(m.fromLabel,0,Mathf.Clamp01(time/.1f));
            }
            plateImages[i].canvasRenderer.SetAlpha(1);
            tabPlates[i].alpha=m.plate;tabPlates[i].transform.localScale=new Vector3(1,m.scale,1);
            tabIcons[i].anchoredPosition=new Vector2(0,m.y);tabIcons[i].localScale=Vector3.one;
            var label=tabLabels[i];
            if(label!=null){var color=label.color;color.a=m.label;label.color=color;}
        }
        private IEnumerator Move(float target)
        {
            IsMoving=true;float from=pages.horizontalNormalizedPosition,time=0;
            while(time<.25f)
            {
                time+=Time.unscaledDeltaTime;
                pages.horizontalNormalizedPosition=Mathf.Lerp(from,target,Mathf.SmoothStep(0,1,time/.25f));
                yield return null;
            }
            pages.horizontalNormalizedPosition=target;IsMoving=false;movement=null;
        }
    }
}
