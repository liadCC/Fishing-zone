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

        [ContextMenu("Input/Switch To Player Map")]
        private void SwitchToPlayer() => Switch(InputMap.Player);

        [ContextMenu("Input/Switch To Boat Map")]
        private void SwitchToBoat() => Switch(InputMap.Boat);

        [ContextMenu("Input/Switch To Fishing Map")]
        private void SwitchToFishing() => Switch(InputMap.Fishing);

        [ContextMenu("Input/Disable Gameplay Input")]
        private void DisableGameplayInput()
        {
            if (_gameInput != null)
            {
                _gameInput.DisableGameplayInput();
            }
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

        private void Switch(InputMap map)
        {
            if (!Application.isPlaying)
            {
                GameLog.Warn(LogCategory.Input, "Enter Play Mode before using the dev menu.");
                return;
            }

            if (_gameInput == null)
            {
                GameLog.Error(LogCategory.Input, "GameFlowDevMenu has no GameInput assigned.");
                return;
            }

            _gameInput.SwitchTo(map);
        }
    }
}
