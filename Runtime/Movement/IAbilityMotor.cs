using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>
    /// The game's character mover, registered in <see cref="AbilityServices"/> as IAbilityMotor.
    /// Each frame it moves by <see cref="MovementArbiter.TryGetVelocity"/> when an override is in control, otherwise by
    /// input scaled with <see cref="MovementArbiter.SpeedMultiplierTotal"/>.
    /// </summary>
    public interface IAbilityMotor
    {
        /// <summary>
        /// Moves the character instantly with no interpolation in between.
        /// Implement with <see cref="Teleporter.Teleport"/>, then sync any state the motor keeps itself (yaw, camera).
        /// </summary>
        void Teleport(Vector3 position, Quaternion rotation);
    }

    public static class Teleporter
    {
        /// <summary>
        /// Moves <paramref name="body"/> instantly. Keeps a CharacterController or Rigidbody on it in sync so the next
        /// physics step does not drag it back or interpolate from the old pose. Rigidbody velocity is kept.
        /// </summary>
        public static void Teleport(Transform body, Vector3 position, Quaternion rotation)
        {
            if (body.TryGetComponent(out Rigidbody rigidbody))
            {
                rigidbody.position = position;
                rigidbody.rotation = rotation;
            }
            body.SetPositionAndRotation(position, rotation);
            // A CharacterController keeps its own copy of the position until transforms are synced; teleports are rare.
            Physics.SyncTransforms();
        }
    }
}
