namespace Juris.Abilities
{
    /// <summary>
    /// The one path abilities deal damage through: the owner's <see cref="AbilityController.OutgoingDamage"/>,
    /// then the target's <see cref="Health.IncomingDamage"/>, then <see cref="Health.ApplyDamage"/>.
    /// <see cref="AbilitySystem"/> registers one in its services; get it with <c>Services.Get&lt;DamageService&gt;()</c>.
    /// </summary>
    public sealed class DamageService
    {
        private readonly AbilityController _source;
        private readonly ITargetable _owner;

        /// <param name="owner">Gives the source id for <see cref="DamageInfo"/>; read per hit, so it can be set up later. Null = id 0.</param>
        public DamageService(AbilityController source, ITargetable owner = null)
        {
            _source = source;
            _owner = owner;
        }

        /// <summary>Damages a living target. Publishes <see cref="DamageDealt"/> on the source's bus.</summary>
        public DamageResult Deal(Health target, float baseAmount)
        {
            if (target == null || !target.IsAlive) return default;

            float amount = target.IncomingDamage.Apply(_source.OutgoingDamage.Apply(baseAmount));
            float before = target.Current;
            target.ApplyDamage(new DamageInfo(amount, _owner?.Id ?? 0));

            var result = new DamageResult(before - target.Current, !target.IsAlive);
            _source.Events.Publish(new DamageDealt(target, result.Amount, result.Killed));
            return result;
        }
    }

    public readonly struct DamageResult
    {
        /// <summary>Health actually removed (overkill not included).</summary>
        public readonly float Amount;
        public readonly bool Killed;

        public DamageResult(float amount, bool killed)
        {
            Amount = amount;
            Killed = killed;
        }
    }

    /// <summary>Published on the attacker's bus, for hit markers and damage numbers.</summary>
    public readonly struct DamageDealt
    {
        public readonly Health Target;
        public readonly float Amount;
        public readonly bool Killed;

        public DamageDealt(Health target, float amount, bool killed)
        {
            Target = target;
            Amount = amount;
            Killed = killed;
        }
    }
}
