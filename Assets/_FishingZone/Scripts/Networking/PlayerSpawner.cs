using FishingZone.Core;
using Unity.Netcode;
using UnityEngine;

namespace FishingZone.Networking
{
    /// <summary>
    /// Creates player objects when the crew reaches a gameplay scene, and only then.
    ///
    /// Netcode's automatic player prefab spawns on connection, which for this project is during the
    /// main menu. A player standing in the menu is not cosmetic: enabling it takes the action map
    /// away from the UI and adds a second audio listener alongside the menu camera. Automatic
    /// creation is therefore switched off and the server spawns explicitly instead.
    ///
    /// Server only. Clients receive the objects through normal replication.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField]
        private GameFlowManager _gameFlow;

        [SerializeField]
        private NetworkObject _playerPrefab;

        /// <summary>Scenes the crew actually plays in. The menu and lobby are deliberately absent.</summary>
        [SerializeField]
        private GameState[] _gameplayStates = { GameState.Port, GameState.Expedition };

        /// <summary>Keeps four players from arriving inside one another and shoving each other apart.</summary>
        [SerializeField]
        private float _spawnRingRadius = 1.5f;

        private void OnEnable()
        {
            if (_gameFlow != null)
            {
                _gameFlow.StateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_gameFlow != null)
            {
                _gameFlow.StateChanged -= HandleStateChanged;
            }
        }

        // Subscribed in Start because NetworkManager assigns its singleton in Awake.
        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            }
        }

        /// <summary>Arriving in a gameplay scene: everyone currently connected needs a body.</summary>
        private void HandleStateChanged(GameState state)
        {
            if (!IsServerReady() || !IsGameplayState(state))
            {
                return;
            }

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                SpawnPlayerFor(clientId);
            }
        }

        /// <summary>Joining while the crew is already out: this client needs a body immediately.</summary>
        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServerReady() || _gameFlow == null || !IsGameplayState(_gameFlow.CurrentState))
            {
                return;
            }

            SpawnPlayerFor(clientId);
        }

        private void SpawnPlayerFor(ulong clientId)
        {
            if (_playerPrefab == null)
            {
                GameLog.Error(LogCategory.Network, "PlayerSpawner has no player prefab assigned.");
                return;
            }

            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
                && client.PlayerObject != null)
            {
                // Already has a body, which happens when a transition and a connection coincide.
                return;
            }

            NetworkObject instance = Instantiate(_playerPrefab, GetSpawnPosition(clientId), Quaternion.identity);

            // destroyWithScene keeps the lifetime tied to the gameplay scene, so returning to the
            // menu removes every player without any despawn bookkeeping of our own.
            instance.SpawnAsPlayerObject(clientId, destroyWithScene: true);

            GameLog.Info(LogCategory.Network, $"Spawned player for client {clientId}.");
        }

        /// <summary>
        /// Spread around a small ring by client id, so the arrangement is the same on every machine
        /// and nobody spawns inside anybody else.
        /// </summary>
        private Vector3 GetSpawnPosition(ulong clientId)
        {
            float angle = clientId * 90f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle) * _spawnRingRadius, 1f, Mathf.Cos(angle) * _spawnRingRadius);
        }

        private bool IsGameplayState(GameState state)
        {
            if (_gameplayStates == null)
            {
                return false;
            }

            for (int i = 0; i < _gameplayStates.Length; i++)
            {
                if (_gameplayStates[i] == state)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsServerReady()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }
    }
}
