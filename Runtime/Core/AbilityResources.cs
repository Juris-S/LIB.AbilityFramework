using System;
using System.Collections.Generic;

namespace Juris.Abilities
{
    /// <summary>
    /// Per-owner spendable values (ultimate charge, mana...), keyed by tag, e.g. "Resource.Ultimate".
    /// Abilities spend them through their definition's cost; game code fills them (damage dealt, time...).
    /// </summary>
    public sealed class AbilityResources
    {
        private sealed class Pool
        {
            public GameplayTag Resource;
            public float Current;
            public float Max;
        }

        private readonly List<Pool> _pools = new List<Pool>();
        private readonly AbilityEventBus _events;

        internal AbilityResources(AbilityEventBus events) => _events = events;

        /// <summary>Adds a resource, or changes its max (current is clamped). Starts empty unless <paramref name="current"/> is given.</summary>
        public void Define(GameplayTag resource, float max, float current = 0f)
        {
            if (!resource.IsValid) throw new ArgumentException("Resource tag is invalid.", nameof(resource));
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max), "Max must be positive.");

            var pool = Find(resource);
            if (pool == null)
            {
                pool = new Pool { Resource = resource };
                _pools.Add(pool);
            }
            pool.Max = max;
            Set(pool, current);
        }

        public bool Has(GameplayTag resource) => Find(resource) != null;

        /// <summary>Current value, or 0 if the resource is not defined.</summary>
        public float GetCurrent(GameplayTag resource) => Find(resource)?.Current ?? 0f;

        /// <summary>Max value, or 0 if the resource is not defined.</summary>
        public float GetMax(GameplayTag resource) => Find(resource)?.Max ?? 0f;

        /// <summary>Current / max in 0..1, for meters. 0 if the resource is not defined.</summary>
        public float GetFraction(GameplayTag resource)
        {
            var pool = Find(resource);
            return pool == null ? 0f : pool.Current / pool.Max;
        }

        public bool CanAfford(GameplayTag resource, float amount)
        {
            if (amount <= 0f) return true;
            var pool = Find(resource);
            return pool != null && pool.Current >= amount;
        }

        /// <summary>Adds (or removes, if negative) and clamps to 0..max. Returns the amount actually applied.</summary>
        public float Add(GameplayTag resource, float amount)
        {
            var pool = Find(resource);
            if (pool == null || amount == 0f) return 0f;
            float before = pool.Current;
            Set(pool, before + amount);
            return pool.Current - before;
        }

        public bool TrySpend(GameplayTag resource, float amount)
        {
            if (!CanAfford(resource, amount)) return false;
            if (amount > 0f) Add(resource, -amount);
            return true;
        }

        private void Set(Pool pool, float value)
        {
            float before = pool.Current;
            pool.Current = Math.Min(Math.Max(value, 0f), pool.Max);
            if (pool.Current != before)
                _events.Publish(new AbilityResourceChanged(pool.Resource, pool.Current, pool.Max, pool.Current - before));
        }

        private Pool Find(GameplayTag resource)
        {
            for (int i = 0; i < _pools.Count; i++)
                if (_pools[i].Resource == resource) return _pools[i];
            return null;
        }
    }

    public readonly struct AbilityResourceChanged
    {
        public readonly GameplayTag Resource;
        public readonly float Current;
        public readonly float Max;
        public readonly float Delta;

        public AbilityResourceChanged(GameplayTag resource, float current, float max, float delta)
        {
            Resource = resource;
            Current = current;
            Max = max;
            Delta = delta;
        }
    }
}
