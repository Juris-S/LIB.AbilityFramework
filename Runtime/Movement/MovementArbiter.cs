using System.Collections.Generic;
using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>Drives the character while it holds control of <see cref="MovementArbiter"/> (dashes, rolls, knockbacks).</summary>
    public interface IMovementOverride
    {
        /// <summary>World-space velocity for this frame. Replaces input and gravity while this override is in control.</summary>
        Vector3 GetVelocity(float deltaTime);
    }

    /// <summary>
    /// Decides who moves the character. Abilities request control or a speed change and release it by handle;
    /// the game's motor asks <see cref="TryGetVelocity"/> each frame and falls back to normal movement when it returns false.
    /// Abilities never write transforms.
    /// </summary>
    public sealed class MovementArbiter
    {
        private struct Request
        {
            public int Handle;
            public int Priority;
            public double EndTime;
            public IMovementOverride Override;
        }

        private struct SpeedMultiplier
        {
            public int Handle;
            public float Factor;
        }

        private readonly IGameClock _clock;
        private readonly List<Request> _requests = new List<Request>(2);
        private readonly List<SpeedMultiplier> _speedMultipliers = new List<SpeedMultiplier>(2);
        private int _nextHandle = 1;

        public MovementArbiter(IGameClock clock) => _clock = clock;

        /// <summary>
        /// Asks for control. The highest priority wins; on a tie the newest request wins.
        /// Expires after <paramref name="duration"/> seconds (default: until released). Returns a handle for <see cref="Release"/>.
        /// </summary>
        public int RequestControl(IMovementOverride movement, int priority, float duration = float.PositiveInfinity)
        {
            int handle = _nextHandle++;
            _requests.Add(new Request { Handle = handle, Priority = priority, EndTime = _clock.Time + duration, Override = movement });
            return handle;
        }

        /// <summary>Releases a control request or a speed multiplier.</summary>
        public bool Release(int handle)
        {
            for (int i = 0; i < _requests.Count; i++)
            {
                if (_requests[i].Handle != handle) continue;
                _requests.RemoveAt(i);
                return true;
            }
            for (int i = 0; i < _speedMultipliers.Count; i++)
            {
                if (_speedMultipliers[i].Handle != handle) continue;
                _speedMultipliers.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>True while the request exists and has not expired (it may still be outranked).</summary>
        public bool IsActive(int handle)
        {
            RemoveExpired();
            for (int i = 0; i < _requests.Count; i++)
                if (_requests[i].Handle == handle) return true;
            return false;
        }

        /// <summary>The override in control, or null when the motor should use normal movement.</summary>
        public IMovementOverride Current
        {
            get
            {
                RemoveExpired();
                int best = -1;
                for (int i = 0; i < _requests.Count; i++)
                    if (best < 0 || _requests[i].Priority >= _requests[best].Priority) best = i;
                return best >= 0 ? _requests[best].Override : null;
            }
        }

        public bool TryGetVelocity(float deltaTime, out Vector3 velocity)
        {
            var current = Current;
            velocity = current != null ? current.GetVelocity(deltaTime) : Vector3.zero;
            return current != null;
        }

        /// <summary>Scales normal movement speed (0.5 = half). Multipliers multiply with each other. Returns a handle for <see cref="Release"/>.</summary>
        public int AddSpeedMultiplier(float factor)
        {
            int handle = _nextHandle++;
            _speedMultipliers.Add(new SpeedMultiplier { Handle = handle, Factor = factor });
            return handle;
        }

        /// <summary>Product of every speed multiplier; 1 when there are none.</summary>
        public float SpeedMultiplierTotal
        {
            get
            {
                float total = 1f;
                for (int i = 0; i < _speedMultipliers.Count; i++) total *= _speedMultipliers[i].Factor;
                return total;
            }
        }

        private void RemoveExpired()
        {
            double now = _clock.Time;
            for (int i = _requests.Count - 1; i >= 0; i--)
                if (now >= _requests[i].EndTime) _requests.RemoveAt(i);
        }
    }
}
