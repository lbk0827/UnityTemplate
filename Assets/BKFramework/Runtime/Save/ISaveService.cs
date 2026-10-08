namespace BK.Save
{
    /// <summary>
    /// Typed save slots. One file per <see cref="SaveData"/> subclass, loaded on first
    /// access and written back when dirty: periodically, on pause, on quit, on dispose,
    /// or when <see cref="Flush"/> is called.
    /// </summary>
    public interface ISaveService
    {
        /// <summary>Loads (once) and returns the slot. Never null; a missing or corrupt file yields defaults.</summary>
        T Get<T>() where T : SaveData, new();

        /// <summary>Writes every dirty slot. Returns false if any write failed; those slots stay dirty.</summary>
        bool Flush();

        bool HasUnsavedChanges { get; }
    }
}
