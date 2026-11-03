using System;
using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>Who dealt damage and how much, after modifiers (see <see cref="DamageService"/>).</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly int SourceId;

        public DamageInfo(float amount, int sourceId)
        {
            Amount = amount;
            SourceId = sourceId;
        }
    }

    /// <summary>Minimal health attribute. Raises events; presentation and respawn logic live in the game.</summary>
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float max = 200f;

        public float Max => max;
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;

        /// <summary>Armour, damage reduction... Applied by <see cref="DamageService"/> after the attacker's outgoing modifiers.</summary>
        public DamageModifiers IncomingDamage { get; } = new DamageModifiers();

        public event Action<Health, DamageInfo> Damaged;
        public event Action<Health, DamageInfo> Died;
        public event Action<Health> Healed;

        private void Awake() => Current = max;

        /// <summary>Raw damage, no modifiers. Abilities should go through <see cref="DamageService.Deal"/>.</summary>
        public void ApplyDamage(in DamageInfo damage)
        {
            if (!IsAlive || damage.Amount <= 0f) return;
            Current = Mathf.Max(0f, Current - damage.Amount);
            Damaged?.Invoke(this, damage);
            if (Current <= 0f) Died?.Invoke(this, damage);
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Current = Mathf.Min(max, Current + amount);
            Healed?.Invoke(this);
        }

        /// <summary>Full health, alive again. For respawns.</summary>
        public void ResetToFull()
        {
            Current = max;
            Healed?.Invoke(this);
        }
    }
}
