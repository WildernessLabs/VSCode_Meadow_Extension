#nullable enable
namespace Meadow.Debugging.Core.Debugger
{
    /// <summary>
    /// Types of debugger session events.
    /// </summary>
    public enum DebuggerEventType
    {
        /// <summary>Debugging session has started.</summary>
        SessionStarted,

        /// <summary>Target is ready to accept commands.</summary>
        SessionReady,

        /// <summary>Target process has exited.</summary>
        SessionExited,

        /// <summary>Execution stopped (step completed).</summary>
        StepCompleted,

        /// <summary>Execution stopped at a breakpoint.</summary>
        BreakpointHit,

        /// <summary>Execution stopped due to a caught exception.</summary>
        ExceptionThrown,

        /// <summary>Execution stopped due to an unhandled exception.</summary>
        UnhandledException,

        /// <summary>Execution was paused/interrupted.</summary>
        Paused,

        /// <summary>A new thread was created.</summary>
        ThreadStarted,

        /// <summary>A thread has exited.</summary>
        ThreadExited
    }

    /// <summary>
    /// Exception break modes for controlling when to break on exceptions.
    /// </summary>
    public enum ExceptionBreakMode
    {
        /// <summary>Never break on this exception type.</summary>
        Never,

        /// <summary>Always break when this exception is thrown.</summary>
        Always,

        /// <summary>Only break on unhandled exceptions of this type.</summary>
        Unhandled
    }
}
