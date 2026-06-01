using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BatD.Pty;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests.Linux;

[TestClass]
public class PosixPtyTests
{
    [TestMethod]
    public async Task PosixPty_CanStartAndClose()
    {
        if (Environment.OSVersion.Platform != PlatformID.Unix)
            return;

        var pty = new PosixPty();
        try
        {
            // Just test if we can start /bin/true
            pty.Start("/bin/true", "", "/", null, 80, 24);
            var exitCode = await pty.WaitForExitAsync();
            Assert.AreEqual(0, exitCode);
        }
        finally
        {
            pty.Dispose();
        }
    }
}
