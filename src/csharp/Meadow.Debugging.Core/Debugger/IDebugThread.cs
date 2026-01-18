#nullable enable
using System.Collections.Generic;

namespace Meadow.Debugging.Core.Debugger
{
    /// <summary>
    /// Represents a thread in the debugger.
    /// </summary>
    public interface IDebugThread
    {
        /// <summary>
        /// The thread ID.
        /// </summary>
        long Id { get; }

        /// <summary>
        /// The thread name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the call stack for this thread.
        /// </summary>
        /// <param name="maxFrames">Maximum number of frames to retrieve.</param>
        IReadOnlyList<IDebugStackFrame> GetStackFrames(int maxFrames = 50);

        /// <summary>
        /// Sets this thread as the active thread for debugging operations.
        /// </summary>
        void SetActive();
    }
}
