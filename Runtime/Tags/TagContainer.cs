using System.Collections.Generic;

namespace Juris.Abilities
{
    /// <summary>
    /// Ref-counted runtime tag bag. Adding a tag twice needs two removes.
    /// <see cref="HasTag"/> is O(1): every add also counts the tag's ancestors.
    /// </summary>
    public sealed class TagContainer
    {
        private readonly Dictionary<GameplayTag, int> _explicit = new Dictionary<GameplayTag, int>();
        private readonly Dictionary<GameplayTag, int> _implicit = new Dictionary<GameplayTag, int>();

        public IEnumerable<GameplayTag> ExplicitTags => _explicit.Keys;

        public void Add(GameplayTag tag)
        {
            Increment(_explicit, tag, 1);
            for (var t = tag; t.IsValid; t = t.Parent) Increment(_implicit, t, 1);
        }

        public void Remove(GameplayTag tag)
        {
            if (!_explicit.ContainsKey(tag)) return;
            Increment(_explicit, tag, -1);
            for (var t = tag; t.IsValid; t = t.Parent) Increment(_implicit, t, -1);
        }

        public void Add(TagSet set)
        {
            var tags = set.Tags;
            for (int i = 0; i < tags.Count; i++) Add(tags[i]);
        }

        public void Remove(TagSet set)
        {
            var tags = set.Tags;
            for (int i = 0; i < tags.Count; i++) Remove(tags[i]);
        }

        /// <summary>True if <paramref name="tag"/> or any descendant of it is present.</summary>
        public bool HasTag(GameplayTag tag) => _implicit.ContainsKey(tag);

        public bool HasAny(TagSet set)
        {
            var tags = set.Tags;
            for (int i = 0; i < tags.Count; i++)
                if (HasTag(tags[i])) return true;
            return false;
        }

        /// <summary>True if any tag in <paramref name="set"/>, or an ancestor of one, was explicitly added.</summary>
        public bool ContainsAnyAncestorOf(TagSet set)
        {
            var tags = set.Tags;
            for (int i = 0; i < tags.Count; i++)
                for (var t = tags[i]; t.IsValid; t = t.Parent)
                    if (_explicit.ContainsKey(t)) return true;
            return false;
        }

        private static void Increment(Dictionary<GameplayTag, int> counts, GameplayTag tag, int delta)
        {
            counts.TryGetValue(tag, out int count);
            count += delta;
            if (count > 0) counts[tag] = count;
            else counts.Remove(tag);
        }
    }
}
