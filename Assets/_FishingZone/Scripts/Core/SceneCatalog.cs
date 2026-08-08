using System;
using UnityEngine;

namespace FishingZone.Core
{
    /// <summary>
    /// Maps a <see cref="GameState"/> to the scene that represents it.
    /// Scene names live in data so renaming or re-targeting a scene never requires a code change,
    /// and so no system other than GameFlowManager ever needs to know a scene name at all.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneCatalog", menuName = "Fishing Zone/Core/Scene Catalog")]
    public class SceneCatalog : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public GameState State;
            public string SceneName;
        }

        [SerializeField]
        private Entry[] _entries = Array.Empty<Entry>();

        /// <summary>
        /// Returns the scene name for a state, or null if the catalog has no entry for it.
        /// Callers are expected to treat null as a configuration error, not a normal case.
        /// </summary>
        public string GetSceneName(GameState state)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].State == state)
                {
                    return string.IsNullOrWhiteSpace(_entries[i].SceneName) ? null : _entries[i].SceneName;
                }
            }

            return null;
        }
    }
}
