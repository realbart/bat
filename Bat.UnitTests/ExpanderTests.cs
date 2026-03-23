using Bat.Context;
using Bat.Execution;

namespace Bat.UnitTests;

[TestClass]
public class ExpanderTests
{
    [TestClass]
    public class BatchParameterExpansion
    {
        [TestMethod]
        public void ExpandBatchParameters_NoParameters_ReturnsOriginal()
        {
            // Arrange
            var bc = new BatchContext();
            var line = "echo hello world";

            // Act
            var result = Expander.ExpandBatchParameters(line, bc);

            // Assert
            Assert.AreEqual("echo hello world", result);
        }

        [TestMethod]
        public void ExpandBatchParameters_SingleParameter_Expands()
        {
            // Arrange
            var bc = new BatchContext { Parameters = ["test.bat", "arg1", "arg2", null, null, null, null, null, null, null] };
            var line = "echo %1 and %2";

            // Act
            var result = Expander.ExpandBatchParameters(line, bc);

            // Assert
            Assert.AreEqual("echo arg1 and arg2", result);
        }

        [TestMethod]
        public void ExpandBatchParameters_NullParameter_RemainsLiteral()
        {
            // Arrange
            var bc = new BatchContext { Parameters = ["test.bat", null, null, null, null, null, null, null, null, null] };
            var line = "echo %1 and %2";

            // Act
            var result = Expander.ExpandBatchParameters(line, bc);

            // Assert
            Assert.AreEqual("echo %1 and %2", result);
        }

        [TestMethod]
        public void ExpandBatchParameters_MixedParameters_ExpandsOnlySet()
        {
            // Arrange
            var bc = new BatchContext { Parameters = ["test.bat", "arg1", null, "arg3", null, null, null, null, null, null] };
            var line = "echo %1 %2 %3";

            // Act
            var result = Expander.ExpandBatchParameters(line, bc);

            // Assert
            Assert.AreEqual("echo arg1 %2 arg3", result);
        }

        [TestMethod]
        public void ExpandBatchParameters_Parameter0_ExpandsToFileName()
        {
            // Arrange
            var bc = new BatchContext { Parameters = ["test.bat", "arg1", null, null, null, null, null, null, null, null] };
            var line = "echo Running %0 with %1";

            // Act
            var result = Expander.ExpandBatchParameters(line, bc);

            // Assert
            Assert.AreEqual("echo Running test.bat with arg1", result);
        }

        [TestMethod]
        public void ExpandBatchParameters_AllParameters_Expands()
        {
            // Arrange
            var bc = new BatchContext { Parameters = ["test.bat", "arg1", "arg2", "arg3", null, null, null, null, null, null] };
            var line = "echo %*";

            // Act
            var result = Expander.ExpandBatchParameters(line, bc);

            // Assert
            Assert.AreEqual("echo arg1 arg2 arg3", result);
        }

        [TestMethod]
        public void ExpandBatchParameters_AllParametersEmpty_ExpandsToEmpty()
        {
            // Arrange
            var bc = new BatchContext { Parameters = ["test.bat", null, null, null, null, null, null, null, null, null] };
            var line = "echo %*";

            // Act
            var result = Expander.ExpandBatchParameters(line, bc);

            // Assert
            Assert.AreEqual("echo ", result);
        }

        [TestMethod]
        public void ExpandBatchParameters_WithShift_OffsetsParameters()
        {
            // Arrange
            var bc = new BatchContext 
            { 
                Parameters = ["test.bat", "arg1", "arg2", "arg3", null, null, null, null, null, null],
                ShiftOffset = 1
            };
            var line = "echo %1 %2";

            // Act
            var result = Expander.ExpandBatchParameters(line, bc);

            // Assert
            Assert.AreEqual("echo arg2 arg3", result);
        }

        [TestMethod]
        public void ExpandBatchParameters_EmptyString_ReturnsEmpty()
        {
            // Arrange
            var bc = new BatchContext();
            var line = "";

            // Act
            var result = Expander.ExpandBatchParameters(line, bc);

            // Assert
            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void ExpandBatchParameters_NullString_ReturnsNull()
        {
            // Arrange
            var bc = new BatchContext();
            string? line = null;

            // Act
            var result = Expander.ExpandBatchParameters(line!, bc);

            // Assert
            Assert.IsNull(result);
        }
    }

    [TestClass]
    public class EnvironmentVariableExpansion
    {
        [TestMethod]
        public void ExpandEnvironmentVariables_NoVariables_ReturnsOriginal()
        {
            // Arrange
            var ctx = new DosContext(new DosFileSystem());
            var line = "echo hello world";

            // Act
            var result = Expander.ExpandEnvironmentVariables(line, ctx);

            // Assert
            Assert.AreEqual("echo hello world", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_SingleVariable_Expands()
        {
            // Arrange
            var ctx = new DosContext(new DosFileSystem());
            ctx.EnvironmentVariables["TEST"] = "value";
            var line = "echo %TEST%";

            // Act
            var result = Expander.ExpandEnvironmentVariables(line, ctx);

            // Assert
            Assert.AreEqual("echo value", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_UndefinedVariable_RemainsLiteral()
        {
            // Arrange
            var ctx = new DosContext(new DosFileSystem());
            var line = "echo %NOTFOUND%";

            // Act
            var result = Expander.ExpandEnvironmentVariables(line, ctx);

            // Assert
            Assert.AreEqual("echo %NOTFOUND%", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_MultipleVariables_Expands()
        {
            // Arrange
            var ctx = new DosContext(new DosFileSystem());
            ctx.EnvironmentVariables["VAR1"] = "value1";
            ctx.EnvironmentVariables["VAR2"] = "value2";
            var line = "echo %VAR1% and %VAR2%";

            // Act
            var result = Expander.ExpandEnvironmentVariables(line, ctx);

            // Assert
            Assert.AreEqual("echo value1 and value2", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_MixedDefinedAndUndefined_ExpandsOnlyDefined()
        {
            // Arrange
            var ctx = new DosContext(new DosFileSystem());
            ctx.EnvironmentVariables["DEFINED"] = "exists";
            var line = "echo %DEFINED% and %UNDEFINED%";

            // Act
            var result = Expander.ExpandEnvironmentVariables(line, ctx);

            // Assert
            Assert.AreEqual("echo exists and %UNDEFINED%", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_IgnoresBatchParameters()
        {
            // Arrange
            var ctx = new DosContext(new DosFileSystem());
            ctx.EnvironmentVariables["1"] = "should_not_expand";
            var line = "echo %1";

            // Act
            var result = Expander.ExpandEnvironmentVariables(line, ctx);

            // Assert
            Assert.AreEqual("echo %1", result); // Single digit should not be expanded as env var
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_EmptyVariable_Expands()
        {
            // Arrange
            var ctx = new DosContext(new DosFileSystem());
            ctx.EnvironmentVariables["EMPTY"] = "";
            var line = "echo %EMPTY%";

            // Act
            var result = Expander.ExpandEnvironmentVariables(line, ctx);

            // Assert
            Assert.AreEqual("echo ", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_EmptyString_ReturnsEmpty()
        {
            // Arrange
            var ctx = new DosContext(new DosFileSystem());
            var line = "";

            // Act
            var result = Expander.ExpandEnvironmentVariables(line, ctx);

            // Assert
            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_NullString_ReturnsNull()
        {
            // Arrange
            var ctx = new DosContext(new DosFileSystem());
            string? line = null;

            // Act
            var result = Expander.ExpandEnvironmentVariables(line!, ctx);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_UnclosedPercent_LeavesAsIs()
        {
            // Arrange
            var ctx = new DosContext(new DosFileSystem());
            ctx.EnvironmentVariables["TEST"] = "value";
            var line = "echo %TEST";

            // Act
            var result = Expander.ExpandEnvironmentVariables(line, ctx);

            // Assert
            Assert.AreEqual("echo %TEST", result);
        }
    }
}
