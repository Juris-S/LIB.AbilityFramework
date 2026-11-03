using System;
using System.Collections.Generic;

namespace Juris.Abilities
{
    public enum AbilityActivationResult
    {
        Success,
        AlreadyRunning,
        OnCooldown,
        /// <summary>Owner has a BlockedByTags tag, or a running ability blocks this one's ability tags.</summary>
        Blocked,
        /// <summary>Owner cannot pay the definition's <see cref="AbilityDefinition.Cost"/>.</summary>
        InsufficientResource,
    }

    /// <summary>
    /// Owns one actor's abilities: activation validation, tag rules, input buffering, ticking and cancellation.
    /// Plain C#; drive it with <see cref="AbilitySystem"/> or your own loop.
    /// </summary>
    public sealed class AbilityController
    {
        private struct GroupCooldown
        {
            public GameplayTag Group;
            public double ReadyTime;
        }

        private readonly List<AbilityInstance> _abilities = new List<AbilityInstance>();
        private readonly List<GroupCooldown> _groupCooldowns = new List<GroupCooldown>();
        private readonly float _inputBufferSeconds;

        // Limitation: one buffered press (latest wins). Make it a queue if combos need several.
        private bool _hasBufferedPress;
        private AbilityCommand _bufferedPress;
        private double _bufferExpiry;

        public AbilityController(IGameClock clock, float inputBufferSeconds = 0.12f)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _inputBufferSeconds = inputBufferSeconds;
            Resources = new AbilityResources(Events);
            Movement = new MovementArbiter(clock);
        }

        public IGameClock Clock { get; }
        public AbilityEventBus Events { get; } = new AbilityEventBus();
        public AbilityServices Services { get; } = new AbilityServices();

        /// <summary>Spendable values (ultimate charge, mana...). Game code defines and fills them.</summary>
        public AbilityResources Resources { get; }

        /// <summary>Damage boosts and the like, applied by <see cref="DamageService"/> before the target's modifiers.</summary>
        public DamageModifiers OutgoingDamage { get; } = new DamageModifiers();

        /// <summary>Movement control requests and speed multipliers. The game's <see cref="IAbilityMotor"/> reads it.</summary>
        public MovementArbiter Movement { get; }

        /// <summary>Owner state tags (granted by running abilities, or added by stuns, statuses...).</summary>
        public TagContainer Tags { get; } = new TagContainer();

        /// <summary>Ability tags currently blocked by running abilities.</summary>
        public TagContainer BlockedAbilityTags { get; } = new TagContainer();

        public IReadOnlyList<AbilityInstance> Abilities => _abilities;

        internal bool HasBufferedPress => _hasBufferedPress;
        internal AbilityCommand BufferedPress => _bufferedPress;
        internal float BufferRemaining => (float)Math.Max(0d, _bufferExpiry - Clock.Time);

        public AbilityInstance Grant(AbilityDefinition definition, int slot)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (FindBySlot(slot) != null) throw new InvalidOperationException($"Slot {slot} is already taken.");
            var instance = new AbilityInstance(this, definition, slot);
            _abilities.Add(instance);
            return instance;
        }

        public void Remove(AbilityInstance instance)
        {
            Cancel(instance, AbilityCancelReason.Removed);
            _abilities.Remove(instance);
            if (_hasBufferedPress && _bufferedPress.Slot == instance.Slot) _hasBufferedPress = false;
        }

        public AbilityInstance FindBySlot(int slot)
        {
            for (int i = 0; i < _abilities.Count; i++)
                if (_abilities[i].Slot == slot) return _abilities[i];
            return null;
        }

        /// <summary>Routes input intent: activates idle abilities (buffering blocked presses) or forwards to running ones.</summary>
        public void Submit(in AbilityCommand command)
        {
            var instance = FindBySlot(command.Slot);
            if (instance == null) return;

            if (instance.IsRunning)
            {
                if (command.Phase == AbilityInputPhase.Cancel)
                {
                    Cancel(instance, AbilityCancelReason.Input);
                    return;
                }
                instance.Execution.HandleCommand(command);
                EndIfComplete(instance);
                return;
            }

            if (command.Phase != AbilityInputPhase.Pressed) return;

            if (_inputBufferSeconds <= 0f)
            {
                TryActivate(instance, command);
                return;
            }

            if (CanActivate(instance) == AbilityActivationResult.Success)
            {
                Activate(instance, command);
                return;
            }

            _hasBufferedPress = true;
            _bufferedPress = command;
            _bufferExpiry = Clock.Time + _inputBufferSeconds;
        }

        public AbilityActivationResult CanActivate(AbilityInstance instance)
        {
            if (instance.IsRunning) return AbilityActivationResult.AlreadyRunning;
            if (instance.CooldownRemaining > 0f) return AbilityActivationResult.OnCooldown;

            var definition = instance.Definition;
            if (Tags.HasAny(definition.BlockedByTags)) return AbilityActivationResult.Blocked;
            if (BlockedAbilityTags.ContainsAnyAncestorOf(definition.AbilityTags)) return AbilityActivationResult.Blocked;
            if (!Resources.CanAfford(definition.CostResource, definition.Cost)) return AbilityActivationResult.InsufficientResource;
            return AbilityActivationResult.Success;
        }

        /// <summary>Activates immediately without buffering. Publishes <see cref="AbilityActivationFailed"/> on failure.</summary>
        public AbilityActivationResult TryActivate(AbilityInstance instance, in AbilityCommand command)
        {
            var result = CanActivate(instance);
            if (result != AbilityActivationResult.Success)
            {
                Events.Publish(new AbilityActivationFailed(instance, result));
                return result;
            }
            Activate(instance, command);
            return result;
        }

        /// <summary>Time left on a shared cooldown group; 0 for <see cref="GameplayTag.None"/> or an idle group.</summary>
        public float GetGroupCooldownRemaining(GameplayTag group)
        {
            if (!group.IsValid) return 0f;
            for (int i = 0; i < _groupCooldowns.Count; i++)
                if (_groupCooldowns[i].Group == group)
                    return (float)Math.Max(0d, _groupCooldowns[i].ReadyTime - Clock.Time);
            return 0f;
        }

        /// <summary>Locks every ability in <paramref name="group"/> for at least <paramref name="seconds"/>. Never shortens a running lockout.</summary>
        public void StartGroupCooldown(GameplayTag group, float seconds)
        {
            if (!group.IsValid || seconds <= 0f) return;
            double readyTime = Clock.Time + seconds;
            for (int i = 0; i < _groupCooldowns.Count; i++)
            {
                if (_groupCooldowns[i].Group != group) continue;
                if (readyTime > _groupCooldowns[i].ReadyTime)
                    _groupCooldowns[i] = new GroupCooldown { Group = group, ReadyTime = readyTime };
                return;
            }
            _groupCooldowns.Add(new GroupCooldown { Group = group, ReadyTime = readyTime });
        }

        public void Tick()
        {
            float deltaTime = Clock.DeltaTime;
            for (int i = 0; i < _abilities.Count; i++)
            {
                var instance = _abilities[i];
                if (!instance.IsRunning) continue;
                instance.Execution.Tick(deltaTime);
                EndIfComplete(instance);
            }

            if (_hasBufferedPress) RetryBufferedPress();
        }

        public void Cancel(AbilityInstance instance, AbilityCancelReason reason)
        {
            if (!instance.IsRunning) return;
            instance.IsRunning = false; // first, so a re-entrant Cancel from the execution is a no-op
            instance.Execution.Cancel(reason);

            var definition = instance.Definition;
            float refund = definition.GetRefund(reason);
            if (refund > 0f) Resources.Add(definition.CostResource, refund);

            Cleanup(instance, true, reason);
        }

        /// <summary>
        /// Cancels running abilities whose ability tags match any of <paramref name="tags"/> (stuns, interrupts).
        /// Abilities with an <see cref="AbilityDefinition.InterruptPriority"/> above <paramref name="priority"/> survive;
        /// the default cancels everything that matches.
        /// </summary>
        public void CancelAbilitiesWithTags(TagSet tags, AbilityCancelReason reason = AbilityCancelReason.Interrupted, int priority = int.MaxValue)
        {
            for (int i = 0; i < _abilities.Count; i++)
            {
                var instance = _abilities[i];
                if (instance.IsRunning
                    && instance.Definition.InterruptPriority <= priority
                    && instance.Definition.AbilityTags.MatchesAny(tags))
                    Cancel(instance, reason);
            }
        }

        public void CancelAll(AbilityCancelReason reason = AbilityCancelReason.External)
        {
            for (int i = 0; i < _abilities.Count; i++) Cancel(_abilities[i], reason);
            _hasBufferedPress = false;
        }

        private void Activate(AbilityInstance instance, in AbilityCommand command)
        {
            var definition = instance.Definition;
            if (definition.CancelAbilitiesWithTags.Count > 0)
                CancelAbilitiesWithTags(definition.CancelAbilitiesWithTags, AbilityCancelReason.Interrupted, definition.InterruptPriority);

            // Limitation: cooldown always starts on activation; add a start-on-end/refund policy when an ability needs it.
            instance.SpendCharge();
            StartGroupCooldown(definition.CooldownGroup, definition.GroupCooldown);
            Resources.TrySpend(definition.CostResource, definition.Cost); // partly refunded on cancel, see Cancel
            instance.IsRunning = true;
            Tags.Add(definition.GrantedTags);
            BlockedAbilityTags.Add(definition.BlockAbilitiesWithTags);

            var execution = instance.GetOrCreateExecution();
            Events.Publish(new AbilityActivated(instance));
            execution.Start(command);
            EndIfComplete(instance);
        }

        private void EndIfComplete(AbilityInstance instance)
        {
            if (!instance.IsRunning || !instance.Execution.IsComplete) return;
            instance.IsRunning = false;
            Cleanup(instance, false, default);
        }

        private void Cleanup(AbilityInstance instance, bool cancelled, AbilityCancelReason reason)
        {
            var definition = instance.Definition;
            Tags.Remove(definition.GrantedTags);
            BlockedAbilityTags.Remove(definition.BlockAbilitiesWithTags);
            Events.Publish(new AbilityEnded(instance, cancelled, reason));
        }

        private void RetryBufferedPress()
        {
            var instance = FindBySlot(_bufferedPress.Slot);
            if (instance == null)
            {
                _hasBufferedPress = false;
                return;
            }

            var result = CanActivate(instance);
            if (result == AbilityActivationResult.Success)
            {
                _hasBufferedPress = false;
                Activate(instance, _bufferedPress);
            }
            else if (Clock.Time >= _bufferExpiry)
            {
                _hasBufferedPress = false;
                Events.Publish(new AbilityActivationFailed(instance, result));
            }
        }
    }
}
