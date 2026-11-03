using UnityEngine;

namespace Juris.Abilities
{
    public enum AbilityInputPhase
    {
        /// <summary>Activates an idle ability (buffered if blocked), or is forwarded to a running one.</summary>
        Pressed,
        Released,
        Confirm,
        /// <summary>Cancels the running execution.</summary>
        Cancel,
    }

    /// <summary>Input intent for one ability slot. Produced by input adapters, AI or replays.</summary>
    public readonly struct AbilityCommand
    {
        public readonly int Slot;
        public readonly AbilityInputPhase Phase;
        public readonly Ray Aim;

        public AbilityCommand(int slot, AbilityInputPhase phase, Ray aim = default)
        {
            Slot = slot;
            Phase = phase;
            Aim = aim;
        }
    }
}
