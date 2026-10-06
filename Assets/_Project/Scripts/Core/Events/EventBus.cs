using System;
using System.Collections.Generic;
using UnityEngine;

namespace Beast.Core
{
    /// <summary>Marker for event payloads. Events are structs, so raising them allocates nothing.</summary>
    public interface IEvent { }

    /// <summary>
    /// Type-safe publish/subscribe bus. One static channel per event type.
    /// Always pair Subscribe (OnEnable) with Unsubscribe (OnDisable).
    /// </summary>
    public static class EventBus<T> where T : struct, IEvent
    {
        static Action<T> handlers;

        static EventBus() => EventBusRegistry.Register(Clear);

        public static void Subscribe(Action<T> handler) => handlers += handler;

        public static void Unsubscribe(Action<T> handler) => handlers -= handler;

        public static void Raise(T evt) => handlers?.Invoke(evt);

        static void Clear() => handlers = null;
    }

    static class EventBusRegistry
    {
        static readonly List<Action> clearers = new();

        internal static void Register(Action clear) => clearers.Add(clear);

        // Drops stale listeners from the previous Play session (Domain Reload disabled).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearAll()
        {
            foreach (var clear in clearers) clear();
        }
    }
}
