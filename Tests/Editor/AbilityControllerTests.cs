using NUnit.Framework;

namespace Juris.Abilities.Tests
{
    public class AbilityControllerTests
    {
        private ManualGameClock _clock;
        private AbilityController _controller;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualGameClock();
            _controller = new AbilityController(_clock);
        }

        private void Press(int slot) => _controller.Submit(new AbilityCommand(slot, AbilityInputPhase.Pressed));

        private void Step(float seconds)
        {
            _clock.Advance(seconds);
            _controller.Tick();
        }

        // --- Lifecycle ---

        [Test]
        public void Activate_RunsUntilDuration_ThenEnds()
        {
            var ability = _controller.Grant(TestAbility.Create(duration: 1f), 0);
            int ended = 0;
            _controller.Events.Subscribe((in AbilityEnded e) => ended++);

            Press(0);
            Assert.IsTrue(ability.IsRunning);

            Step(0.5f);
            Assert.IsTrue(ability.IsRunning);

            Step(0.6f);
            Assert.IsFalse(ability.IsRunning);
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void InstantAbility_CompletesDuringActivation()
        {
            var ability = _controller.Grant(TestAbility.Create(duration: 0f), 0);
            Press(0);
            Assert.IsFalse(ability.IsRunning);
            Assert.AreEqual(1, ((TestExecution)ability.Execution).starts);
        }

        [Test]
        public void Cooldown_BlocksReactivation_UntilElapsed()
        {
            var ability = _controller.Grant(TestAbility.Create(duration: 0f, cooldown: 2f), 0);
            Press(0);
            Assert.AreEqual(AbilityActivationResult.OnCooldown, _controller.CanActivate(ability));

            Step(2f);
            Assert.AreEqual(AbilityActivationResult.Success, _controller.CanActivate(ability));
        }

        [Test]
        public void TwoOwners_SharingDefinition_HaveSeparateState()
        {
            var definition = TestAbility.Create(duration: 0f, cooldown: 5f);
            var otherController = new AbilityController(_clock);
            var mine = _controller.Grant(definition, 0);
            var theirs = otherController.Grant(definition, 0);

            Press(0);

            Assert.Greater(mine.CooldownRemaining, 0f);
            Assert.AreEqual(0f, theirs.CooldownRemaining);
            Assert.AreNotSame(mine.Execution, theirs.GetOrCreateExecution());
        }

        [Test]
        public void Cancel_CallsExecutionCancel_AndRemovesGrantedTags()
        {
            var ability = _controller.Grant(TestAbility.Create(granted: new[] { "Test.State.Channelling" }), 0);
            var channelling = GameplayTag.Get("Test.State.Channelling");

            Press(0);
            Assert.IsTrue(_controller.Tags.HasTag(channelling));

            _controller.Submit(new AbilityCommand(0, AbilityInputPhase.Cancel));

            var execution = (TestExecution)ability.Execution;
            Assert.IsFalse(ability.IsRunning);
            Assert.AreEqual(1, execution.cancels);
            Assert.AreEqual(AbilityCancelReason.Input, execution.lastCancelReason);
            Assert.IsFalse(_controller.Tags.HasTag(channelling));
        }

        [Test]
        public void RunningAbility_ReceivesForwardedCommands()
        {
            var ability = _controller.Grant(TestAbility.Create(), 0);
            Press(0);
            _controller.Submit(new AbilityCommand(0, AbilityInputPhase.Confirm));
            _controller.Submit(new AbilityCommand(0, AbilityInputPhase.Released));

            CollectionAssert.AreEqual(
                new[] { AbilityInputPhase.Confirm, AbilityInputPhase.Released },
                ((TestExecution)ability.Execution).commands);
        }

        [Test]
        public void Execution_IsReusedAcrossActivations()
        {
            var definition = TestAbility.Create(duration: 0f);
            var ability = _controller.Grant(definition, 0);

            Press(0);
            Press(0);
            Press(0);

            Assert.AreEqual(1, definition.created.Count);
            Assert.AreEqual(3, definition.created[0].starts);
        }

        // --- Tag interaction matrix (Deadeye / Blink / stun) ---

        private AbilityInstance GrantDeadeye(int slot = 0) => _controller.Grant(TestAbility.Create(
            duration: 6f,
            abilityTags: new[] { "Test.Ability.Ultimate", "Test.Ability.Interruptible" },
            granted: new[] { "Test.State.Channelling" },
            blockedBy: new[] { "Test.State.Stunned" },
            blocks: new[] { "Test.Ability.Movement" }), slot);

        private AbilityInstance GrantBlink(int slot = 1) => _controller.Grant(TestAbility.Create(
            duration: 0f,
            abilityTags: new[] { "Test.Ability.Movement.Blink" },
            blockedBy: new[] { "Test.State.Stunned" }), slot);

        [Test]
        public void Deadeye_BlocksMovementAbilities_WhileRunning()
        {
            GrantDeadeye();
            var blink = GrantBlink();

            Press(0);
            Assert.AreEqual(AbilityActivationResult.Blocked, _controller.CanActivate(blink));

            Step(6f);
            Assert.AreEqual(AbilityActivationResult.Success, _controller.CanActivate(blink));
        }

        [Test]
        public void StunnedOwner_CannotActivate()
        {
            var blink = GrantBlink();
            var stunned = GameplayTag.Get("Test.State.Stunned.Hard");

            _controller.Tags.Add(stunned);
            Assert.AreEqual(AbilityActivationResult.Blocked, _controller.CanActivate(blink));

            _controller.Tags.Remove(stunned);
            Assert.AreEqual(AbilityActivationResult.Success, _controller.CanActivate(blink));
        }

        [Test]
        public void Stun_CancelsInterruptible_ButNotOthers()
        {
            var deadeye = GrantDeadeye();
            var sturdy = _controller.Grant(TestAbility.Create(abilityTags: new[] { "Test.Ability.Ultimate" }), 2);

            Press(0);
            Press(2);
            _controller.CancelAbilitiesWithTags(new TagSet("Test.Ability.Interruptible"));

            Assert.IsFalse(deadeye.IsRunning);
            Assert.AreEqual(AbilityCancelReason.Interrupted, ((TestExecution)deadeye.Execution).lastCancelReason);
            Assert.IsTrue(sturdy.IsRunning);
        }

        [Test]
        public void Activation_CancelsAbilitiesWithMatchingTags()
        {
            var deadeye = GrantDeadeye();
            _controller.Grant(TestAbility.Create(duration: 0f, cancels: new[] { "Test.Ability.Ultimate" }), 3);

            Press(0);
            Press(3);

            Assert.IsFalse(deadeye.IsRunning);
        }

        [Test]
        public void Activation_CancelsRunningAbilityAtOrBelowItsPriority()
        {
            var channel = _controller.Grant(TestAbility.Create(abilityTags: new[] { "Test.Ability.Channel" }, interruptPriority: 1), 0);
            _controller.Grant(TestAbility.Create(duration: 0f, cancels: new[] { "Test.Ability.Channel" }, interruptPriority: 1), 1);

            Press(0);
            Press(1);

            Assert.IsFalse(channel.IsRunning);
        }

        [Test]
        public void Activation_DoesNotCancelRunningAbilityAboveItsPriority()
        {
            var ultimate = _controller.Grant(TestAbility.Create(abilityTags: new[] { "Test.Ability.Channel" }, interruptPriority: 5), 0);
            var light = _controller.Grant(TestAbility.Create(duration: 1f, cancels: new[] { "Test.Ability.Channel" }, interruptPriority: 1), 1);

            Press(0);
            Press(1);

            Assert.IsTrue(ultimate.IsRunning);
            Assert.IsTrue(light.IsRunning);
            Assert.AreEqual(0, ((TestExecution)ultimate.Execution).cancels);
        }

        [Test]
        public void ExternalCancel_RespectsPriority_AndDefaultsToCancellingAll()
        {
            var weak = _controller.Grant(TestAbility.Create(abilityTags: new[] { "Test.Ability.Interruptible" }, interruptPriority: 1), 0);
            var strong = _controller.Grant(TestAbility.Create(abilityTags: new[] { "Test.Ability.Interruptible" }, interruptPriority: 3), 1);
            Press(0);
            Press(1);

            _controller.CancelAbilitiesWithTags(new TagSet("Test.Ability.Interruptible"), AbilityCancelReason.Interrupted, 2);
            Assert.IsFalse(weak.IsRunning);
            Assert.IsTrue(strong.IsRunning);

            _controller.CancelAbilitiesWithTags(new TagSet("Test.Ability.Interruptible"));
            Assert.IsFalse(strong.IsRunning);
        }

        // --- Input buffer ---

        [Test]
        public void BufferedPress_FiresWhenBlockerClears_WithinWindow()
        {
            GrantDeadeye();
            var blink = GrantBlink();
            int blinkStarts = 0;
            _controller.Events.Subscribe((in AbilityActivated e) => { if (e.Ability == blink) blinkStarts++; });

            Press(0);
            Step(5.95f);
            Press(1);                // pressed 50 ms before Deadeye ends
            Assert.AreEqual(0, blinkStarts);

            Step(0.06f);             // Deadeye ends this tick, buffered Blink fires
            Assert.AreEqual(1, blinkStarts);
        }

        [Test]
        public void BufferedPress_Expires_AndReportsFailure()
        {
            GrantDeadeye();
            var blink = GrantBlink();
            AbilityActivationResult? failure = null;
            _controller.Events.Subscribe((in AbilityActivationFailed e) => failure = e.Reason);

            Press(0);
            Press(1);
            Step(0.2f);              // past the 120 ms window, Deadeye still running

            Assert.AreEqual(AbilityActivationResult.Blocked, failure);
            Step(6f);
            Assert.IsFalse(blink.IsRunning);
            Assert.AreEqual(null, blink.Execution, "never activated");
        }

        // --- Charges ---

        private AbilityInstance GrantChargedBlink(float groupCooldown = 0f, int slot = 0) =>
            _controller.Grant(TestAbility.Create(duration: 0f, cooldown: 3f, maxCharges: 3,
                cooldownGroup: "Cooldown.Movement", groupCooldown: groupCooldown), slot);

        [Test]
        public void Charges_SpendOnePerActivation_BlockAtZero()
        {
            var blink = GrantChargedBlink();

            Press(0);
            Assert.AreEqual(2, blink.Charges);
            Assert.AreEqual(0f, blink.CooldownRemaining, "a charge is left");

            Press(0);
            Press(0);
            Assert.AreEqual(0, blink.Charges);
            Assert.AreEqual(AbilityActivationResult.OnCooldown, _controller.CanActivate(blink));
            Assert.AreEqual(3f, blink.CooldownRemaining, 1e-4f);
        }

        [Test]
        public void Charges_RechargeOneAtATime()
        {
            var blink = GrantChargedBlink();
            Press(0); Press(0); Press(0);

            Step(3f);
            Assert.AreEqual(1, blink.Charges);
            Step(3f);
            Assert.AreEqual(2, blink.Charges);
            Step(3f);
            Assert.AreEqual(3, blink.Charges);
            Assert.AreEqual(0f, blink.RechargeRemaining);
        }

        [Test]
        public void Charges_TimerKeepsRunning_WhenSpentMidRecharge()
        {
            var blink = GrantChargedBlink();
            Press(0);                 // t=0: 2 left, next charge at t=3
            Step(2f);
            Press(0);                 // t=2: 1 left, timer not restarted

            Step(1f);                 // t=3
            Assert.AreEqual(2, blink.Charges);
            Assert.AreEqual(3f, blink.RechargeRemaining, 1e-4f, "next charge at t=6");
        }

        [Test]
        public void Charges_NeverExceedMax_AfterLongGap()
        {
            var blink = GrantChargedBlink();
            Press(0); Press(0); Press(0);

            Step(100f);

            Assert.AreEqual(3, blink.Charges);
            Press(0);
            Assert.AreEqual(3f, blink.RechargeRemaining, 1e-4f, "timer starts fresh from full");
        }

        // --- Shared cooldown groups ---

        [Test]
        public void CooldownGroup_LocksOtherAbilitiesInGroup()
        {
            var blink = GrantChargedBlink(groupCooldown: 1f);
            var dash = _controller.Grant(TestAbility.Create(duration: 0f, cooldownGroup: "Cooldown.Movement"), 1);
            var other = _controller.Grant(TestAbility.Create(duration: 0f), 2);

            Press(0);

            Assert.AreEqual(AbilityActivationResult.OnCooldown, _controller.CanActivate(dash));
            Assert.AreEqual(AbilityActivationResult.OnCooldown, _controller.CanActivate(blink), "charges left, group still locks");
            Assert.AreEqual(1f, dash.CooldownRemaining, 1e-4f);
            Assert.AreEqual(AbilityActivationResult.Success, _controller.CanActivate(other));

            Step(1.01f);
            Assert.AreEqual(AbilityActivationResult.Success, _controller.CanActivate(dash));
            Assert.AreEqual(AbilityActivationResult.Success, _controller.CanActivate(blink));
        }

        [Test]
        public void CooldownGroup_NeverShortensLongerLockout()
        {
            var group = GameplayTag.Get("Cooldown.Movement");
            var blink = GrantChargedBlink(groupCooldown: 1f);
            _controller.StartGroupCooldown(group, 5f);
            Step(4.5f);
            _controller.StartGroupCooldown(group, 0.1f);

            Assert.AreEqual(0.5f, _controller.GetGroupCooldownRemaining(group), 1e-4f);
            Assert.AreEqual(0.5f, blink.CooldownRemaining, 1e-4f);
        }

        // --- Resource costs (ultimate charge) ---

        private static readonly GameplayTag Ultimate = GameplayTag.Get("Resource.Ultimate");

        private AbilityInstance GrantUltimate(float cost = 100f) =>
            _controller.Grant(TestAbility.Create(duration: 1f, costResource: "Resource.Ultimate", cost: cost,
                blockedBy: new[] { "State.Stunned" }), 0);

        [Test]
        public void Cost_BlocksActivation_BelowCost()
        {
            var ultimate = GrantUltimate();
            _controller.Resources.Define(Ultimate, 100f, 99f);
            AbilityActivationResult failure = AbilityActivationResult.Success;
            _controller.Events.Subscribe((in AbilityActivationFailed e) => failure = e.Reason);

            Press(0);
            Step(0.2f); // let the buffered press expire

            Assert.IsFalse(ultimate.IsRunning);
            Assert.AreEqual(AbilityActivationResult.InsufficientResource, failure);
            Assert.AreEqual(99f, _controller.Resources.GetCurrent(Ultimate));
        }

        [Test]
        public void Cost_IsSpent_OnActivation()
        {
            var ultimate = GrantUltimate();
            _controller.Resources.Define(Ultimate, 100f, 100f);

            Press(0);

            Assert.IsTrue(ultimate.IsRunning);
            Assert.AreEqual(0f, _controller.Resources.GetCurrent(Ultimate));
        }

        [Test]
        public void Cost_IsNotSpent_OnFailedActivation()
        {
            var ultimate = GrantUltimate();
            _controller.Resources.Define(Ultimate, 100f, 100f);
            _controller.Tags.Add(GameplayTag.Get("State.Stunned"));

            Press(0);
            Step(0.2f);

            Assert.IsFalse(ultimate.IsRunning);
            Assert.AreEqual(100f, _controller.Resources.GetCurrent(Ultimate));
        }

        [Test]
        public void Cost_UndefinedResource_CannotActivate()
        {
            var ultimate = GrantUltimate();
            Assert.AreEqual(AbilityActivationResult.InsufficientResource, _controller.CanActivate(ultimate));
        }

        [Test]
        public void BufferedPress_FiresWhenMeterFills_WithinWindow()
        {
            var ultimate = GrantUltimate();
            _controller.Resources.Define(Ultimate, 100f, 95f);

            Press(0);
            _controller.Resources.Add(Ultimate, 5f);
            Step(0.05f);

            Assert.IsTrue(ultimate.IsRunning);
        }

        [Test]
        public void Resources_ClampToMax_AndPublishChanges()
        {
            _controller.Resources.Define(Ultimate, 100f);
            float lastDelta = 0f;
            int changes = 0;
            _controller.Events.Subscribe((in AbilityResourceChanged e) => { changes++; lastDelta = e.Delta; });

            Assert.AreEqual(60f, _controller.Resources.Add(Ultimate, 60f));
            Assert.AreEqual(40f, _controller.Resources.Add(Ultimate, 60f), "clamped to max");
            Assert.AreEqual(0f, _controller.Resources.Add(Ultimate, 10f), "already full: no change");

            Assert.AreEqual(2, changes);
            Assert.AreEqual(40f, lastDelta);
            Assert.AreEqual(1f, _controller.Resources.GetFraction(Ultimate));
        }

        // --- Refund on cancel ---

        private AbilityInstance GrantRefundingUltimate(
            AbilityCancelReasonMask reasons = AbilityCancelReasonMask.Input) =>
            _controller.Grant(TestAbility.Create(duration: 1f, costResource: "Resource.Ultimate", cost: 100f,
                refundOnCancel: 0.75f, refundReasons: reasons), 0);

        [Test]
        public void Refund_OnInputCancel_GivesBackFraction()
        {
            var ultimate = GrantRefundingUltimate();
            _controller.Resources.Define(Ultimate, 100f, 100f);
            float refundSeen = 0f;
            _controller.Events.Subscribe((in AbilityEnded e) => refundSeen = _controller.Resources.GetCurrent(Ultimate));

            Press(0);
            _controller.Submit(new AbilityCommand(0, AbilityInputPhase.Cancel));

            Assert.IsFalse(ultimate.IsRunning);
            Assert.AreEqual(75f, _controller.Resources.GetCurrent(Ultimate));
            Assert.AreEqual(75f, refundSeen, "refunded before AbilityEnded");
        }

        [Test]
        public void Refund_NotGiven_OnCompletion()
        {
            GrantRefundingUltimate();
            _controller.Resources.Define(Ultimate, 100f, 100f);

            Press(0);
            Step(1.1f);

            Assert.AreEqual(0f, _controller.Resources.GetCurrent(Ultimate));
        }

        [Test]
        public void Refund_RespectsReasonFilter()
        {
            var ultimate = GrantRefundingUltimate();
            _controller.Resources.Define(Ultimate, 100f, 100f);

            Press(0);
            _controller.Cancel(ultimate, AbilityCancelReason.Interrupted);

            Assert.AreEqual(0f, _controller.Resources.GetCurrent(Ultimate), "a stun does not refund by default");
        }

        [Test]
        public void Refund_OnFilteredInReason()
        {
            var ultimate = GrantRefundingUltimate(AbilityCancelReasonMask.Input | AbilityCancelReasonMask.Interrupted);
            _controller.Resources.Define(Ultimate, 100f, 100f);

            Press(0);
            _controller.Cancel(ultimate, AbilityCancelReason.Interrupted);

            Assert.AreEqual(75f, _controller.Resources.GetCurrent(Ultimate));
        }

        [Test]
        public void Refund_ZeroFraction_ByDefault()
        {
            var ultimate = GrantUltimate();
            _controller.Resources.Define(Ultimate, 100f, 100f);

            Press(0);
            _controller.Submit(new AbilityCommand(0, AbilityInputPhase.Cancel));

            Assert.AreEqual(0f, _controller.Resources.GetCurrent(Ultimate));
        }

        // --- Events ---

        [Test]
        public void EventBus_DeliversTypedEvents_UntilUnsubscribed()
        {
            var bus = new AbilityEventBus();
            int received = 0;
            AbilityEventHandler<AbilityActivated> handler = (in AbilityActivated e) => received++;

            bus.Subscribe(handler);
            bus.Publish(new AbilityActivated(null));
            bus.Publish(new AbilityEnded(null, false, default));
            bus.Unsubscribe(handler);
            bus.Publish(new AbilityActivated(null));

            Assert.AreEqual(1, received);
        }
    }
}
