using Bat.Nodes;

namespace Bat.Execution;

/// <summary>
/// Interface for executable execution strategies.
/// Implementations: BatchExecutor, NativeExecutor, DotNetLibraryExecutor
/// </summary>
internal interface IExecutor
{
    /// <summary>
    /// Executes an external command.
    /// </summary>
    /// <param name="executablePath">Full native path to executable</param>
    /// <param name="arguments">Command line arguments</param>
    /// <param name="batchContext">Current batch context</param>
    /// <param name="redirections">I/O redirections</param>
    /// <returns>Exit code</returns>
    Task<int> ExecuteAsync(string executablePath, string arguments, BatchContext batchContext, IReadOnlyList<Redirection> redirections);
}
