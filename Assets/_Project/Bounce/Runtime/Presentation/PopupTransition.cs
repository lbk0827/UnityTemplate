using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    // Shared unscaled transition: modal input remains blocked until the close completes.
    public sealed class PopupTransition : MonoBehaviour
    {
        private CanvasGroup group;
        private Transform content;
        private Vector3 scale;
        private bool closing;
        public bool IsClosing => closing;
        public void SetContent(Transform value){content=value;scale=value.localScale;}

        public static PopupTransition Open(GameObject root)
        {
            var view=root.GetComponent<PopupTransition>();if(view==null)view=root.AddComponent<PopupTransition>();
            view.group=root.GetComponent<CanvasGroup>();if(view.group==null)view.group=root.AddComponent<CanvasGroup>();
            view.content=root.transform.Find("Root");if(view.content==null)view.content=root.transform.Find("Target");
            if(view.content!=null)
            {
                if(view.content.localScale==Vector3.zero)view.content.localScale=Vector3.one;
                view.scale=view.content.localScale;
            }
            var blocker=root.GetComponent<Image>();if(blocker==null)blocker=root.AddComponent<Image>();
            blocker.enabled=true;blocker.color=Color.clear;blocker.raycastTarget=true;
            // Native prefabs use different dim hierarchies and alpha tracks. A dedicated
            // first sibling keeps the modal backdrop consistent without tinting its art.
            foreach(var image in root.GetComponentsInChildren<Image>(true))if(image.name=="img_black")image.color=Color.clear;
            var dim=new GameObject("Modal backdrop",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            dim.transform.SetParent(root.transform,false);dim.transform.SetAsFirstSibling();
            dim.rectTransform.anchorMin=Vector2.zero;dim.rectTransform.anchorMax=Vector2.one;
            dim.rectTransform.offsetMin=dim.rectTransform.offsetMax=Vector2.zero;
            dim.color=new Color(.025f,.025f,.075f,.92f);dim.raycastTarget=false;
            view.StartCoroutine(view.Animate(true,null));
            return view;
        }
        public void Close(Action finished)
        {
            if(closing)return;
            closing=true;StopAllCoroutines();StartCoroutine(Animate(false,finished));
        }
        private IEnumerator Animate(bool opening,Action finished)
        {
            float duration=opening?.22f:.14f;
            group.interactable=false;
            float start=group.alpha;
            for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(elapsed/duration),ease=1-Mathf.Pow(1-t,3);
                group.alpha=opening?ease:Mathf.Lerp(start,0,ease);
                if(content!=null)content.localScale=scale*(opening?Mathf.Lerp(.94f,1,ease):Mathf.Lerp(1,.97f,ease));
                yield return null;
            }
            group.alpha=opening?1:0;
            if(content!=null)content.localScale=scale;
            group.interactable=opening;
            finished?.Invoke();
        }
    }
}
