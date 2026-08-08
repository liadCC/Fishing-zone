using FishingZone.Core.Input;
using UnityEngine;

namespace FishingZone.Core.DevTools
{
    /// <summary>
    /// Right-click this component in the Inspector to drive the game flow and input context
    /// before any menus or gameplay exist.
    /// The class is not wrapped in a UNITY_EDITOR guard on purpose: excluding it from a build
    /// would leave a missing-script reference in the Bootstrap scene. Its context menu entries
    /// are Editor-only features, so it is inert at runtime and nothing else depends on it.
    /// </summary>
    public class GameFlowDevMenu : MonoBehaviour
    {
        [SerializeField]
        private GameFlowManager _gameFlow;

        [SerializeField]
        private GameInput _gameInput;

        [ContextMenu("Flow/Go To Main Menu")]
        private void GoToMainMenu() => Request(GameState.MainMenu);

        [ContextMenu("Flow/Go To Lobby")]
        private void GoToLobby() => Request(GameState.Lobby);

        [ContextMenu("Flow/Go To Port")]
        private void GoToPort() => Request(GameState.Port);

        [ContextMenu("Flow/Go To Expedition")]
        private void GoToExpedition() => Request(GameState.Expedition);

        [ContextMenu("Flow/Log Current State")]
        private void LogCurrentState()
        {
            if (_gameFlow != null)
            {
                GameLog.Info(LogCategory.Flow, $"Current state is {_gameFlow.CurrentState}.");
            }
        }

        [ContextMenu("Input/Switch Exclusively To Player")]
        private void SwitchToPlayer() => Exclusive(InputMap.Player);

        [ContextMenu("Input/Switch Exclusively To Boat")]
        private void SwitchToBoat() => Exclusive(InputMap.Boat);

        [ContextMenu("Input/Switch Exclusively To Fishing")]
        private void SwitchToFishing() => Exclusive(InputMap.Fishing);

        [ContextMenu("Input/Add Player Map")]
        private void AddPlayer() => Add(InputMap.Player);

        [ContextMenu("Input/Add Boat Map")]
        private void AddBoat() => Add(InputMap.Boat);

        [ContextMenu("Input/Add Fishing Map")]
        private void AddFishing() => Add(InputMap.Fishing);

        [ContextMenu("Input/Add UI Map")]
        private void AddUI() => Add(InputMap.UI);

        [ContextMenu("Input/Remove Player Map")]
        private void RemovePlayer() => Remove(InputMap.Player);

        [ContextMenu("Input/Remove Boat Map")]
        private void RemoveBoat() => Remove(InputMap.Boat);

        [ContextMenu("Input/Remove Fishing Map")]
        private void RemoveFishing() => Remove(InputMap.Fishing);

        [ContextMenu("Input/Remove UI Map")]
        private void RemoveUI() => Remove(InputMap.UI);

        [ContextMenu("Input/Disable All Maps")]
        private void DisableAllMaps()
        {
            if (ResolveInput() != null)
            {
                _gameInput.DisableAllMaps();
            }
        }

        [ContextMenu("Input/Log Active Maps")]
        private void LogActiveMaps()
        {
            if (ResolveInput() == null)
            {
                return;
            }

            GameLog.Info(LogCategory.Input, _gameInput.ActiveMaps.Count == 0
                ? "Active maps: none"
                : $"Active maps: {string.Join(", ", _gameInput.ActiveMaps)}");
        }

        private void Request(GameState state)
        {
            if (!Application.isPlaying)
            {
                GameLog.Warn(LogCategory.Flow, "Enter Play Mode before using the dev menu.");
                return;
            }

            if (_gameFlow == null)
            {
                GameLog.Error(LogCategory.Flow, "GameFlowDevMenu has no GameFlowManager assigned.");
                return;
            }

            _gameFlow.GoTo(state);
        }

        private void Exclusive(InputMap map)
        {
            if (ResolveInput() != null)
            {
                _gameInput.SwitchTo(map);
            }
        }

        private void Add(InputMap map)
        {
            if (ResolveInput() != null)
            {
                _gameInput.EnableMap(map);
            }
        }

        private void Remove(InputMap map)
        {
            if (ResolveInput() != null)
            {
                _gameInput.DisableMap(map);
            }
        }

        private GameInput ResolveInput()
        {
            if (!Application.isPlaying)
            {
                GameLog.Warn(LogCategory.Input, "Enter Play Mode before using the dev menu.");
                return null;
            }

            if (_gameInput == null)
            {
                GameLog.Error(LogCategory.Input, "GameFlowDevMenu has no GameInput assigned.");
                return null;
            }

            return _gameInput;
        }
    }
}
