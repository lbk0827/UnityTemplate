using UnityEngine;

namespace BK.Kit.Editor
{
    // Separate script asset keeps the unsaved authoring document identifiable across domain reloads.
    public sealed class BounceLevelDraft : ScriptableObject
    {
        public BounceModule.Levels library;
    }
}
