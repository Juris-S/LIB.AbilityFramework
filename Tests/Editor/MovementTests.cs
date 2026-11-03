using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Juris.Abilities.Tests
{
    public class MovementTests
    {
        private sealed class FixedVelocity : IMovementOverride
        {
            private readonly Vector3 _velocity;
            public FixedVelocity(Vector3 velocity) => _velocity = velocity;
            public Vector3 GetVelocity(float deltaTime) => _velocity;
        }

        private ManualGameClock _clock;
        private MovementArbiter _movement;
        private readonly FixedVelocity _dash = new FixedVelocity(Vector3.forward * 20f);
        private readonly FixedVelocity _knockback = new FixedVelocity(Vector3.back * 10f);

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualGameClock();
            _movement = new MovementArbiter(_clock);
        }

        // --- Control requests ---

        [Test]
        public void NoRequests_MotorUsesNormalMovement()
        {
            Assert.IsNull(_movement.Current);
            Assert.IsFalse(_movement.TryGetVelocity(0.1f, out var velocity));
            Assert.AreEqual(Vector3.zero, velocity);
        }

        [Test]
        public void HighestPriorityWins_RegardlessOfOrder()
        {
            _movement.RequestControl(_knockback, priority: 10);
            _movement.RequestControl(_dash, priority: 1);

            Assert.AreSame(_knockback, _movement.Current);
            Assert.IsTrue(_movement.TryGetVelocity(0.1f, out var velocity));
            Assert.AreEqual(Vector3.back * 10f, velocity);
        }

        [Test]
        public void EqualPriority_NewestWins()
        {
            _movement.RequestControl(_dash, priority: 5);
            _movement.RequestControl(_knockback, priority: 5);

            Assert.AreSame(_knockback, _movement.Current);
        }

        [Test]
        public void Release_HandsControlBack()
        {
            _movement.RequestControl(_dash, priority: 1);
            int knockback = _movement.RequestControl(_knockback, priority: 10);

            Assert.IsTrue(_movement.Release(knockback));
            Assert.AreSame(_dash, _movement.Current);
            Assert.IsFalse(_movement.Release(knockback), "a handle only releases once");
        }

        [Test]
        public void Duration_ExpiresOnTheClock()
        {
            int dash = _movement.RequestControl(_dash, priority: 1, duration: 0.5f);

            _clock.Advance(0.25f);
            Assert.IsTrue(_movement.IsActive(dash));
            Assert.AreSame(_dash, _movement.Current);

            _clock.Advance(0.25f);
            Assert.IsFalse(_movement.IsActive(dash));
            Assert.IsNull(_movement.Current);
        }

        [Test]
        public void OutrankedRequest_StaysActive_AndResumesAfterwards()
        {
            int dash = _movement.RequestControl(_dash, priority: 1);
            int knockback = _movement.RequestControl(_knockback, priority: 10, duration: 0.2f);

            Assert.IsTrue(_movement.IsActive(dash));
            _clock.Advance(0.2f);

            Assert.IsFalse(_movement.IsActive(knockback));
            Assert.AreSame(_dash, _movement.Current);
        }

        // --- Speed multipliers ---

        [Test]
        public void SpeedMultipliers_Compound_AndRelease()
        {
            Assert.AreEqual(1f, _movement.SpeedMultiplierTotal);

            int slow = _movement.AddSpeedMultiplier(0.5f);
            _movement.AddSpeedMultiplier(1.2f);
            Assert.AreEqual(0.6f, _movement.SpeedMultiplierTotal, 1e-5f);

            Assert.IsTrue(_movement.Release(slow));
            Assert.AreEqual(1.2f, _movement.SpeedMultiplierTotal, 1e-5f);
        }

        [Test]
        public void SpeedMultipliers_DoNotTakeControl()
        {
            _movement.AddSpeedMultiplier(0.5f);
            Assert.IsNull(_movement.Current);
        }

        [Test]
        public void TryGetVelocity_DoesNotAllocate()
        {
            _movement.RequestControl(_dash, priority: 1, duration: 10f);
            _movement.RequestControl(_knockback, priority: 2, duration: 10f);
            _movement.TryGetVelocity(0.1f, out _); // warm up

            Assert.That(() => { _movement.TryGetVelocity(0.1f, out _); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Controller_OwnsAnArbiterOnItsClock()
        {
            var controller = new AbilityController(_clock);
            int handle = controller.Movement.RequestControl(_dash, priority: 1, duration: 1f);

            _clock.Advance(1f);
            Assert.IsFalse(controller.Movement.IsActive(handle));
        }

        // --- Teleport ---

        [Test]
        public void Teleport_SyncsCharacterController()
        {
            var go = new GameObject("body", typeof(CharacterController));
            try
            {
                var target = new Vector3(10f, 0f, 5f);
                var facing = Quaternion.Euler(0f, 90f, 0f);
                Teleporter.Teleport(go.transform, target, facing);

                // A stale CharacterController would snap back towards the origin on its next Move.
                go.GetComponent<CharacterController>().Move(Vector3.zero);
                Assert.That(Vector3.Distance(target, go.transform.position), NUnit.Framework.Is.LessThan(0.01f));
                Assert.That(Quaternion.Angle(facing, go.transform.rotation), NUnit.Framework.Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Teleport_MovesRigidbody_AndKeepsVelocity()
        {
            var go = new GameObject("body", typeof(Rigidbody));
            try
            {
                var rigidbody = go.GetComponent<Rigidbody>();
                rigidbody.linearVelocity = Vector3.up * 3f;
                var target = new Vector3(-4f, 2f, 1f);

                Teleporter.Teleport(go.transform, target, Quaternion.identity);

                Assert.That(Vector3.Distance(target, rigidbody.position), NUnit.Framework.Is.LessThan(0.01f));
                Assert.AreEqual(Vector3.up * 3f, rigidbody.linearVelocity);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
