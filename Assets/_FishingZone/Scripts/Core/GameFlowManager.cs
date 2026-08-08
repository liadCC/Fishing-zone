using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishingZone.Core
{
    /// <summary>
    /// The only object in the project allowed to change scenes.
    /// Everything else asks for a state change and observes <see cref="StateChanged"/>,
    /// which keeps scene loading out of gameplay scripts and gives multiplayer a single
    /// place to switch to a host-driven network scene load later.
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        [SerializeField]
        private SceneCatalog _sceneCatalog;

        /// <summary>Raised after the target scene has finished loading and become active.</summary>
        public event Action<GameState> StateChanged;

        public GameState CurrentState { get; private set; } = GameState.Boot;

        public bool IsTransitioning { get; private set; }

        /// <summary>
        /// Requests a move to <paramref name="target"/>. Illegal or redundant requests are
        /// logged and ignored rather than throwing, so a mis-wired button can never break a session.
        /// </summary>
        public void GoTo(GameState target)
        {
            if (IsTransitioning)
            {
                GameLog.Warn(LogCategory.Flow, $"Ignored transition to {target}: a transition is already in progress.");
                return;
            }

            if (target == CurrentState)
            {
                GameLog.Warn(LogCategory.Flow, $"Ignored transition to {target}: already in that state.");
                return;
            }

            if (!IsTransitionAllowed(CurrentState, target))
            {
                GameLog.Error(LogCategory.Flow, $"Illegal transition {CurrentState} -> {target}.");
                return;
            }

            if (_sceneCatalog == null)
            {
                GameLog.Error(LogCategory.Flow, "No SceneCatalog assigned on GameFlowManager.");
                return;
            }

            string sceneName = _sceneCatalog.GetSceneName(target);
            if (sceneName == null)
            {
                GameLog.Error(LogCategory.Flow, $"SceneCatalog has no scene name for state {target}.");
                return;
            }

            StartCoroutine(TransitionRoutine(target, sceneName));
        }

        /// <summary>
        /// The documented flow. Boot leads only to the main menu; Port is the hub that both
        /// sends the crew out on an expedition and receives them when they return.
        /// </summary>
        private static bool IsTransitionAllowed(GameState from, GameState to)
        {
            switch (from)
            {
                case GameState.Boot:
                    return to == GameState.MainMenu;
                case GameState.MainMenu:
                    return to == GameState.Lobby;
                case GameState.Lobby:
                    return to == GameState.Port || to == GameState.MainMenu;
                case GameState.Port:
                    return to == GameState.Expedition || to == GameState.MainMenu;
                case GameState.Expedition:
                    return to == GameState.Port || to == GameState.MainMenu;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Isolated on purpose: when Netcode is introduced, only this method changes to a
        /// host-authoritative network scene load. Callers and flow rules stay untouched.
        /// </summary>
        private IEnumerator TransitionRoutine(GameState target, string sceneName)
        {
            IsTransitioning = true;
            GameLog.Info(LogCategory.Flow, $"{CurrentState} -> {target} (loading scene '{sceneName}')");

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load == null)
            {
                GameLog.Error(LogCategory.Flow, $"Scene '{sceneName}' could not be loaded. Is it in the Build Profiles scene list?");
                IsTransitioning = false;
                yield break;
            }

            while (!load.isDone)
            {
                yield return null;
            }

            CurrentState = target;
            IsTransitioning = false;
            GameLog.Info(LogCategory.Flow, $"Entered state {target}.");

            StateChanged?.Invoke(target);
        }
    }
}
