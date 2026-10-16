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
        private float[] focus;
        private TMPro.TMP_Text[] tabLabels;
        private Image[] plateImages;
        private readonly Vector3[] contentCorners=new Vector3[4];

        public void Initialize(GameObject screen, VisualBindings hud)
        {
            pages=screen.GetComponentsInChildren<ScrollRect>(true).First(s=>s.name=="svl_lobby");
            pages.enabled=false;
            tabs=new[]{hud.Get<Toggle>("StoreToggle"),hud.Get<Toggle>("HomeToggle"),hud.Get<Toggle>("ContentToggle")};
            tabIcons=new RectTransform[tabs.Length];tabPlates=new CanvasGroup[tabs.Length];focus=new float[tabs.Length];
            tabLabels=new TMPro.TMP_Text[tabs.Length];plateImages=new Image[tabs.Length];
            for(int i=0;i<tabs.Length;i++)
            {
                int page=i;var tab=tabs[i];
                tab.onValueChanged=new Toggle.ToggleEvent();tab.group=null;
                tab.transition=Selectable.Transition.None;tab.graphic=null;
                foreach(var sequence in tab.GetComponentsInChildren<VisualSequence>(true))sequence.enabled=false;
                var hit=tab.GetComponent<Image>();if(hit==null)hit=tab.gameObject.AddComponent<Image>();
                hit.color=Color.clear;hit.raycastTarget=true;tab.targetGraphic=hit;
                var background=tab.transform.Find("Background");
                var check=(RectTransform)tab.transform.Find("Checkmark");
                background.gameObject.SetActive(false);
                check.gameObject.SetActive(true);check.anchorMin=check.anchorMax=new Vector2(.5f,.5f);
                check.pivot=new Vector2(.5f,.5f);check.sizeDelta=new Vector2(286,218);check.anchoredPosition=new Vector2(0,5);
                var plate=check.GetComponent<Image>();plate.raycastTarget=false;plateImages[i]=plate;
                var icon=(RectTransform)check.Find("IMG_On");
                icon.SetParent(tab.transform,false);icon.anchorMin=icon.anchorMax=new Vector2(.5f,.5f);
                icon.sizeDelta=new Vector2(128,120);icon.anchoredPosition=new Vector2(0,10);tabIcons[i]=icon;
                var iconImage=icon.GetComponent<Image>();iconImage.enabled=true;iconImage.color=Color.white;
                foreach(var graphic in icon.GetComponentsInChildren<Graphic>(true)){graphic.canvasRenderer.SetAlpha(1);graphic.raycastTarget=false;}
                var label=icon.Find("TXT_On") as RectTransform;
                if(label!=null){label.anchoredPosition=new Vector2(0,-82);label.localScale=Vector3.one*.8f;}
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
            SelectedPage=page;UpdateTabs();PageChanged?.Invoke(page);
            if(!animate)for(int i=0;i<tabs.Length;i++){focus[i]=i==SelectedPage?1:0;DrawTab(i);}
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
                focus[i]=Mathf.MoveTowards(focus[i],i==SelectedPage?1:0,Time.unscaledDeltaTime/.22f);
                DrawTab(i);
            }
        }
        private void DrawTab(int i)
        {
            float t=Mathf.SmoothStep(0,1,focus[i]);
            plateImages[i].canvasRenderer.SetAlpha(1);
            tabPlates[i].alpha=t;tabIcons[i].anchoredPosition=new Vector2(0,Mathf.Lerp(0,25,t));
            tabIcons[i].localScale=Vector3.one*Mathf.Lerp(.92f,1.05f,t);
            var label=tabLabels[i];
            if(label!=null){var color=label.color;color.a=t;label.color=color;}
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
