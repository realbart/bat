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
            var bc = new BatchContext { Console = null!, Context = null! };
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
            var bc = new BatchContext { Console = null!, Context = null!, Parameters = ["test.bat", "arg1", "arg2", null, null, null, null, null, null, null] };
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
            var bc = new BatchContext { Console = null!, Context = null!, Parameters = ["test.bat", null, null, null, null, null, null, null, null, null] };
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
            var bc = new BatchContext { Console = null!, Context = null!, Parameters = ["test.bat", "arg1", null, "arg3", null, null, null, null, null, null] };
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
            var bc = new BatchContext { Console = null!, Context = null!, Parameters = ["test.bat", "arg1", null, null, null, null, null, null, null, null] };
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
            var bc = new BatchContext { Console = null!, Context = null!, Parameters = ["test.bat", "arg1", "arg2", "arg3", null, null, null, null, null, null] };
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
            var bc = new BatchContext { Console = null!, Context = null!, Parameters = ["test.bat", null, null, null, null, null, null, null, null, null] };
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
                Console = null!,
                Context = null!,
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
            var bc = new BatchContext { Console = null!, Context = null! };
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
            var bc = new BatchContext { Console = null!, Context = null! };
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
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
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
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
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
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
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
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
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
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
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
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
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
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
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
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
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
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
            string? line = null;

            // Act
            var result = Expander.ExpandEnvironmentVariables(line!, ctx);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_UnclosedPercent_StripsLonePercent()
        {
            // CMD strips a lone % when there is no closing % — batch mode behavior
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
            ctx.EnvironmentVariables["TEST"] = "value";
            var line = "echo %TEST";

            var result = Expander.ExpandEnvironmentVariables(line, ctx);

            Assert.AreEqual("echo TEST", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_StringSubstitution_ReplacesStr1WithStr2()
        {
            // From SET /? help: "%PATH:str1=str2% would expand the PATH variable,
            // substituting each occurrence of str1 with str2."
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
            ctx.EnvironmentVariables["MYVAR"] = "hello world hello";

            var result = Expander.ExpandEnvironmentVariables("echo %MYVAR:hello=hi%", ctx);

            Assert.AreEqual("echo hi world hi", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_StringSubstitution_EmptyStr2_DeletesOccurrences()
        {
            // From SET /? help: "str2 can be the empty string to effectively delete all occurrences of str1"
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
            ctx.EnvironmentVariables["MYVAR"] = "abcXdefXghi";

            var result = Expander.ExpandEnvironmentVariables("echo %MYVAR:X=%", ctx);

            Assert.AreEqual("echo abcdefghi", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_SubstringExtraction_OffsetAndLength()
        {
            // From SET /? help: "%PATH:~10,5% would use 5 characters starting at offset 10"
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
            ctx.EnvironmentVariables["MYVAR"] = "0123456789ABCDE";

            var result = Expander.ExpandEnvironmentVariables("echo %MYVAR:~10,5%", ctx);

            Assert.AreEqual("echo ABCDE", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_SubstringExtraction_NegativeOffset_FromEnd()
        {
            // From SET /? help: "%PATH:~-10% would extract the last 10 characters"
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
            ctx.EnvironmentVariables["MYVAR"] = "0123456789ABCDE"; // 15 chars

            var result = Expander.ExpandEnvironmentVariables("echo %MYVAR:~-10%", ctx);

            Assert.AreEqual("echo 56789ABCDE", result);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_SubstringExtraction_NegativeLength_ExcludesFromEnd()
        {
            // From SET /? help: "%PATH:~0,-2% would extract all but the last 2 characters"
            var ctx = new DosContext(new DosFileSystem(new Dictionary<char, string> { ['Z'] = @"C:\" }));
            ctx.EnvironmentVariables["MYVAR"] = "Hello";

            var result = Expander.ExpandEnvironmentVariables("echo %MYVAR:~0,-2%", ctx);

            Assert.AreEqual("echo Hel", result);
        }
    }
}
