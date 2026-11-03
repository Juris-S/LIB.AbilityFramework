using System;
using System.Collections.Generic;

namespace Juris.Abilities
{
    /// <summary>
    /// Per-controller dependencies handed to executions (owner, targeting, motor...).
    /// Games register their own services, so new abilities need no core edits.
    /// </summary>
    public sealed class AbilityServices
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Add<T>(T service) where T : class => _services[typeof(T)] = service;

        public bool TryGet<T>(out T service) where T : class
        {
            bool found = _services.TryGetValue(typeof(T), out var value);
            service = found ? (T)value : null;
            return found;
        }

        public T Get<T>() where T : class =>
            TryGet(out T service)
                ? service
                : throw new InvalidOperationException($"No {typeof(T).Name} registered in AbilityServices.");
    }
}
