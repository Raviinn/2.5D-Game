using System;
using System.Collections.Generic;
using UnityEngine;

namespace Beast.Core
{
    /// <summary>
    /// Central registry for core services (save, time, input, ...).
    /// Services register once at boot; consumers look them up and cache the result.
    /// </summary>
    public static class Services
    {
        static readonly Dictionary<Type, object> registry = new();

        public static void Register<T>(T service) where T : class
        {
            if (!registry.TryAdd(typeof(T), service))
                Debug.LogWarning($"[Services] {typeof(T).Name} is already registered. Ignoring duplicate.");
        }

        public static void Unregister<T>(T service) where T : class
        {
            if (registry.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, service))
                registry.Remove(typeof(T));
        }

        public static T Get<T>() where T : class
        {
            if (registry.TryGetValue(typeof(T), out var service))
                return (T)service;

            throw new InvalidOperationException(
                $"[Services] {typeof(T).Name} is not registered. Did the Bootstrapper run? (Beast > Setup > Run Milestone 1 Setup)");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (registry.TryGetValue(typeof(T), out var s))
            {
                service = (T)s;
                return true;
            }
            service = null;
            return false;
        }

        // Runs before every Play session, so this works with Domain Reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => registry.Clear();
    }
}
