using System.Collections.Generic;
using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>
    /// Scene component that makes a GameObject an <see cref="ITargetable"/>.
    /// Assign hardpoint transforms from the rig; defaults fall back to offsets above the root.
    /// </summary>
    public sealed class Targetable : MonoBehaviour, ITargetable
    {
        private static readonly List<ITargetable> s_active = new List<ITargetable>();
        private static int s_nextId = 1;

        /// <summary>Every enabled Targetable in the scene.</summary>
        public static IReadOnlyList<ITargetable> Active => s_active;

        [SerializeField] private int team;
        [Tooltip("Where shots go (chest).")]
        [SerializeField] private Transform aimPoint;
        [Tooltip("Where UI markers go (head).")]
        [SerializeField] private Transform indicatorPoint;
        [Tooltip("Points tested for line of sight. Empty = aim point only.")]
        [SerializeField] private Transform[] visibilityPoints = System.Array.Empty<Transform>();

        private Health _health;

        public int Id { get; private set; }
        public int Team => team;

        /// <summary>Set false to hide this target from abilities (invisibility, spawn protection).</summary>
        public bool Hidden { get; set; }

        public bool IsTargetable => isActiveAndEnabled && !Hidden && (_health == null || _health.IsAlive);
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + Vector3.up * 1.2f;
        public Vector3 IndicatorPosition => indicatorPoint != null ? indicatorPoint.position : transform.position + Vector3.up * 1.9f;
        public Transform Root => transform;

        public int VisibilityPointCount => visibilityPoints.Length > 0 ? visibilityPoints.Length : 1;

        public Vector3 GetVisibilityPoint(int index) =>
            visibilityPoints.Length > 0 ? visibilityPoints[index].position : AimPosition;

        private void Awake()
        {
            Id = s_nextId++;
            _health = GetComponent<Health>();
        }

        private void OnEnable() => s_active.Add(this);
        private void OnDisable() => s_active.Remove(this);

        // Domain reload is off in some projects; reset statics so stale targets never leak between play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_active.Clear();
            s_nextId = 1;
        }
    }

    /// <summary>Register in <see cref="AbilityServices"/> to expose every scene <see cref="Targetable"/>.</summary>
    public sealed class SceneTargetSource : ITargetSource
    {
        public IReadOnlyList<ITargetable> Targets => Targetable.Active;
    }
}
