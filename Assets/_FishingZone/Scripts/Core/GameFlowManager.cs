using System;
using System.Collections;
using Unity.Netcode;
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
        /// True once a host, server or client is running. Offline play keeps the original local
        /// path, so nothing about single-player flow depends on Netcode being started.
        /// </summary>
        private static bool IsSessionActive =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>Grace period for a followed scene to become active before it is called a failure.</summary>
        private const float FollowActivationTimeout = 5f;

        private bool _isSceneEventHooked;

        /// <summary>
        /// Subscribed in Start rather than OnEnable because NetworkManager assigns its Singleton in
        /// Awake, and every Awake runs before any Start.
        /// </summary>
        private void Start()
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            NetworkManager.Singleton.OnServerStarted += HookSceneEvents;
            NetworkManager.Singleton.OnClientStarted += HookSceneEvents;
            NetworkManager.Singleton.OnServerStopped += HandleSessionStopped;
            NetworkManager.Singleton.OnClientStopped += HandleSessionStopped;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            NetworkManager.Singleton.OnServerStarted -= HookSceneEvents;
            NetworkManager.Singleton.OnClientStarted -= HookSceneEvents;
            NetworkManager.Singleton.OnServerStopped -= HandleSessionStopped;
            NetworkManager.Singleton.OnClientStopped -= HandleSessionStopped;
        }

        /// <summary>
        /// Requests a move to <paramref name="target"/>. Illegal or redundant requests are
        /// logged and ignored rather than throwing, so a mis-wired button can never break a session.
        /// </summary>
        public void GoTo(GameState target)
        {
            // Shared scene changes belong to the host. A client that asks is refused rather than
            // silently desynchronised from the rest of the crew.
            if (IsSessionActive && !NetworkManager.Singleton.IsServer)
            {
                GameLog.Warn(LogCategory.Flow, $"Ignored transition to {target}: only the host changes scene during a session.");
                return;
            }

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
        /// Only the loading mechanism differs between offline and session play. The flow rules,
        /// the callers and the state bookkeeping below are shared by both.
        /// </summary>
        private IEnumerator TransitionRoutine(GameState target, string sceneName)
        {
            IsTransitioning = true;
            GameLog.Info(LogCategory.Flow, $"{CurrentState} -> {target} (loading scene '{sceneName}')");

            IEnumerator load = IsSessionActive
                ? LoadForSession(sceneName)
                : LoadLocally(sceneName);

            while (load.MoveNext())
            {
                yield return load.Current;
            }

            if (!IsTransitioning)
            {
                // The loader already reported a failure and cleared the flag.
                yield break;
            }

            CurrentState = target;
            IsTransitioning = false;
            GameLog.Info(LogCategory.Flow, $"Entered state {target}.");

            StateChanged?.Invoke(target);
        }

        private IEnumerator LoadLocally(string sceneName)
        {
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
        }

        /// <summary>
        /// Netcode loads the scene on the host and every connected client. It reports completion
        /// through scene events rather than an AsyncOperation, so the local scene becoming active is
        /// what is waited on here.
        /// </summary>
        private IEnumerator LoadForSession(string sceneName)
        {
            NetworkSceneManager sceneManager = NetworkManager.Singleton.SceneManager;
            if (sceneManager == null)
            {
                GameLog.Error(LogCategory.Flow, "Scene management is disabled on the NetworkManager, so the host cannot move the crew between scenes.");
                IsTransitioning = false;
                yield break;
            }

            SceneEventProgressStatus status = sceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                GameLog.Error(LogCategory.Flow, $"Networked load of '{sceneName}' was refused: {status}.");
                IsTransitioning = false;
                yield break;
            }

            while (SceneManager.GetActiveScene().name != sceneName)
            {
                yield return null;
            }
        }

        private void HookSceneEvents()
        {
            // Host raises both started callbacks, so this has to tolerate being called twice.
            if (_isSceneEventHooked || NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null)
            {
                return;
            }

            NetworkManager.Singleton.SceneManager.OnLoadComplete += HandleNetworkLoadComplete;
            _isSceneEventHooked = true;
        }

        private void HandleSessionStopped(bool isHost)
        {
            // Netcode disposes its scene manager on shutdown and builds a new one next time, so the
            // subscription is simply marked stale rather than removed from a dead object.
            _isSceneEventHooked = false;
        }

        /// <summary>
        /// Keeps a client's own idea of the flow in step with the host that is driving it.
        /// The host sets its state in <see cref="TransitionRoutine"/> and must not do it twice.
        /// </summary>
        private void HandleNetworkLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            if (NetworkManager.Singleton == null
                || NetworkManager.Singleton.IsServer
                || clientId != NetworkManager.Singleton.LocalClientId)
            {
                return;
            }

            if (!TryGetStateForScene(sceneName, out GameState state) || state == CurrentState)
            {
                return;
            }

            StartCoroutine(FollowHostRoutine(state, sceneName));
        }

        /// <summary>
        /// Adopts the host's scene, but only once this client is genuinely in it.
        ///
        /// The scene event names a scene; it is not proof that the scene became the active one. A
        /// scene that finished loading without being activated leaves the previous one on screen,
        /// and reporting the state from the event alone turned that into a silent lie: the log said
        /// the crew had moved while the player was still standing in the old level.
        /// </summary>
        private IEnumerator FollowHostRoutine(GameState state, string sceneName)
        {
            Scene loaded = SceneManager.GetSceneByName(sceneName);
            if (loaded.IsValid() && loaded.isLoaded && SceneManager.GetActiveScene() != loaded)
            {
                // Loaded but never activated, which is the case that produced the mismatch.
                SceneManager.SetActiveScene(loaded);
            }

            // A short grace period, because activation can land a frame or two after the event.
            float remaining = FollowActivationTimeout;
            while (SceneManager.GetActiveScene().name != sceneName && remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (SceneManager.GetActiveScene().name != sceneName)
            {
                GameLog.Error(LogCategory.Flow,
                    $"Netcode reported '{sceneName}' loaded, but the active scene is still '{SceneManager.GetActiveScene().name}'. " +
                    $"State stays {CurrentState}. Check that '{sceneName}' is in the build list on this client.");
                yield break;
            }

            CurrentState = state;
            IsTransitioning = false;
            GameLog.Info(LogCategory.Flow, $"Followed the host into {state}.");

            StateChanged?.Invoke(state);
        }

        /// <summary>
        /// Reverses the catalog lookup by asking it about each state in turn, which keeps
        /// SceneCatalog itself unchanged and still free of any knowledge of networking.
        /// </summary>
        private bool TryGetStateForScene(string sceneName, out GameState state)
        {
            if (_sceneCatalog != null)
            {
                foreach (GameState candidate in Enum.GetValues(typeof(GameState)))
                {
                    if (_sceneCatalog.GetSceneName(candidate) == sceneName)
                    {
                        state = candidate;
                        return true;
                    }
                }
            }

            state = GameState.Boot;
            return false;
        }
    }
}
