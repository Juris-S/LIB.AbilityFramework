using System.Collections.Generic;

namespace Juris.Abilities
{
    public enum DamageModifierOp
    {
        /// <summary>Flat amount added (negative to reduce). Applied before every multiplier.</summary>
        Add,
        /// <summary>Factor, e.g. 1.3 for +30% or 0.5 for half. Multipliers multiply with each other.</summary>
        Multiply,
    }

    /// <summary>
    /// A stack of damage modifiers (damage boosts, armour, damage reduction).
    /// Resolves as (amount + every Add) × every Multiply, clamped at 0. Order of adding never matters.
    /// </summary>
    public sealed class DamageModifiers
    {
        private struct Entry
        {
            public int Handle;
            public DamageModifierOp Op;
            public float Value;
        }

        private readonly List<Entry> _entries = new List<Entry>(4);
        private int _nextHandle = 1;

        public int Count => _entries.Count;

        /// <summary>Returns a handle for <see cref="Remove"/>.</summary>
        public int Add(DamageModifierOp op, float value)
        {
            int handle = _nextHandle++;
            _entries.Add(new Entry { Handle = handle, Op = op, Value = value });
            return handle;
        }

        public bool Remove(int handle)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Handle != handle) continue;
                _entries.RemoveAt(i);
                return true;
            }
            return false;
        }

        public void Clear() => _entries.Clear();

        public float Apply(float amount)
        {
            float flat = 0f, factor = 1f;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Op == DamageModifierOp.Add) flat += entry.Value;
                else factor *= entry.Value;
            }
            float result = (amount + flat) * factor;
            return result > 0f ? result : 0f;
        }
    }
}
