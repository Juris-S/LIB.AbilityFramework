using System;
using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>
    /// Shared, immutable design-time settings. Never store runtime state here:
    /// per-owner state belongs on <see cref="AbilityInstance"/>, per-cast state on the execution.
    /// </summary>
    public abstract class AbilityDefinition : ScriptableObject
    {
        [SerializeField] internal string abilityId;
        [Tooltip("With charges, the time to recharge one charge. Charges recharge one at a time.")]
        [SerializeField, Min(0f)] internal float cooldown;
        [Tooltip("Uses stored before the cooldown blocks activation, e.g. 3 for Tracer's Blink.")]
        [SerializeField, Min(1)] internal int maxCharges = 1;

        [Header("Cooldown Group")]
        [Tooltip("Abilities sharing this tag lock each other out, e.g. Cooldown.Movement. Empty = none.")]
        [SerializeField] internal string cooldownGroup;
        [Tooltip("On activation, every ability in the group is blocked for this long.")]
        [SerializeField, Min(0f)] internal float groupCooldown;

        [Header("Cost")]
        [Tooltip("Resource tag spent on activation, e.g. Resource.Ultimate or Resource.Mana. Empty = free.")]
        [SerializeField] internal string costResource;
        [SerializeField, Min(0f)] internal float cost;
        [Tooltip("Fraction of the cost given back when the ability is cancelled. Natural completion never refunds.")]
        [SerializeField, Range(0f, 1f)] internal float refundOnCancel;
        [Tooltip("Cancel reasons that refund, e.g. Input only, so a stun does not.")]
        [SerializeField] internal AbilityCancelReasonMask refundReasons = AbilityCancelReasonMask.Input;

        [Header("Tags")]
        [Tooltip("Describe this ability, e.g. Ability.Movement.Blink, Ability.Interruptible.")]
        [SerializeField] internal TagSet abilityTags = new TagSet();
        [Tooltip("Added to the owner while this ability runs, e.g. State.Channelling.")]
        [SerializeField] internal TagSet grantedTags = new TagSet();
        [Tooltip("Cannot activate while the owner has any of these, e.g. State.Stunned.")]
        [SerializeField] internal TagSet blockedByTags = new TagSet();
        [Tooltip("While running, other abilities with these ability tags cannot activate.")]
        [SerializeField] internal TagSet blockAbilitiesWithTags = new TagSet();
        [Tooltip("On activation, running abilities with these ability tags are cancelled.")]
        [SerializeField] internal TagSet cancelAbilitiesWithTags = new TagSet();

        [Header("Interrupts")]
        [Tooltip("Strength of this ability as an interrupt source and as an interrupt target. A cancel only lands on running abilities at or below its priority, e.g. light interrupt 1, channel 1, ultimate 5.")]
        [SerializeField] internal int interruptPriority;

        public string AbilityId => string.IsNullOrEmpty(abilityId) ? name : abilityId;
        public float Cooldown => cooldown;
        public int MaxCharges => maxCharges < 1 ? 1 : maxCharges;
        public float GroupCooldown => groupCooldown;

        /// <summary>The shared cooldown group, or <see cref="GameplayTag.None"/>.</summary>
        public GameplayTag CooldownGroup =>
            _groupResolved ? _cooldownGroupTag : ResolveCooldownGroup();
        public float Cost => cost;
        public float RefundOnCancel => refundOnCancel;
        public AbilityCancelReasonMask RefundReasons => refundReasons;

        /// <summary>Cost given back when cancelled for <paramref name="reason"/>; 0 if that reason does not refund.</summary>
        public float GetRefund(AbilityCancelReason reason) =>
            refundReasons.Includes(reason) ? cost * refundOnCancel : 0f;

        /// <summary>The resource <see cref="Cost"/> is paid from, or <see cref="GameplayTag.None"/> if free.</summary>
        public GameplayTag CostResource =>
            _costResolved ? _costResourceTag : ResolveCostResource();

        public int InterruptPriority => interruptPriority;

        public TagSet AbilityTags => abilityTags;
        public TagSet GrantedTags => grantedTags;
        public TagSet BlockedByTags => blockedByTags;
        public TagSet BlockAbilitiesWithTags => blockAbilitiesWithTags;
        public TagSet CancelAbilitiesWithTags => cancelAbilitiesWithTags;

        /// <summary>Called once per instance; the execution is reused for every later activation.</summary>
        public abstract IAbilityExecution CreateExecution(AbilityInstance instance);

        [NonSerialized] private bool _costResolved;
        [NonSerialized] private GameplayTag _costResourceTag;

        private GameplayTag ResolveCostResource()
        {
            _costResourceTag = string.IsNullOrWhiteSpace(costResource) ? GameplayTag.None : GameplayTag.Get(costResource);
            _costResolved = true;
            return _costResourceTag;
        }

        [NonSerialized] private bool _groupResolved;
        [NonSerialized] private GameplayTag _cooldownGroupTag;

        private GameplayTag ResolveCooldownGroup()
        {
            _cooldownGroupTag = string.IsNullOrWhiteSpace(cooldownGroup) ? GameplayTag.None : GameplayTag.Get(cooldownGroup);
            _groupResolved = true;
            return _cooldownGroupTag;
        }

        protected virtual void OnValidate()
        {
            _costResolved = false;
            _groupResolved = false;
        }
    }
}
