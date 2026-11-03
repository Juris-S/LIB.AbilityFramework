using System.Collections.Generic;
using UnityEngine;

namespace Juris.Abilities.Tests
{
    /// <summary>Configurable test ability: runs for <see cref="duration"/> seconds (0 = instant) and records calls.</summary>
    internal sealed class TestAbility : AbilityDefinition
    {
        public float duration;
        public readonly List<TestExecution> created = new List<TestExecution>();

        public static TestAbility Create(
            float duration = 1f,
            float cooldown = 0f,
            string[] abilityTags = null,
            string[] granted = null,
            string[] blockedBy = null,
            string[] blocks = null,
            string[] cancels = null,
            string costResource = null,
            float cost = 0f,
            float refundOnCancel = 0f,
            AbilityCancelReasonMask refundReasons = AbilityCancelReasonMask.Input,
            int maxCharges = 1,
            string cooldownGroup = null,
            float groupCooldown = 0f,
            int interruptPriority = 0)
        {
            var ability = CreateInstance<TestAbility>();
            ability.duration = duration;
            ability.cooldown = cooldown;
            ability.abilityTags = new TagSet(abilityTags);
            ability.grantedTags = new TagSet(granted);
            ability.blockedByTags = new TagSet(blockedBy);
            ability.blockAbilitiesWithTags = new TagSet(blocks);
            ability.cancelAbilitiesWithTags = new TagSet(cancels);
            ability.costResource = costResource;
            ability.cost = cost;
            ability.refundOnCancel = refundOnCancel;
            ability.refundReasons = refundReasons;
            ability.maxCharges = maxCharges;
            ability.cooldownGroup = cooldownGroup;
            ability.groupCooldown = groupCooldown;
            ability.interruptPriority = interruptPriority;
            return ability;
        }

        public override IAbilityExecution CreateExecution(AbilityInstance instance)
        {
            var execution = new TestExecution(duration);
            created.Add(execution);
            return execution;
        }
    }

    internal sealed class TestExecution : IAbilityExecution
    {
        private readonly float _duration;
        private float _elapsed;

        public TestExecution(float duration) => _duration = duration;

        public int starts;
        public int cancels;
        public AbilityCancelReason lastCancelReason;
        public readonly List<AbilityInputPhase> commands = new List<AbilityInputPhase>();

        public bool IsComplete => _elapsed >= _duration;

        public void Start(in AbilityCommand command)
        {
            starts++;
            _elapsed = 0f;
        }

        public void Tick(float deltaTime) => _elapsed += deltaTime;

        public void HandleCommand(in AbilityCommand command) => commands.Add(command.Phase);

        public void Cancel(AbilityCancelReason reason)
        {
            cancels++;
            lastCancelReason = reason;
        }
    }
}
