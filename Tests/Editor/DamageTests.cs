using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Juris.Abilities.Tests
{
    public class DamageTests
    {
        private AbilityController _controller;
        private DamageService _damage;
        private GameObject _go;
        private Health _health;

        [SetUp]
        public void SetUp()
        {
            _controller = new AbilityController(new ManualGameClock());
            _damage = new DamageService(_controller);
            _go = new GameObject("target", typeof(Health));
            _health = _go.GetComponent<Health>();
            _health.ResetToFull(); // Awake does not run in EditMode
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        // --- Modifier stacking ---

        [Test]
        public void Modifiers_AddFlatBeforeMultiplying()
        {
            var modifiers = new DamageModifiers();
            modifiers.Add(DamageModifierOp.Multiply, 2f);
            modifiers.Add(DamageModifierOp.Add, 10f);   // added after the multiplier, still applied first

            Assert.AreEqual(120f, modifiers.Apply(50f)); // (50 + 10) × 2
        }

        [Test]
        public void Modifiers_MultipliersCompound_AndClampAtZero()
        {
            var modifiers = new DamageModifiers();
            modifiers.Add(DamageModifierOp.Multiply, 1.5f);
            modifiers.Add(DamageModifierOp.Multiply, 0.5f);
            Assert.AreEqual(75f, modifiers.Apply(100f));

            modifiers.Add(DamageModifierOp.Add, -500f);
            Assert.AreEqual(0f, modifiers.Apply(100f));
        }

        [Test]
        public void Modifiers_RemoveByHandle()
        {
            var modifiers = new DamageModifiers();
            int boost = modifiers.Add(DamageModifierOp.Multiply, 2f);

            Assert.IsTrue(modifiers.Remove(boost));
            Assert.IsFalse(modifiers.Remove(boost), "already removed");
            Assert.AreEqual(100f, modifiers.Apply(100f));
        }

        // --- Damage service ---

        [Test]
        public void Deal_AppliesOutgoingThenIncoming()
        {
            _controller.OutgoingDamage.Add(DamageModifierOp.Add, 20f);       // 80 + 20 = 100
            _health.IncomingDamage.Add(DamageModifierOp.Multiply, 0.5f);     // 100 × 0.5 = 50

            var result = _damage.Deal(_health, 80f);

            Assert.AreEqual(50f, result.Amount);
            Assert.AreEqual(_health.Max - 50f, _health.Current);
            Assert.IsFalse(result.Killed);
        }

        [Test]
        public void Deal_Kills_ReportsHealthRemoved_AndPublishes()
        {
            DamageDealt dealt = default;
            int events = 0;
            _controller.Events.Subscribe((in DamageDealt e) => { events++; dealt = e; });

            var result = _damage.Deal(_health, _health.Max + 100f);

            Assert.IsTrue(result.Killed);
            Assert.AreEqual(_health.Max, result.Amount, "overkill not counted");
            Assert.AreEqual(1, events);
            Assert.AreSame(_health, dealt.Target);
            Assert.IsTrue(dealt.Killed);
        }

        [Test]
        public void Deal_IgnoresDeadOrMissingTargets()
        {
            int events = 0;
            _controller.Events.Subscribe((in DamageDealt e) => events++);

            _damage.Deal(_health, _health.Max);
            var dead = _damage.Deal(_health, 50f);
            var missing = _damage.Deal(null, 50f);

            Assert.AreEqual(1, events);
            Assert.AreEqual(0f, dead.Amount);
            Assert.AreEqual(0f, missing.Amount);
        }

        [Test]
        public void Deal_SourceId_ComesFromOwner()
        {
            var owner = new GameObject("owner", typeof(Targetable));
            try
            {
                var targetable = owner.GetComponent<Targetable>();
                var damage = new DamageService(_controller, targetable);
                int sourceId = -1;
                _health.Damaged += (h, d) => sourceId = d.SourceId;

                damage.Deal(_health, 10f);

                Assert.AreEqual(targetable.Id, sourceId);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void Deal_DoesNotAllocate()
        {
            _controller.OutgoingDamage.Add(DamageModifierOp.Multiply, 1.1f);
            _health.IncomingDamage.Add(DamageModifierOp.Add, -1f);
            _controller.Events.Subscribe((in DamageDealt e) => { });
            _damage.Deal(_health, 1f); // warm up

            Assert.That(() => { _damage.Deal(_health, 1f); }, Is.Not.AllocatingGCMemory());
        }
    }
}
