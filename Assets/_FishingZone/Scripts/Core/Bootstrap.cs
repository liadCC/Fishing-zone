using FishingZone.Core.Input;
using UnityEngine;

namespace FishingZone.Core
{
    /// <summary>
    /// Startup entry point. It makes the persistent services survive scene loads, publishes them,
    /// and hands control to <see cref="GameFlowManager"/>. It deliberately holds no gameplay state:
    /// systems are added to the services object, not to this class.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField]
        private GameFlowManager _gameFlow;

        [SerializeField]
        private GameInput _gameInput;

        /// <summary>Guards against a second Bootstrap scene load creating a duplicate set of services.</summary>
        private static bool _hasInitialized;

        /// <summary>
        /// True only on the instance that actually registered the services, so a duplicate
        /// Bootstrap destroying itself cannot unregister the live ones.
        /// </summary>
        private bool _isServiceOwner;

        // Static state survives play sessions when domain reload is disabled, so it is reset per run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _hasInitialized = false;
        }

        private void Awake()
        {
            if (_hasInitialized)
            {
                GameLog.Warn(LogCategory.Boot, "Services already initialized; destroying the duplicate Bootstrap object.");
                Destroy(gameObject);
                return;
            }

            if (_gameFlow == null || _gameInput == null)
            {
                GameLog.Error(LogCategory.Boot, "Bootstrap is missing a service reference. Assign GameFlowManager and GameInput in the Inspector.");
                return;
            }

            _hasInitialized = true;
            _isServiceOwner = true;
            DontDestroyOnLoad(gameObject);

            ServiceRegistry.Register(_gameFlow);
            ServiceRegistry.Register(_gameInput);

            GameLog.Info(LogCategory.Boot, "Persistent services initialized.");
        }

        private void Start()
        {
            if (!_isServiceOwner)
            {
                return;
            }

            // The menu is driven by UI input only; gameplay maps stay off until a gameplay scene asks for one.
            _gameInput.SetUIEnabled(true);
            _gameFlow.GoTo(GameState.MainMenu);
        }

        private void OnDestroy()
        {
            if (!_isServiceOwner)
            {
                return;
            }

            ServiceRegistry.Unregister<GameFlowManager>();
            ServiceRegistry.Unregister<GameInput>();
            _hasInitialized = false;
        }
    }
}
