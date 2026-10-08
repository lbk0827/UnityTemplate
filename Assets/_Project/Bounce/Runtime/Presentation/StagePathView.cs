using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    public sealed class StagePathView : MonoBehaviour
    {
        private sealed class Slot
        {
            public RectTransform rect;public VisualBindings binding;public CanvasGroup alpha;
            public Vector2 from,to;public float fromScale,toScale;
        }
        private readonly List<Slot> slots=new List<Slot>();
        public bool IsAdvancing { get; private set; }
        private CanvasGroup input;

        public void Initialize(VisualBindings stage)
        {
            var app=KitApp.Instance;
            var content=stage.Get<RectTransform>("levelContentRoot");
            var prefab=stage.Get<GameObject>("levelSlotPrefab");
            if(content==null || prefab==null)return;
            foreach(var layout in content.GetComponents<LayoutGroup>())layout.enabled=false;
            var fitter=content.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            int previous=app.ConsumeStageAdvance();
            IsAdvancing=previous==app.Progress.unlockedLevel-1 && previous>0;
            int first=IsAdvancing?previous:app.Progress.unlockedLevel;
            input=stage.GetComponent<CanvasGroup>();if(input==null)input=stage.gameObject.AddComponent<CanvasGroup>();
            input.interactable=input.blocksRaycasts=!IsAdvancing;
            for(int i=0;i<(IsAdvancing?9:8);i++)
            {
                if((long)first+i>int.MaxValue)break;
                int level=first+i;var go=Instantiate(prefab,content,false);go.name="Stage_Slot_"+level;go.SetActive(true);
                var b=go.GetComponent<VisualBindings>();
                foreach(var key in new[]{"hardText","veryHardText","feverText"})SetActive(b,key,false);
                SetActive(b,"normalText",true);b.Get<TMP_Text>("normalText").text=level.ToString();
                SetActive(b,"dim",i>0);
                var image=b.Get<Image>("levelBackground");image.sprite=b.Get<Sprite>("normalSprite");
                var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);
                var slot=new Slot {rect=rect,binding=b,alpha=go.AddComponent<CanvasGroup>(),from=Position(i,rect),to=Position(IsAdvancing?i-1:i,rect),fromScale=Scale(i),toScale=Scale(IsAdvancing?i-1:i)};
                rect.anchoredPosition=slot.from;rect.localScale=Vector3.one*slot.fromScale;slots.Add(slot);
                foreach(var graphic in go.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                if(level==app.Progress.unlockedLevel)
                {
                    image.raycastTarget=true;var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
                    button.onClick.AddListener(()=>{if(!IsAdvancing && !app.IsLoading)app.Play(level);});
                }
            }
            content.sizeDelta=new Vector2(content.sizeDelta.x,Position(8,(RectTransform)prefab.transform).y);
            if(IsAdvancing)StartCoroutine(Advance());
        }
        private static float Scale(int index)=>index==0?1:.8f;
        private static Vector2 Position(int index,RectTransform rect)
        {
            float height=rect.rect.height;
            float bottom=index<0?204-height-180:index==0?204:204+height+180+(index-1)*(height*.8f+170);
            return new Vector2(0,bottom+height*rect.pivot.y*Scale(index));
        }
        private static void SetActive(VisualBindings binding,string key,bool value)
        {var go=binding.Get<GameObject>(key);if(go!=null)go.SetActive(value);}
        private IEnumerator Advance()
        {
            yield return new WaitForSecondsRealtime(.15f);
            float time=0;
            while(time<.65f)
            {
                time+=Time.unscaledDeltaTime;float t=Mathf.SmoothStep(0,1,time/.65f);
                foreach(var slot in slots)
                {
                    slot.rect.anchoredPosition=Vector2.Lerp(slot.from,slot.to,t);
                    slot.rect.localScale=Vector3.one*Mathf.Lerp(slot.fromScale,slot.toScale,t);
                }
                slots[0].alpha.alpha=1-t;
                yield return null;
            }
            Destroy(slots[0].rect.gameObject);slots.RemoveAt(0);
            SetActive(slots[0].binding,"dim",false);
            IsAdvancing=false;input.interactable=input.blocksRaycasts=true;
        }
    }
}
