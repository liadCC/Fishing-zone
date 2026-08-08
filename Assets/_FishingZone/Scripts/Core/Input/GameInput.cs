using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FishingZone.Core.Input
{
    /// <summary>
    /// Owns the project's single Input Actions asset and decides which Action Map is live.
    /// Gameplay scripts read actions through serialized InputActionReference fields; they never
    /// poll keys and never enable maps themselves, so control context stays in one place.
    /// </summary>
    public class GameInput : MonoBehaviour
    {
        [SerializeField]
        private InputActionAsset _actions;

        /// <summary>
        /// Exactly one gameplay map is active at a time: standing on deck, steering and fishing
        /// are mutually exclusive contexts, which is also what stops two systems reading the same key.
        /// </summary>
        public InputMap? ActiveGameplayMap { get; private set; }

        public bool IsUIEnabled { get; private set; }

        public event Action<InputMap?> ActiveGameplayMapChanged;

        /// <summary>Exposed so systems can resolve actions when an Inspector reference is impractical.</summary>
        public InputActionAsset Actions => _actions;

        private void Awake()
        {
            if (_actions == null)
            {
                GameLog.Error(LogCategory.Input, "No Input Actions asset assigned on GameInput. Assign FishingZoneControls in the Inspector.");
                return;
            }

            // Start with everything off so the first explicit SwitchTo defines the context.
            _actions.Disable();
        }

        private void OnDisable()
        {
            if (_actions != null)
            {
                _actions.Disable();
            }
        }

        /// <summary>
        /// Makes <paramref name="map"/> the active gameplay map and disables the previous one.
        /// Passing <see cref="InputMap.UI"/> here is a mistake; use <see cref="SetUIEnabled"/> instead,
        /// because UI input is layered on top of gameplay rather than replacing it.
        /// </summary>
        public void SwitchTo(InputMap map)
        {
            if (map == InputMap.UI)
            {
                GameLog.Error(LogCategory.Input, "SwitchTo(UI) is not valid. Use SetUIEnabled to toggle the UI map.");
                return;
            }

            if (_actions == null)
            {
                return;
            }

            if (ActiveGameplayMap.HasValue)
            {
                FindMap(ActiveGameplayMap.Value)?.Disable();
            }

            InputActionMap target = FindMap(map);
            if (target == null)
            {
                ActiveGameplayMap = null;
                ActiveGameplayMapChanged?.Invoke(null);
                return;
            }

            target.Enable();
            ActiveGameplayMap = map;
            GameLog.Info(LogCategory.Input, $"Active gameplay map: {map}");

            ActiveGameplayMapChanged?.Invoke(map);
        }

        /// <summary>Disables all gameplay input, for example during a scene transition or a cutscene.</summary>
        public void DisableGameplayInput()
        {
            if (_actions == null || !ActiveGameplayMap.HasValue)
            {
                return;
            }

            FindMap(ActiveGameplayMap.Value)?.Disable();
            ActiveGameplayMap = null;
            GameLog.Info(LogCategory.Input, "Gameplay input disabled.");

            ActiveGameplayMapChanged?.Invoke(null);
        }

        public void SetUIEnabled(bool enabled)
        {
            if (_actions == null)
            {
                return;
            }

            InputActionMap ui = FindMap(InputMap.UI);
            if (ui == null)
            {
                return;
            }

            if (enabled)
            {
                ui.Enable();
            }
            else
            {
                ui.Disable();
            }

            IsUIEnabled = enabled;
            GameLog.Info(LogCategory.Input, $"UI map {(enabled ? "enabled" : "disabled")}.");
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
