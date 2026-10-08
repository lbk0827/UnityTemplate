using UnityEngine;

namespace BK.Kit
{
    public sealed class KitAudioChannel : MonoBehaviour
    {
        public enum Category { Music, Effects }
        public Category Kind { get; private set; }
        public bool IsMuted => source!=null && source.mute;
        private AudioSource source;
        private KitApp app;
        public void Initialize(AudioSource audioSource,Category category)
        {
            if(app!=null)app.Changed-=Refresh;
            source=audioSource;Kind=category;app=KitApp.Instance;
            if(app!=null) {app.Changed+=Refresh;Refresh();}
        }
        private void Refresh()
        {
            if(source!=null)source.mute=Kind==Category.Music?!app.Progress.musicEnabled:!app.Progress.effectsEnabled;
        }
        private void OnDestroy() {if(app!=null)app.Changed-=Refresh;}
    }
}
