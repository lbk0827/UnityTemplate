using System;
using BK.Save;

namespace BK.Kit
{
    [Serializable]
    public sealed class BounceProfileData : SaveData
    {
        public const string DefaultName = "BK Player";
        public override int CurrentVersion => 1;
        public string playerName = DefaultName;
        public int avatarIndex, frameIndex;
        public System.Collections.Generic.List<string> seenGuides = new System.Collections.Generic.List<string>();
        public override bool Validate() => !string.IsNullOrWhiteSpace(playerName) && playerName.Length <= 24;
    }
}
