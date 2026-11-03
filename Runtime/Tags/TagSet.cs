using System;
using System.Collections.Generic;
using UnityEngine;

namespace Juris.Abilities
{
    /// <summary>Immutable, inspector-authored list of tags. Resolved to <see cref="GameplayTag"/>s on first use.</summary>
    [Serializable]
    public sealed class TagSet : ISerializationCallbackReceiver
    {
        [SerializeField] private string[] tags = Array.Empty<string>();

        [NonSerialized] private GameplayTag[] _resolved;

        public TagSet() { }

        public TagSet(params string[] tags) => this.tags = tags ?? Array.Empty<string>();

        public IReadOnlyList<GameplayTag> Tags
        {
            get
            {
                if (_resolved != null) return _resolved;
                _resolved = new GameplayTag[tags.Length];
                for (int i = 0; i < tags.Length; i++) _resolved[i] = GameplayTag.Get(tags[i]);
                return _resolved;
            }
        }

        public int Count => tags.Length;

        /// <summary>True if any tag in this set matches (is, or descends from) any tag in <paramref name="other"/>.</summary>
        public bool MatchesAny(TagSet other)
        {
            var mine = Tags;
            var theirs = other.Tags;
            for (int i = 0; i < mine.Count; i++)
                for (int j = 0; j < theirs.Count; j++)
                    if (mine[i].Matches(theirs[j])) return true;
            return false;
        }

        public override string ToString() => string.Join(", ", tags);

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
        void ISerializationCallbackReceiver.OnAfterDeserialize() => _resolved = null;
    }
}
