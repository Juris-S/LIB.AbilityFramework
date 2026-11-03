using System;
using NUnit.Framework;

namespace Juris.Abilities.Tests
{
    public class GameplayTagTests
    {
        [Test]
        public void SameName_InternsToEqualTag()
        {
            Assert.AreEqual(GameplayTag.Get("Test.Tag.A"), GameplayTag.Get(" Test.Tag.A "));
        }

        [Test]
        public void Child_MatchesParent_NotViceVersa()
        {
            var child = GameplayTag.Get("Test.Parent.Child");
            var parent = GameplayTag.Get("Test.Parent");

            Assert.IsTrue(child.Matches(parent));
            Assert.IsTrue(child.Matches(child));
            Assert.IsFalse(parent.Matches(child));
            Assert.IsFalse(GameplayTag.Get("Test.Parentish").Matches(parent));
        }

        [TestCase("")]
        [TestCase(".Leading")]
        [TestCase("Trailing.")]
        public void MalformedName_Throws(string name)
        {
            Assert.Throws<ArgumentException>(() => GameplayTag.Get(name));
        }

        [Test]
        public void Container_HasTag_MatchesDescendants_AndRefCounts()
        {
            var container = new TagContainer();
            var stunned = GameplayTag.Get("Test.State.Stunned");
            var state = GameplayTag.Get("Test.State");

            container.Add(stunned);
            container.Add(stunned);
            Assert.IsTrue(container.HasTag(state));

            container.Remove(stunned);
            Assert.IsTrue(container.HasTag(stunned), "still one reference left");

            container.Remove(stunned);
            Assert.IsFalse(container.HasTag(stunned));
            Assert.IsFalse(container.HasTag(state));
        }
    }
}
