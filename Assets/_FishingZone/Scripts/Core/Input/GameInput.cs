using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FishingZone.Core.Input
{
    /// <summary>
    /// Owns the project's single Input Actions asset and tracks which Action Maps are enabled.
    /// Any combination of maps may be live at once: whether Player input should coexist with Boat,
    /// Fishing or UI input is a gameplay decision belonging to the system that owns the situation,
    /// not something this class is in a position to decide.
    /// Gameplay scripts read actions through serialized InputActionReference fields; they never poll
    /// keys and never enable maps themselves, so control context stays in one place.
    /// </summary>
    public class GameInput : MonoBehaviour
    {
        [SerializeField]
        private InputActionAsset _actions;

        private readonly HashSet<InputMap> _activeMaps = new HashSet<InputMap>();

        /// <summary>Read-only view of every map currently enabled.</summary>
        public IReadOnlyCollection<InputMap> ActiveMaps => _activeMaps;

        /// <summary>Raised after any change to the set of enabled maps. Listeners query <see cref="IsMapEnabled"/>.</summary>
        public event Action ActiveMapsChanged;

        /// <summary>Exposed so systems can resolve actions when an Inspector reference is impractical.</summary>
        public InputActionAsset Actions => _actions;

        private void Awake()
        {
            if (_actions == null)
            {
                GameLog.Error(LogCategory.Input, "No Input Actions asset assigned on GameInput. Assign FishingZoneControls in the Inspector.");
                return;
            }

            // Start with everything off so the first explicit call defines the context.
            _actions.Disable();
        }

        private void OnDisable()
        {
            if (_actions != null)
            {
                _actions.Disable();
            }

            _activeMaps.Clear();
        }

        public bool IsMapEnabled(InputMap map)
        {
            return _activeMaps.Contains(map);
        }

        /// <summary>
        /// Enables one map without touching any other. This is the additive path that lets
        /// contextual input layer on top of what is already live.
        /// </summary>
        public void EnableMap(InputMap map)
        {
            if (ApplyMap(map, true))
            {
                LogActiveMaps();
                ActiveMapsChanged?.Invoke();
            }
        }

        /// <summary>Disables one map without touching any other.</summary>
        public void DisableMap(InputMap map)
        {
            if (ApplyMap(map, false))
            {
                LogActiveMaps();
                ActiveMapsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Replaces the whole set in one operation: everything listed is enabled, everything else disabled.
        /// </summary>
        public void SetActiveMaps(params InputMap[] maps)
        {
            if (_actions == null)
            {
                return;
            }

            bool changed = false;

            foreach (InputMap map in Enum.GetValues(typeof(InputMap)))
            {
                bool shouldBeEnabled = maps != null && Array.IndexOf(maps, map) >= 0;
                changed |= ApplyMap(map, shouldBeEnabled);
            }

            if (changed)
            {
                LogActiveMaps();
                ActiveMapsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Convenience for the common exclusive case: make <paramref name="map"/> the only live map.
        /// This is a caller-chosen policy, not a rule enforced by GameInput.
        /// </summary>
        public void SwitchTo(InputMap map)
        {
            SetActiveMaps(map);
        }

        public void DisableAllMaps()
        {
            SetActiveMaps();
        }

        /// <summary>Returns true when the map's enabled state actually changed.</summary>
        private bool ApplyMap(InputMap map, bool enable)
        {
            if (_actions == null || _activeMaps.Contains(map) == enable)
            {
                return false;
            }

            InputActionMap target = FindMap(map);
            if (target == null)
            {
                return false;
            }

            if (enable)
            {
                target.Enable();
                _activeMaps.Add(map);
            }
            else
            {
                target.Disable();
                _activeMaps.Remove(map);
            }

            return true;
        }

        private void LogActiveMaps()
        {
            GameLog.Info(LogCategory.Input, _activeMaps.Count == 0
                ? "Active maps: none"
                : $"Active maps: {string.Join(", ", _activeMaps)}");
        }

        private InputActionMap FindMap(InputMap map)
        {
            InputActionMap found = _actions.FindActionMap(map.ToString(), throwIfNotFound: false);
            if (found == null)
            {
                GameLog.Error(LogCategory.Input, $"Action Map '{map}' was not found in {_actions.name}.");
            }

            return found;
        }
    }
}
