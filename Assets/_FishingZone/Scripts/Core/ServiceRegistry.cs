using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishingZone.Core
{
    /// <summary>
    /// Minimal lookup for the handful of services that survive scene loads.
    /// It exists so we do not add a static Instance to every manager: objects created by a
    /// scene load cannot be wired to persistent objects in the Inspector, and this keeps that
    /// one problem in one place. It is not a dependency injection container and should not grow into one.
    /// </summary>
    public static class ServiceRegistry
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        /// <summary>
        /// Static state outlives a play session when Enter Play Mode Options disable domain reload,
        /// so the registry is cleared explicitly before each run.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Services.Clear();
        }

        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                GameLog.Error(LogCategory.Boot, $"Refused to register a null {typeof(T).Name}.");
                return;
            }

            if (Services.ContainsKey(typeof(T)))
            {
                GameLog.Warn(LogCategory.Boot, $"{typeof(T).Name} was already registered; the previous instance is being replaced.");
            }

            Services[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class
        {
            Services.Remove(typeof(T));
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object found))
            {
                service = found as T;
                return service != null;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Returns the service or null. Logs an error on miss, because a missing persistent
        /// service always means the Bootstrap scene was skipped or wired incorrectly.
        /// </summary>
        public static T Get<T>() where T : class
        {
            if (TryGet(out T service))
            {
                return service;
            }

            GameLog.Error(LogCategory.Boot, $"{typeof(T).Name} is not registered. Did the game start from the Bootstrap scene?");
            return null;
        }
    }
}
