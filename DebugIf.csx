using Bat.Execution;
using Bat.UnitTests;
using Context;

var fs = new TestFileSystem();
fs.AddDir('C', ["test"]);
fs.AddBatchFile('C', ["test"], "if.bat", """
@echo off
if "hello"=="hello" echo THEN1
if "hello"=="world" echo THEN2 else echo ELSE2
if defined NOSUCHVAR echo THEN3 else echo ELSE3
""");

var console = new TestConsole();
var ctx = new TestCommandContext(fs) { Console = console };
ctx.SetCurrentDrive('C');
ctx.SetPath('C', ["test"]);

var bc = new BatchContext { Context = ctx };
var executor = new BatchExecutor();
await executor.ExecuteAsync("C:\\test\\if.bat", "", bc, []);

System.Console.WriteLine("=== OUTPUT ===");
System.Console.WriteLine(console.OutText);
System.Console.WriteLine("=== ERRORS ===");
System.Console.WriteLine(console.ErrText);
