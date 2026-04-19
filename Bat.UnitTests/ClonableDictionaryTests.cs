using Bat.Context;

namespace Bat.UnitTests;

[TestClass]
public class ClonableDictionaryTests
{
    [TestMethod]
    public void Add_DuplicateKey_ShouldThrowArgumentException()
    {
        var dict = new ClonableDictionary<string, string>();
        dict.Add("key", "value");
        try
        {
            dict.Add("key", "value2");
            Assert.Fail("Should have thrown ArgumentException");
        }
        catch (ArgumentException) { }
    }

    [TestMethod]
    public void Indexer_Set_ShouldOverwriteExistingValue()
    {
        var dict = new ClonableDictionary<string, string>();
        dict["key"] = "value1";
        dict["key"] = "value2";
        Assert.AreEqual("value2", dict["key"]);
    }

    [TestMethod]
    public void Indexer_Get_MissingKey_ShouldThrowKeyNotFoundException()
    {
        var dict = new ClonableDictionary<string, string>();
        try
        {
            _ = dict["missing"];
            Assert.Fail("Should have thrown KeyNotFoundException");
        }
        catch (KeyNotFoundException) { }
    }

    [TestMethod]
    public void Remove_NonExistentKey_ShouldReturnFalse()
    {
        var dict = new ClonableDictionary<string, string>();
        Assert.IsFalse(dict.Remove("missing"));
    }

    [TestMethod]
    public void Remove_ExistingKey_ShouldReturnTrueAndRemoveKey()
    {
        var dict = new ClonableDictionary<string, string>();
        dict["key"] = "value";
        Assert.IsTrue(dict.Remove("key"));
        Assert.IsFalse(dict.ContainsKey("key"));
    }

    [TestMethod]
    public void Count_ShouldReflectCurrentNumberOfItems()
    {
        var dict = new ClonableDictionary<string, string>();
        Assert.AreEqual(0, dict.Count);
        dict["k1"] = "v1";
        Assert.AreEqual(1, dict.Count);
        dict["k2"] = "v2";
        Assert.AreEqual(2, dict.Count);
        dict.Remove("k1");
        Assert.AreEqual(1, dict.Count);
    }

    [TestMethod]
    public void Clear_ShouldRemoveAllItems()
    {
        var dict = new ClonableDictionary<string, string>();
        dict["k1"] = "v1";
        dict.Clear();
        Assert.AreEqual(0, dict.Count);
        Assert.IsFalse(dict.ContainsKey("k1"));
    }

    [TestMethod]
    public void Clone_ShouldBeIndependent()
    {
        var dict = new ClonableDictionary<string, string>();
        dict["k1"] = "v1";
        var clone = dict.Clone();
        
        Assert.AreEqual("v1", clone["k1"]);
        
        clone["k1"] = "v2";
        Assert.AreEqual("v1", dict["k1"]);
        Assert.AreEqual("v2", clone["k1"]);
        
        clone["k2"] = "v2";
        Assert.IsFalse(dict.ContainsKey("k2"));
    }

    [TestMethod]
    public void Keys_ShouldReturnAllKeys()
    {
        var dict = new ClonableDictionary<string, string>();
        dict["k1"] = "v1";
        dict["k2"] = "v2";
        var keys = dict.Keys;
        Assert.AreEqual(2, keys.Count);
        Assert.IsTrue(keys.Contains("k1"));
        Assert.IsTrue(keys.Contains("k2"));
    }

    [TestMethod]
    public void Values_ShouldReturnAllValues()
    {
        var dict = new ClonableDictionary<string, string>();
        dict["k1"] = "v1";
        dict["k2"] = "v2";
        var values = dict.Values;
        Assert.AreEqual(2, values.Count);
        Assert.IsTrue(values.Contains("v1"));
        Assert.IsTrue(values.Contains("v2"));
    }

    [TestMethod]
    public void TryGetValue_ShouldReturnTrueIfKeyExists()
    {
        var dict = new ClonableDictionary<string, string>();
        dict["k1"] = "v1";
        Assert.IsTrue(dict.TryGetValue("k1", out var val));
        Assert.AreEqual("v1", val);
    }

    [TestMethod]
    public void TryGetValue_ShouldReturnFalseIfKeyDoesNotExist()
    {
        var dict = new ClonableDictionary<string, string>();
        Assert.IsFalse(dict.TryGetValue("missing", out var val));
        Assert.IsNull(val);
    }
}
