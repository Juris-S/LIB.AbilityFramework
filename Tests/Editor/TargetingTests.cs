using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Juris.Abilities.Tests
{
    public class TargetingTests
    {
        private sealed class FakeTarget : ITargetable
        {
            public int Id { get; set; }
            public int Team { get; set; } = 1;
            public bool IsTargetable { get; set; } = true;
            public Vector3 AimPosition { get; set; }
            public Vector3 IndicatorPosition => AimPosition + Vector3.up * 0.5f;
            public Transform Root { get; set; }
            public Vector3[] Points = System.Array.Empty<Vector3>();
            public int VisibilityPointCount => Points.Length;
            public Vector3 GetVisibilityPoint(int index) => Points[index];
        }

        private sealed class FakeSource : ITargetSource
        {
            public readonly List<ITargetable> list = new List<ITargetable>();
            public IReadOnlyList<ITargetable> Targets => list;
        }

        private sealed class StubLos : ILineOfSight
        {
            public System.Func<Vector3, bool> clear = _ => true;
            public bool IsClear(Vector3 from, Vector3 to, Transform targetRoot) => clear(to);
        }

        private static TargetQuery Cone105(int excludeTeam = -1) =>
            new TargetQuery(Vector3.zero, Vector3.forward, 105f, 200f, excludeTeam);

        // --- Cone math ---

        [TestCase(0f, 0f, 10f, true)]      // dead ahead
        [TestCase(10f, 0f, 10f, true)]     // 45° right, inside the 52.5° half angle
        [TestCase(10f, 0f, 5f, false)]     // ~63° right, outside
        [TestCase(0f, 0f, -10f, false)]    // behind
        [TestCase(0f, 0f, 250f, false)]    // beyond 200 m
        [TestCase(0f, 10f, 10f, true)]     // 45° up, cone is 3D
        public void Cone_Contains(float x, float y, float z, bool expected)
        {
            Assert.AreEqual(expected, Cone105().Contains(new Vector3(x, y, z)));
        }

        [Test]
        public void Cone_IsIndependentOfCameraFov_AndIncludesPointAtOrigin()
        {
            Assert.IsTrue(Cone105().Contains(Vector3.zero));
        }

        // --- Find ---

        [Test]
        public void Find_FiltersTeamRangeConeAndUntargetable()
        {
            var source = new FakeSource();
            var enemy = new FakeTarget { Id = 1, AimPosition = new Vector3(0, 0, 20) };
            source.list.Add(enemy);
            source.list.Add(new FakeTarget { Id = 2, Team = 0, AimPosition = new Vector3(0, 0, 20) });          // own team
            source.list.Add(new FakeTarget { Id = 3, AimPosition = new Vector3(0, 0, -20) });                   // behind
            source.list.Add(new FakeTarget { Id = 4, AimPosition = new Vector3(0, 0, 500) });                   // range
            source.list.Add(new FakeTarget { Id = 5, AimPosition = new Vector3(0, 0, 20), IsTargetable = false }); // invisible/dead

            var results = new List<ITargetable>();
            TargetQueries.Find(source, Cone105(excludeTeam: 0), results);

            CollectionAssert.AreEqual(new[] { enemy }, results);
        }

        // --- Visibility: any-point vs aim-point (the Deadeye miss) ---

        [Test]
        public void AnyPointVisible_PassesIfOnePointIsClear_ButAimPointCanStillBeBlocked()
        {
            var aim = new Vector3(0, 1.2f, 20);
            var knee = new Vector3(0, 0.6f, 20);
            var target = new FakeTarget { AimPosition = aim, Points = new[] { aim, new Vector3(0, 1.6f, 20), knee } };
            var los = new StubLos { clear = to => to == knee };   // only the knee point is exposed

            Assert.IsTrue(TargetQueries.AnyPointVisible(los, Vector3.zero, target), "charges: knee is exposed");
            Assert.IsFalse(TargetQueries.AimPointVisible(los, Vector3.zero, target), "but the shot would hit cover");
        }

        [Test]
        public void AnyPointVisible_FalseWhenEveryPointBlocked()
        {
            var target = new FakeTarget { AimPosition = Vector3.forward, Points = new[] { Vector3.forward } };
            Assert.IsFalse(TargetQueries.AnyPointVisible(new StubLos { clear = _ => false }, Vector3.zero, target));
        }

        // --- Physics LOS ---

        [Test]
        public void PhysicsLineOfSight_BlockedByWall_ClearForOwnColliders()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 0, 5);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.position = new Vector3(0, 0, 10);
            var other = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            other.transform.position = new Vector3(20, 0, 10);
            Physics.SyncTransforms();

            try
            {
                var los = new PhysicsLineOfSight(~0);
                Assert.IsFalse(los.IsClear(Vector3.zero, body.transform.position, body.transform), "wall in between");
                Assert.IsTrue(los.IsClear(new Vector3(0, 0, 7), body.transform.position, body.transform), "past the wall, hits own collider");
                Assert.IsFalse(los.IsClear(new Vector3(0, 0, 7), body.transform.position, other.transform), "someone else's collider blocks");
            }
            finally
            {
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void PhysicsLineOfSight_SkipsCasterColliders()
        {
            var caster = GameObject.CreatePrimitive(PrimitiveType.Capsule);   // ray starts outside it, passes through it
            caster.transform.position = new Vector3(0, 0, 2);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.position = new Vector3(0, 0, 10);
            Physics.SyncTransforms();

            try
            {
                Assert.IsFalse(new PhysicsLineOfSight(~0).IsClear(Vector3.zero, body.transform.position, body.transform), "caster blocks without ignore");
                var los = new PhysicsLineOfSight(~0, caster.transform);
                Assert.IsTrue(los.IsClear(Vector3.zero, body.transform.position, body.transform), "caster ignored");
                Assert.IsTrue(los.Raycast(Vector3.zero, Vector3.forward, 20f, out var hit));
                Assert.AreSame(body.GetComponent<Collider>(), hit.collider, "shot hits the target and ignores the caster");
            }
            finally
            {
                Object.DestroyImmediate(caster);
                Object.DestroyImmediate(body);
            }
        }

        // --- Health ---

        [Test]
        public void Health_DamageKills_RaisesEventsOnce_AndResets()
        {
            var go = new GameObject("hp", typeof(Health));
            try
            {
                var health = go.GetComponent<Health>();
                typeof(Health).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(health, null);
                int damaged = 0, died = 0;
                health.Damaged += (h, d) => damaged++;
                health.Died += (h, d) => died++;

                health.ApplyDamage(new DamageInfo(health.Max, 7));
                health.ApplyDamage(new DamageInfo(50f, 7));   // already dead, ignored

                Assert.IsFalse(health.IsAlive);
                Assert.AreEqual(1, damaged);
                Assert.AreEqual(1, died);

                health.ResetToFull();
                Assert.IsTrue(health.IsAlive);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
