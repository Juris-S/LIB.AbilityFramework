namespace Juris.Abilities
{
    public enum AbilityCancelReason
    {
        Input,
        /// <summary>Cancelled by another ability's or effect's cancel tags.</summary>
        Interrupted,
        Removed,
        External,
    }

    /// <summary>A set of <see cref="AbilityCancelReason"/>s, for inspector masks.</summary>
    [System.Flags]
    public enum AbilityCancelReasonMask
    {
        None = 0,
        Input = 1 << (int)AbilityCancelReason.Input,
        Interrupted = 1 << (int)AbilityCancelReason.Interrupted,
        Removed = 1 << (int)AbilityCancelReason.Removed,
        External = 1 << (int)AbilityCancelReason.External,
    }

    public static class AbilityCancelReasonMaskExtensions
    {
        public static bool Includes(this AbilityCancelReasonMask mask, AbilityCancelReason reason) =>
            ((int)mask & (1 << (int)reason)) != 0;
    }

    /// <summary>
    /// One activation of an ability. Owns its own internal state machine.
    /// Executions are reused across activations: <see cref="Start"/> must fully reset state.
    /// Override ToString() to show internal state in the debug overlay.
    /// </summary>
    public interface IAbilityExecution
    {
        bool IsComplete { get; }

        void Start(in AbilityCommand command);
        void Tick(float deltaTime);
        void HandleCommand(in AbilityCommand command);

        /// <summary>Release everything acquired since Start (subscriptions, movement overrides, physics ownership).</summary>
        void Cancel(AbilityCancelReason reason);
    }
}
