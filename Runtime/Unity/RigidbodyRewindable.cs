using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>
    /// Records a Rigidbody's pose and velocity every FixedUpdate into a ring buffer and winds it back on request.
    /// While rewinding the body is kinematic with interpolation off; both are restored by <see cref="EndRewind"/>,
    /// including when the component is disabled or destroyed mid-rewind.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RigidbodyRewindable : MonoBehaviour, IRewindable
    {
        [Tooltip("How far back the history reaches. Capacity is this divided by the fixed timestep.")]
        [SerializeField, Min(0.1f)] private float historySeconds = 5f;

        private struct Sample
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
        }

        private Rigidbody _body;
        private RewindBuffer<Sample> _history;
        private Sample _beginState;
        private bool _wasKinematic;
        private RigidbodyInterpolation _interpolation;
        private int _stepsBack;

        public int StepsRecorded => _history == null ? 0 : _history.Count;
        public bool IsRewinding { get; private set; }

        private void Awake() => EnsureInitialised();

        private void FixedUpdate() => RecordTick();

        // Also called lazily so the component works when created and driven before Awake (tests, edit mode).
        private void EnsureInitialised()
        {
            if (_history != null) return;
            _body = GetComponent<Rigidbody>();
            _history = new RewindBuffer<Sample>(Mathf.Max(1, Mathf.CeilToInt(historySeconds / Time.fixedDeltaTime)));
        }

        internal void RecordTick()
        {
            EnsureInitialised();
            if (!IsRewinding) _history.Push(Capture());
        }

        private void OnDisable() => EndRewind(false);

        public void BeginRewind()
        {
            EnsureInitialised();
            if (IsRewinding) return;
            IsRewinding = true;
            _stepsBack = 0;
            _beginState = Capture();
            _wasKinematic = _body.isKinematic;
            _interpolation = _body.interpolation;
            _body.interpolation = RigidbodyInterpolation.None;
            _body.isKinematic = true;
        }

        public bool Seek(int stepsBack)
        {
            if (!IsRewinding || _history.Count == 0) return false;
            _stepsBack = Mathf.Clamp(stepsBack, 0, _history.Count - 1);
            ApplyPose(_history.Get(_stepsBack));
            return _stepsBack == stepsBack;
        }

        public void EndRewind(bool commit)
        {
            if (!IsRewinding) return;
            IsRewinding = false;
            _body.isKinematic = _wasKinematic;
            _body.interpolation = _interpolation;

            Sample final = commit && _history.Count > 0 ? _history.Get(_stepsBack) : _beginState;
            if (commit) _history.DiscardNewest(_stepsBack);
            ApplyPose(final);
            if (!_body.isKinematic) // velocity can't be set on a kinematic body
            {
                _body.linearVelocity = final.Velocity;
                _body.angularVelocity = final.AngularVelocity;
            }
        }

        private Sample Capture() => new Sample
        {
            Position = _body.position,
            Rotation = _body.rotation,
            Velocity = _body.linearVelocity,
            AngularVelocity = _body.angularVelocity,
        };

        private void ApplyPose(in Sample sample) => Teleporter.Teleport(transform, sample.Position, sample.Rotation);
    }
}
