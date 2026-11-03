using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>
    /// Raycast line of sight. The nearest collider hit decides: the target's own colliders count as clear.
    /// The caster's own colliders are skipped entirely (camera inside its capsule, its own model).
    /// </summary>
    public sealed class PhysicsLineOfSight : ILineOfSight
    {
        // Limitation: 16 hits per ray; a ray through more colliders than that can miss the nearest one. Grow if it ever matters.
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly LayerMask _blockers;
        private readonly Transform _ignoreRoot;

        /// <param name="ignoreRoot">The caster. Colliders under it never block or get hit. May be null.</param>
        public PhysicsLineOfSight(LayerMask blockers, Transform ignoreRoot = null)
        {
            _blockers = blockers;
            _ignoreRoot = ignoreRoot;
        }

        public bool IsClear(Vector3 from, Vector3 to, Transform targetRoot)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1e-4f) return true;

            if (!Raycast(from, delta / distance, distance, out var hit)) return true;
            return targetRoot != null && hit.collider.transform.IsChildOf(targetRoot);
        }

        /// <summary>Nearest hit that is not part of the caster. Use it for shots so they follow the same rules as line of sight.</summary>
        public bool Raycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, _blockers, QueryTriggerInteraction.Ignore);
            nearest = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (found && hit.distance >= nearest.distance) continue;
                if (_ignoreRoot != null && hit.collider.transform.IsChildOf(_ignoreRoot)) continue;
                nearest = hit;
                found = true;
            }
            return found;
        }
    }
}
