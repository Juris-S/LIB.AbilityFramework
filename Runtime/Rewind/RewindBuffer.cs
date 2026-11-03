using System;

namespace Juris.Abilities
{
    /// <summary>
    /// Fixed-capacity ring buffer of per-tick samples. Pushing past capacity drops the oldest sample.
    /// Index 0 is the newest sample.
    /// </summary>
    public sealed class RewindBuffer<T> where T : struct
    {
        private readonly T[] _samples;
        private int _head; // next write slot
        private int _count;

        public RewindBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _samples = new T[capacity];
        }

        public int Count => _count;
        public int Capacity => _samples.Length;

        public void Push(in T sample)
        {
            _samples[_head] = sample;
            _head = (_head + 1) % _samples.Length;
            if (_count < _samples.Length) _count++;
        }

        /// <summary>The sample <paramref name="stepsBack"/> ticks before the newest. Clamped to the oldest sample.</summary>
        public T Get(int stepsBack)
        {
            if (_count == 0) throw new InvalidOperationException("Buffer is empty.");
            if (stepsBack < 0) stepsBack = 0;
            if (stepsBack > _count - 1) stepsBack = _count - 1;
            int index = (_head - 1 - stepsBack) % _samples.Length;
            if (index < 0) index += _samples.Length;
            return _samples[index];
        }

        /// <summary>Drops the newest <paramref name="steps"/> samples, e.g. history after a committed rewind.</summary>
        public void DiscardNewest(int steps)
        {
            if (steps <= 0) return;
            if (steps > _count) steps = _count;
            _head = (_head - steps) % _samples.Length;
            if (_head < 0) _head += _samples.Length;
            _count -= steps;
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
        }
    }
}
