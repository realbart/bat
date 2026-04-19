using Bat.Context;

namespace Bat.UnitTests;

[TestClass]
public class ClonableDictionaryReproductionTests
{
    [TestMethod]
    public void Clone_ShouldBeTrulyIndependent_EvenAfterModifications()
    {
        // Setup
        var original = new ClonableDictionary<string, string>();
        original["key1"] = "value1";

        // Clone
        var clone = original.Clone();

        // Verify clone has original values
        Assert.AreEqual("value1", clone["key1"]);

        // Modify clone
        clone["key1"] = "modified1";
        clone["key2"] = "value2";

        // BUG 1: Modification of clone SHOULD NOT affect original
        Assert.AreEqual("value1", original["key1"], "Modifying clone affected original!");
        Assert.IsFalse(original.ContainsKey("key2"), "Adding to clone added to original!");

        // Modify original
        original["key3"] = "value3";

        // BUG 2: Modification of original SHOULD NOT affect clone
        Assert.IsFalse(clone.ContainsKey("key3"), "Adding to original affected clone!");
    }

    [TestMethod]
    public void CloneOfEmptyDictionary_ShouldWork()
    {
        var original = new ClonableDictionary<string, string>();
        var clone = original.Clone();
        
        clone["key1"] = "value1";
        Assert.IsFalse(original.ContainsKey("key1"), "Adding to clone of empty dictionary affected original!");
    }

    [TestMethod]
    public void AddAfterClone_ShouldNotAffectOriginal()
    {
        var original = new ClonableDictionary<string, string>();
        original["k1"] = "v1";

        var clone = original.Clone();
        
        // This 'Add' in current implementation will go into original's current stack's dictionary
        // BECAUSE 'Clone' didn't push a new layer on original IF it was already empty?
        // Wait, if original["k1"] = "v1" was called, original._stack.Dictionary.Count is 1.
        // Clone() calls _stack = new(_stack) on ORIGINAL.
        // Then it returns new ClonableDictionary(new Stack(_stack.Parent)).
        // So 'clone' points to a new Stack that has same Parent as original's NEW stack.
        
        clone.Add("k2", "v2");
        
        Assert.IsFalse(original.ContainsKey("k2"), "Addition to clone should not affect original");
    }
}
