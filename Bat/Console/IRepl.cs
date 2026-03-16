using Context;

namespace Bat.Console
{
    internal interface IRepl
    {
        Task StartAsync(IContext context);
    }
}