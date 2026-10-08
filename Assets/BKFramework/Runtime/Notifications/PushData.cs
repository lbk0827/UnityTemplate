using System;
using BK.Save;

namespace BK.Notifications
{
    [Serializable]
    public sealed class PushData : SaveData
    {
        public override int CurrentVersion => 1;
        /// <summary>The OS prompt was shown once; it is never shown again by us.</summary>
        public bool permissionRequested;
        public bool permissionGranted;
        /// <summary>Game code asked for the prompt; consumed by the next TryRequestPendingPermissionAsync.</summary>
        public bool permissionPending;
    }
}
