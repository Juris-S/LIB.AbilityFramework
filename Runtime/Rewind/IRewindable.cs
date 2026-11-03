namespace Juris.Abilities
{
    /// <summary>
    /// Something that records its state every fixed tick and can be wound back by an ability (Recall, Tracer's Recall).
    /// The contract is explicit so a cancelled ability always leaves the object in a valid state:
    /// <c>BeginRewind</c> takes ownership, <c>EndRewind</c> gives it back. An execution must call
    /// <c>EndRewind</c> from both normal completion and <c>Cancel</c>.
    /// </summary>
    public interface IRewindable
    {
        /// <summary>Ticks of history available to rewind through.</summary>
        int StepsRecorded { get; }

        bool IsRewinding { get; }

        /// <summary>Takes ownership of the object's physics and stops recording. No-op if already rewinding.</summary>
        void BeginRewind();

        /// <summary>
        /// Applies the state recorded <paramref name="stepsBack"/> ticks ago. Clamped to the oldest sample.
        /// Returns false once that limit is reached. Only valid while rewinding.
        /// </summary>
        bool Seek(int stepsBack);

        /// <summary>
        /// Gives physics ownership back. Commit keeps the current seek position, restores its velocity and drops the
        /// history after it. Cancel (<c>commit = false</c>) puts the object back exactly as it was at
        /// <see cref="BeginRewind"/>. No-op when not rewinding.
        /// </summary>
        void EndRewind(bool commit);
    }
}
