namespace Beast.Core
{
    /// <summary>
    /// Anything with state that must survive save/load.
    /// Register with SaveService when enabled, unregister when disabled.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Unique and stable across sessions and builds. Never change it after release.</summary>
        string SaveId { get; }

        /// <summary>Return this object's state as JSON (JsonUtility on a small [Serializable] struct works well).</summary>
        string CaptureState();

        void RestoreState(string json);
    }
}
