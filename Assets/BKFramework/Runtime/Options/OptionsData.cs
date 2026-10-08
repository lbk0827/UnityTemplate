using System;
using BK.Save;

namespace BK.Options
{
    [Serializable]
    public sealed class OptionsData : SaveData
    {
        public override int CurrentVersion => 1;
        public bool music = true;
        public bool sfx = true;
        public bool haptics = true;
        /// <summary>Empty means "use the project default".</summary>
        public string language = "";
    }
}
