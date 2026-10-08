using System;

namespace BK.Save
{
    /// <summary>
    /// One persisted slot. Subclasses are plain [Serializable] field bags that
    /// JsonUtility can round-trip. The service owns loading and writing; a slot
    /// only says what version it is, how to upgrade older files, and whether a
    /// loaded instance is sane.
    /// </summary>
    [Serializable]
    public abstract class SaveData
    {
        /// <summary>Version stamped into the file. Managed by the service.</summary>
        public int version;

        [NonSerialized] private bool _dirty;

        /// <summary>Version the code expects. Bump when fields change meaning.</summary>
        public abstract int CurrentVersion { get; }

        public bool IsDirty => _dirty;
        public void MarkDirty() => _dirty = true;
        internal void ClearDirty() => _dirty = false;

        /// <summary>Called once when no usable file exists. Defaults are already applied.</summary>
        public virtual void OnCreate() { }

        /// <summary>
        /// Called after an older file was populated into this instance. Fields the old
        /// file did not have keep their defaults; fix up semantics here.
        /// </summary>
        public virtual void OnMigrate(int fromVersion) { }

        /// <summary>Return false to treat a loaded file as corrupt (it is preserved, not deleted).</summary>
        public virtual bool Validate() => true;
    }
}
