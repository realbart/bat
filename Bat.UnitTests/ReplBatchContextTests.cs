using Bat.Execution;

namespace Bat.UnitTests;

[TestClass]
public class ReplBatchContextTests
{
    [TestMethod]
    public void ReplBatchContext_Value_IsSingleton()
    {
        // Arrange & Act
        var bc1 = ReplBatchContext.Value;
        var bc2 = ReplBatchContext.Value;

        // Assert
        Assert.AreSame(bc1, bc2);
    }

    [TestMethod]
    public void ReplBatchContext_Value_IsReplMode()
    {
        // Arrange & Act
        var bc = ReplBatchContext.Value;

        // Assert
        Assert.IsNull(bc.BatchFilePath);
        Assert.IsTrue(bc.IsReplMode);
        Assert.IsFalse(bc.IsBatchFile);
    }

    [TestMethod]
    public void ReplBatchContext_Value_HasCmdAsParameter0()
    {
        // Arrange & Act
        var bc = ReplBatchContext.Value;

        // Assert
        Assert.IsNotNull(bc.Parameters);
        Assert.AreEqual(10, bc.Parameters.Length);
        Assert.AreEqual("CMD", bc.Parameters[0]);
    }

    [TestMethod]
    public void ReplBatchContext_Value_HasNullParameters1Through9()
    {
        // Arrange & Act
        var bc = ReplBatchContext.Value;

        // Assert
        for (int i = 1; i < bc.Parameters.Length; i++)
        {
            Assert.IsNull(bc.Parameters[i], $"Parameter {i} should be null");
        }
    }

    [TestMethod]
    public void ReplBatchContext_Value_HasNullLabelPositions()
    {
        // Arrange & Act
        var bc = ReplBatchContext.Value;

        // Assert
        Assert.IsNull(bc.LabelPositions);
    }

    [TestMethod]
    public void ReplBatchContext_UpdateLine_UpdatesFileContent()
    {
        // Arrange
        var testLine = "echo test";

        // Act
        ReplBatchContext.UpdateLine(testLine);
        var bc = ReplBatchContext.Value;

        // Assert
        Assert.AreEqual(testLine, bc.FileContent);
    }

    [TestMethod]
    public void ReplBatchContext_Value_HasEmptySetLocalStack()
    {
        // Arrange & Act
        var bc = ReplBatchContext.Value;

        // Assert
        Assert.IsNotNull(bc.SetLocalStack);
        Assert.AreEqual(0, bc.SetLocalStack.Count);
    }
}
