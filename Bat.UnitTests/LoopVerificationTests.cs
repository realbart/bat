using Bat.UnitTests;

namespace Bat.Tests;

[TestClass]
public class LoopVerificationTests
{
    [TestMethod]
    public async Task Tree_ShouldFailWithTimeout_WhenInfiniteLoopOccurs()
    {
        var harness = new TestHarness();
        
        harness.FileSystem.AddDir('C', ["Loop"]);
        
        // Build a very deep structure to trigger timeout
        string[] currentPath = ["Loop"];
        for (int i = 0; i < 50000; i++)
        {
            var nextName = "Sub";
            harness.FileSystem.AddDir('C', currentPath);
            harness.FileSystem.AddEntry('C', currentPath, nextName, isDir: true);
            currentPath = [.. currentPath, nextName];
        }

        // Execute tree with a very short timeout (1ms)
        try
        {
            await harness.Execute("tree C:\\Loop", timeoutMs: 1);
            Assert.Fail("Should have timed out");
        }
        catch (TimeoutException)
        {
            // Success
        }
    }
}
