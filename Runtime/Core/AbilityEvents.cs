using System;
using System.Collections.Generic;

namespace Juris.Abilities
{
    public delegate void AbilityEventHandler<T>(in T evt) where T : struct;

    /// <summary>Typed struct event bus. Publishing never boxes; subscribing allocates once per handler.</summary>
    public sealed class AbilityEventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(AbilityEventHandler<T> handler) where T : struct
        {
            _handlers.TryGetValue(typeof(T), out var existing);
            _handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public void Unsubscribe<T>(AbilityEventHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var existing)) return;
            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) _handlers.Remove(typeof(T));
            else _handlers[typeof(T)] = remaining;
        }

        public void Publish<T>(in T evt) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var handler))
                ((AbilityEventHandler<T>)handler)(in evt);
        }
    }

    public readonly struct AbilityActivated
    {
        public readonly AbilityInstance Ability;
        public AbilityActivated(AbilityInstance ability) => Ability = ability;
    }

    public readonly struct AbilityActivationFailed
    {
        public readonly AbilityInstance Ability;
        public readonly AbilityActivationResult Reason;

        public AbilityActivationFailed(AbilityInstance ability, AbilityActivationResult reason)
        {
            Ability = ability;
            Reason = reason;
        }
    }

    public readonly struct AbilityEnded
    {
        public readonly AbilityInstance Ability;
        public readonly bool WasCancelled;
        public readonly AbilityCancelReason CancelReason;

        public AbilityEnded(AbilityInstance ability, bool wasCancelled, AbilityCancelReason cancelReason)
        {
            Ability = ability;
            WasCancelled = wasCancelled;
            CancelReason = cancelReason;
        }
    }
}
