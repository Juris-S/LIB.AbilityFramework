using System;
using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>
    /// Thin MonoBehaviour host: creates the <see cref="AbilityController"/>, grants starting abilities and ticks it.
    /// Registers its GameObject and a <see cref="DamageService"/> in <see cref="AbilityController.Services"/>;
    /// add game services in your own Awake.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class AbilitySystem : MonoBehaviour
    {
        [Serializable]
        private struct StartingAbility
        {
            public AbilityDefinition definition;
            public int slot;
        }

        [SerializeField] private StartingAbility[] startingAbilities = Array.Empty<StartingAbility>();
        [SerializeField, Min(0f)] private float inputBufferSeconds = 0.12f;

        public AbilityController Controller { get; private set; }

        private void Awake()
        {
            Controller = new AbilityController(new UnityGameClock(), inputBufferSeconds);
            Controller.Services.Add(gameObject);
            Controller.Services.Add(new DamageService(Controller, GetComponent<ITargetable>()));
            foreach (var ability in startingAbilities)
                if (ability.definition != null) Controller.Grant(ability.definition, ability.slot);
        }

        private void Update() => Controller.Tick();

        private void OnDisable() => Controller.CancelAll(AbilityCancelReason.Removed);
    }
}
