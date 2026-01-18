#nullable enable
using System.Collections.Generic;

namespace Meadow.Debugging.Core.Debugger
{
    /// <summary>
    /// Represents a process being debugged.
    /// </summary>
    public interface IDebugProcess
    {
        /// <summary>
        /// The process ID.
        /// </summary>
        long Id { get; }

        /// <summary>
        /// The process name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets all threads in this process.
        /// </summary>
        IReadOnlyList<IDebugThread> GetThreads();
    }
}
