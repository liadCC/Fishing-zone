using System.Collections.Generic;
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

            GetSpawnPose(clientId, out Vector3 position, out Quaternion rotation);
            NetworkObject instance = Instantiate(_playerPrefab, position, rotation);

            // destroyWithScene keeps the lifetime tied to the gameplay scene, so returning to the
            // menu removes every player without any despawn bookkeeping of our own.
            instance.SpawnAsPlayerObject(clientId, destroyWithScene: true);

            GameLog.Info(LogCategory.Network, $"Spawned player for client {clientId}.");
        }

        /// <summary>
        /// Prefers a spawn point placed in the scene, because world origin is solid ground in the
        /// port but open water in an expedition map, where it drops the crew into the sea.
        /// Falls back to a ring around origin so a scene without points still produces players
        /// rather than nothing at all.
        /// </summary>
        private void GetSpawnPose(ulong clientId, out Vector3 position, out Quaternion rotation)
        {
            IReadOnlyList<PlayerSpawnPoint> points = PlayerSpawnPoint.All;
            if (points.Count > 0)
            {
                // Keyed by client id so every machine agrees on who stands where.
                PlayerSpawnPoint point = points[(int)(clientId % (ulong)points.Count)];
                position = point.transform.position;

                // Flattened, because a spawn point tilted in the scene would otherwise stand the
                // player at an angle.
                Vector3 heading = Vector3.ProjectOnPlane(point.transform.forward, Vector3.up);
                rotation = heading.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(heading, Vector3.up)
                    : Quaternion.identity;
                return;
            }

            GameLog.Warn(LogCategory.Network, "No PlayerSpawnPoint in this scene; falling back to world origin.");

            float angle = clientId * 90f * Mathf.Deg2Rad;
            position = new Vector3(Mathf.Sin(angle) * _spawnRingRadius, 1f, Mathf.Cos(angle) * _spawnRingRadius);
            rotation = Quaternion.identity;
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
