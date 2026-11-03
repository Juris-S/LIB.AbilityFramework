using System.Collections.Generic;
using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>Supplies the targets abilities can consider. Games register one in AbilityServices.</summary>
    public interface ITargetSource
    {
        IReadOnlyList<ITargetable> Targets { get; }
    }

    /// <summary>Answers "can this ray reach that point". Separate from shape and filtering.</summary>
    public interface ILineOfSight
    {
        /// <param name="targetRoot">Colliders under this transform do not block. May be null.</param>
        bool IsClear(Vector3 from, Vector3 to, Transform targetRoot);
    }

    /// <summary>Cone-shaped query. <see cref="FullAngle"/> is the total opening in degrees.</summary>
    public readonly struct TargetQuery
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Forward;
        public readonly float FullAngle;
        public readonly float Range;

        /// <summary>Targets on this team are skipped (the caster's own team). Use -1 to skip nobody.</summary>
        public readonly int ExcludeTeam;

        public TargetQuery(Vector3 origin, Vector3 forward, float fullAngle, float range, int excludeTeam = -1)
        {
            Origin = origin;
            Forward = forward.normalized;
            FullAngle = fullAngle;
            Range = range;
            ExcludeTeam = excludeTeam;
        }

        /// <summary>True if <paramref name="point"/> is within range and inside the cone.</summary>
        public bool Contains(Vector3 point)
        {
            Vector3 delta = point - Origin;
            float sqr = delta.sqrMagnitude;
            if (sqr > Range * Range) return false;
            if (sqr < 1e-6f) return true;
            float cosHalf = Mathf.Cos(FullAngle * 0.5f * Mathf.Deg2Rad);
            return Vector3.Dot(Forward, delta / Mathf.Sqrt(sqr)) >= cosHalf;
        }
    }

    public static class TargetQueries
    {
        /// <summary>Appends targetable, in-cone, in-range targets (by aim position). Does not clear <paramref name="results"/>.</summary>
        public static void Find(ITargetSource source, in TargetQuery query, List<ITargetable> results)
        {
            var targets = source.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!target.IsTargetable || target.Team == query.ExcludeTeam) continue;
                if (query.Contains(target.AimPosition)) results.Add(target);
            }
        }

        /// <summary>True if any visibility point of <paramref name="target"/> is reachable from <paramref name="origin"/>.</summary>
        public static bool AnyPointVisible(ILineOfSight los, Vector3 origin, ITargetable target)
        {
            for (int i = 0; i < target.VisibilityPointCount; i++)
                if (los.IsClear(origin, target.GetVisibilityPoint(i), target.Root)) return true;
            return false;
        }

        /// <summary>True if the aim point itself is reachable, i.e. a shot would not be blocked.</summary>
        public static bool AimPointVisible(ILineOfSight los, Vector3 origin, ITargetable target) =>
            los.IsClear(origin, target.AimPosition, target.Root);
    }
}
