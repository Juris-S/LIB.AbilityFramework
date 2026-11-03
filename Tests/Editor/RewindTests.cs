using NUnit.Framework;
using UnityEngine;

namespace Juris.Abilities.Tests
{
    public class RewindTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // --- RewindBuffer ---

        [Test]
        public void Buffer_GetCountsBackFromNewest()
        {
            var buffer = new RewindBuffer<int>(4);
            buffer.Push(1); buffer.Push(2); buffer.Push(3);

            Assert.AreEqual(3, buffer.Get(0));
            Assert.AreEqual(1, buffer.Get(2));
            Assert.AreEqual(3, buffer.Count);
        }

        [Test]
        public void Buffer_DropsOldestPastCapacity()
        {
            var buffer = new RewindBuffer<int>(3);
            for (int i = 1; i <= 5; i++) buffer.Push(i);

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(5, buffer.Get(0));
            Assert.AreEqual(3, buffer.Get(2));
        }

        [Test]
        public void Buffer_GetClampsToOldest()
        {
            var buffer = new RewindBuffer<int>(4);
            buffer.Push(7); buffer.Push(8);

            Assert.AreEqual(7, buffer.Get(99));
        }

        [Test]
        public void Buffer_DiscardNewest_ThenPushContinuesFromThere()
        {
            var buffer = new RewindBuffer<int>(4);
            for (int i = 1; i <= 6; i++) buffer.Push(i); // holds 3,4,5,6

            buffer.DiscardNewest(2);
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(4, buffer.Get(0));

            buffer.Push(9);
            Assert.AreEqual(9, buffer.Get(0));
            Assert.AreEqual(4, buffer.Get(1));
        }

        // --- RigidbodyRewindable ---

        // Fills history by hand: FixedUpdate does not run in EditMode tests.
        private RigidbodyRewindable CreateRewindable(out Rigidbody body, int samples)
        {
            _go = new GameObject("Rewindable");
            body = _go.AddComponent<Rigidbody>();
            body.useGravity = false;
            var rewindable = _go.AddComponent<RigidbodyRewindable>();
            for (int i = 0; i < samples; i++)
            {
                body.position = new Vector3(i, 0f, 0f);
                _go.transform.position = body.position;
                body.linearVelocity = new Vector3(0f, i, 0f);
                rewindable.RecordTick();
            }
            return rewindable;
        }

        [Test]
        public void Rewind_SeekMovesBodyBack_AndCommitKeepsPositionAndVelocity()
        {
            var rewindable = CreateRewindable(out var body, 5); // x = 0..4, vy = 0..4

            rewindable.BeginRewind();
            Assert.IsTrue(body.isKinematic);
            rewindable.Seek(2);
            Assert.AreEqual(2f, _go.transform.position.x, 1e-4f);

            rewindable.EndRewind(true);

            Assert.IsFalse(body.isKinematic);
            Assert.AreEqual(2f, body.position.x, 1e-4f);
            Assert.AreEqual(2f, body.linearVelocity.y, 1e-4f);
            Assert.AreEqual(3, rewindable.StepsRecorded); // newest two dropped
        }

        [Test]
        public void Rewind_CancelRestoresStateFromBegin_AndKeepsHistory()
        {
            var rewindable = CreateRewindable(out var body, 5);
            body.position = new Vector3(10f, 0f, 0f);
            _go.transform.position = body.position;
            body.linearVelocity = new Vector3(0f, 0f, 3f);

            rewindable.BeginRewind();
            rewindable.Seek(4);
            rewindable.EndRewind(false);

            Assert.IsFalse(body.isKinematic);
            Assert.AreEqual(10f, body.position.x, 1e-4f);
            Assert.AreEqual(3f, body.linearVelocity.z, 1e-4f);
            Assert.AreEqual(5, rewindable.StepsRecorded);
        }

        [Test]
        public void Rewind_RestoresOriginalKinematicAndInterpolation()
        {
            var rewindable = CreateRewindable(out var body, 2);
            body.interpolation = RigidbodyInterpolation.Interpolate;

            rewindable.BeginRewind();
            Assert.AreEqual(RigidbodyInterpolation.None, body.interpolation);
            rewindable.EndRewind(false);

            Assert.IsFalse(body.isKinematic);
            Assert.AreEqual(RigidbodyInterpolation.Interpolate, body.interpolation);
        }

        [Test]
        public void Rewind_SeekPastOldest_ClampsAndReportsFalse()
        {
            var rewindable = CreateRewindable(out _, 3);

            rewindable.BeginRewind();

            Assert.IsFalse(rewindable.Seek(50));
            Assert.AreEqual(0f, _go.transform.position.x, 1e-4f);
            rewindable.EndRewind(false);
        }

        [Test]
        public void Rewind_EndWithoutBegin_IsNoOp()
        {
            var rewindable = CreateRewindable(out var body, 2);

            rewindable.EndRewind(true);

            Assert.IsFalse(rewindable.IsRewinding);
            Assert.AreEqual(2, rewindable.StepsRecorded);
            Assert.IsFalse(body.isKinematic);
        }
    }
}
