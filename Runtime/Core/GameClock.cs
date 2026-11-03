namespace Juris.Abilities
{
    /// <summary>Simulation time source. Ability logic never reads UnityEngine.Time directly.</summary>
    public interface IGameClock
    {
        /// <summary>Scaled seconds since the last tick.</summary>
        float DeltaTime { get; }

        /// <summary>Scaled seconds since start.</summary>
        double Time { get; }
    }

    public sealed class UnityGameClock : IGameClock
    {
        public float DeltaTime => UnityEngine.Time.deltaTime;
        public double Time => UnityEngine.Time.timeAsDouble;
    }

    /// <summary>Clock advanced by hand. For tests, replays and headless simulation.</summary>
    public sealed class ManualGameClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public double Time { get; private set; }

        public void Advance(float seconds)
        {
            DeltaTime = seconds;
            Time += seconds;
        }
    }
}
