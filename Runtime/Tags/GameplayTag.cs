using System;
using System.Collections.Generic;

namespace Juris.Abilities
{
    /// <summary>
    /// Hierarchical tag ("State.Stunned", "Ability.Movement.Blink") interned to an int.
    /// A tag matches itself and every ancestor: "Ability.Movement.Blink" matches "Ability.Movement".
    /// </summary>
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        // Index 0 is the invalid/none tag.
        private static readonly Dictionary<string, int> s_ids = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly List<string> s_names = new List<string> { string.Empty };
        private static readonly List<int> s_parents = new List<int> { 0 };

        private readonly int _id;

        private GameplayTag(int id) => _id = id;

        public static GameplayTag None => default;

        public bool IsValid => _id != 0;
        public string Name => s_names[_id];
        public GameplayTag Parent => new GameplayTag(s_parents[_id]);

        /// <summary>Interns <paramref name="name"/> and all its ancestors.</summary>
        public static GameplayTag Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tag name is empty.", nameof(name));
            name = name.Trim();
            if (s_ids.TryGetValue(name, out int id)) return new GameplayTag(id);

            int dot = name.LastIndexOf('.');
            if (dot == 0 || dot == name.Length - 1) throw new ArgumentException($"Malformed tag '{name}'.", nameof(name));
            int parent = dot < 0 ? 0 : Get(name.Substring(0, dot))._id;

            id = s_names.Count;
            s_names.Add(name);
            s_parents.Add(parent);
            s_ids.Add(name, id);
            return new GameplayTag(id);
        }

        /// <summary>True if this tag is <paramref name="other"/> or a descendant of it.</summary>
        public bool Matches(GameplayTag other)
        {
            if (!other.IsValid) return false;
            for (int id = _id; id != 0; id = s_parents[id])
                if (id == other._id) return true;
            return false;
        }

        public bool Equals(GameplayTag other) => _id == other._id;
        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
        public override int GetHashCode() => _id;
        public override string ToString() => IsValid ? Name : "<none>";

        public static bool operator ==(GameplayTag a, GameplayTag b) => a._id == b._id;
        public static bool operator !=(GameplayTag a, GameplayTag b) => a._id != b._id;
    }
}
