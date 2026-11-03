using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>
    /// Something abilities can target. Positions are read live, so they follow animation.
    /// Visibility points are separate from the aim point on purpose: acquisition can use several
    /// points while a shot always goes to <see cref="AimPosition"/>.
    /// </summary>
    public interface ITargetable
    {
        /// <summary>Stable actor id, unique per target.</summary>
        int Id { get; }
        int Team { get; }

        /// <summary>False while dead, invisible or otherwise untargetable.</summary>
        bool IsTargetable { get; }

        /// <summary>Where shots and projectiles go (e.g. chest).</summary>
        Vector3 AimPosition { get; }

        /// <summary>Where UI markers go (e.g. head).</summary>
        Vector3 IndicatorPosition { get; }

        /// <summary>Colliders under this root never block line of sight to this target.</summary>
        Transform Root { get; }

        int VisibilityPointCount { get; }
        Vector3 GetVisibilityPoint(int index);
    }
}
