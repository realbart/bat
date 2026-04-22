using Context;

namespace Bat.Console;

internal interface IRepl
{
    Task StartAsync(IContext context);
    Task ExecuteCommandAsync(IContext context, string command);
    Task ExecuteBatchAsync(IContext context, string batchFilePath);
}