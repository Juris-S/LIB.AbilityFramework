using System;

namespace Juris.Abilities
{
    /// <summary>One owner's copy of an ability: charges, cooldown and the reusable execution.</summary>
    public sealed class AbilityInstance
    {
        private IAbilityExecution _execution;
        private int _charges;
        private double _rechargeReadyTime; // when the next charge lands, while below max

        internal AbilityInstance(AbilityController controller, AbilityDefinition definition, int slot)
        {
            Controller = controller;
            Definition = definition;
            Slot = slot;
            _charges = definition.MaxCharges;
        }

        public AbilityController Controller { get; }
        public AbilityDefinition Definition { get; }
        public int Slot { get; }
        public bool IsRunning { get; internal set; }

        /// <summary>The execution, or null if never activated.</summary>
        public IAbilityExecution Execution => _execution;

        public int MaxCharges => Definition.MaxCharges;

        public int Charges
        {
            get
            {
                Recharge();
                return _charges;
            }
        }

        /// <summary>Time until the next charge lands; 0 when full.</summary>
        public float RechargeRemaining
        {
            get
            {
                Recharge();
                return _charges >= MaxCharges ? 0f : (float)Math.Max(0d, _rechargeReadyTime - Controller.Clock.Time);
            }
        }

        /// <summary>Time until cooldowns stop blocking activation: 0 while a charge is stored, and never below the group lockout.</summary>
        public float CooldownRemaining =>
            Math.Max(Charges > 0 ? 0f : RechargeRemaining, Controller.GetGroupCooldownRemaining(Definition.CooldownGroup));

        // Limitation: single reused execution per instance = no overlapping casts. Swap for a pool when an ability needs overlap.
        internal IAbilityExecution GetOrCreateExecution() => _execution ??= Definition.CreateExecution(this);

        internal void SpendCharge()
        {
            Recharge();
            float cooldown = Definition.Cooldown;
            if (cooldown <= 0f || _charges <= 0) return;
            if (_charges >= MaxCharges) _rechargeReadyTime = Controller.Clock.Time + cooldown; // full: start the timer
            _charges--;
        }

        /// <summary>Lands every charge whose timer has passed, one cooldown after the other.</summary>
        private void Recharge()
        {
            int max = MaxCharges;
            if (_charges > max) _charges = max; // max lowered on the asset
            float cooldown = Definition.Cooldown;
            double now = Controller.Clock.Time;
            while (_charges < max && (cooldown <= 0f || now >= _rechargeReadyTime))
            {
                _charges++;
                _rechargeReadyTime += cooldown;
            }
        }
    }
}
