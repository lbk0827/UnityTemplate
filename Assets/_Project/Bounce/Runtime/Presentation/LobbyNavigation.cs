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
        private readonly Vector3[] contentCorners=new Vector3[4];

        public void Initialize(GameObject screen, VisualBindings hud)
        {
            pages=screen.GetComponentsInChildren<ScrollRect>(true).First(s=>s.name=="svl_lobby");
            pages.enabled=false;
            tabs=new[]{hud.Get<Toggle>("StoreToggle"),hud.Get<Toggle>("HomeToggle"),hud.Get<Toggle>("ContentToggle")};
            for(int i=0;i<tabs.Length;i++)
            {
                int page=i;var tab=tabs[i];
                tab.onValueChanged=new Toggle.ToggleEvent();tab.group=null;
                tab.transition=Selectable.Transition.None;tab.graphic=null;
                foreach(var sequence in tab.GetComponentsInChildren<VisualSequence>(true))sequence.enabled=false;
                var hit=tab.GetComponent<Image>();if(hit==null)hit=tab.gameObject.AddComponent<Image>();
                hit.color=Color.clear;hit.raycastTarget=true;tab.targetGraphic=hit;
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
                var background=tab.transform.Find("Background");var check=tab.transform.Find("Checkmark");
                if(background!=null)background.gameObject.SetActive(!selected);
                if(check!=null)
                {
                    check.gameObject.SetActive(selected);
                    foreach(var graphic in check.GetComponentsInChildren<Graphic>(true))
                    {var color=graphic.color;color.a=1;graphic.color=color;}
                }
            }
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
