using Bat.Execution;

namespace Bat.UnitTests;

[TestClass]
public class BatchContextTests
{
    [TestMethod]
    public void BatchContext_DefaultConstruction_InitializesCorrectly()
    {
        // Arrange & Act
        var bc = new BatchContext();

        // Assert
        Assert.IsNull(bc.BatchFilePath);
        Assert.IsNull(bc.FileContent);
        Assert.AreEqual(0, bc.FilePosition);
        Assert.AreEqual(0, bc.LineNumber);
        Assert.IsNotNull(bc.Parameters);
        Assert.HasCount(10, bc.Parameters);
        Assert.AreEqual(0, bc.ShiftOffset);
        Assert.IsNotNull(bc.SetLocalStack);
        Assert.IsEmpty(bc.SetLocalStack);
        Assert.IsNull(bc.prev);
        Assert.IsNull(bc.LabelPositions);
    }

    [TestMethod]
    public void BatchContext_IsReplMode_TrueWhenNoFilePath()
    {
        // Arrange
        var bc = new BatchContext { BatchFilePath = null };

        // Act & Assert
        Assert.IsTrue(bc.IsReplMode);
        Assert.IsFalse(bc.IsBatchFile);
    }

    [TestMethod]
    public void BatchContext_IsBatchFile_TrueWhenFilePathSet()
    {
        // Arrange
        var bc = new BatchContext { BatchFilePath = "test.bat" };

        // Act & Assert
        Assert.IsFalse(bc.IsReplMode);
        Assert.IsTrue(bc.IsBatchFile);
    }

    [TestMethod]
    public void BatchContext_Parameters_CanBeSet()
    {
        // Arrange
        var bc = new BatchContext();

        // Act
        bc.Parameters[0] = "test.bat";
        bc.Parameters[1] = "arg1";
        bc.Parameters[2] = "arg2";

        // Assert
        Assert.AreEqual("test.bat", bc.Parameters[0]);
        Assert.AreEqual("arg1", bc.Parameters[1]);
        Assert.AreEqual("arg2", bc.Parameters[2]);
    }

    [TestMethod]
    public void BatchContext_SetLocalStack_CanPushAndPop()
    {
        // Arrange
        var bc = new BatchContext();
        var snapshot = new EnvironmentSnapshot(
            new Dictionary<string, string> { ["TEST"] = "value" },
            new Dictionary<char, string[]> { ['C'] = ["Users", "Test"] },
            false
        );

        // Act
        bc.SetLocalStack.Push(snapshot);

        // Assert
        Assert.AreEqual(1, bc.SetLocalStack.Count);
        var popped = bc.SetLocalStack.Pop();
        Assert.AreEqual(snapshot, popped);
        Assert.AreEqual(0, bc.SetLocalStack.Count);
    }

    [TestMethod]
    public void BatchContext_CallNesting_CanBeLinked()
    {
        // Arrange
        var parent = new BatchContext { BatchFilePath = "parent.bat" };
        var child = new BatchContext { BatchFilePath = "child.bat", prev = parent };

        // Assert
        Assert.AreEqual(parent, child.prev);
        Assert.IsNull(parent.prev);
    }

    [TestMethod]
    public void EnvironmentSnapshot_StoresAllData()
    {
        // Arrange
        var vars = new Dictionary<string, string> { ["VAR1"] = "value1", ["VAR2"] = "value2" };
        var paths = new Dictionary<char, string[]> { ['C'] = ["Windows"], ['D'] = ["Data"] };
        var delayedExpansion = true;

        // Act
        var snapshot = new EnvironmentSnapshot(vars, paths, delayedExpansion);

        // Assert
        Assert.AreEqual(vars, snapshot.Variables);
        Assert.AreEqual(paths, snapshot.Paths);
        Assert.AreEqual(delayedExpansion, snapshot.DelayedExpansion);
    }
}
