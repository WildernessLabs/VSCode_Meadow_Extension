#nullable enable
using System;

namespace Meadow.Debugging.Core.Debugger
{
    /// <summary>
    /// Event arguments for debugger session events.
    /// </summary>
    public class DebuggerEventArgs : EventArgs
    {
        /// <summary>
        /// The type of event.
        /// </summary>
        public DebuggerEventType Type { get; }

        /// <summary>
        /// The thread associated with this event (if applicable).
        /// </summary>
        public IDebugThread? Thread { get; }

        /// <summary>
        /// The breakpoint associated with this event (if applicable).
        /// </summary>
        public IDebugBreakpoint? Breakpoint { get; }

        /// <summary>
        /// The exception associated with this event (if applicable).
        /// </summary>
        public IDebugException? Exception { get; }

        /// <summary>
        /// Additional message or description.
        /// </summary>
        public string? Message { get; }

        /// <summary>
        /// Exit code (for SessionExited events).
        /// </summary>
        public int? ExitCode { get; }

        public DebuggerEventArgs(DebuggerEventType type)
        {
            Type = type;
        }

        public DebuggerEventArgs(
            DebuggerEventType type,
            IDebugThread? thread = null,
            IDebugBreakpoint? breakpoint = null,
            IDebugException? exception = null,
            string? message = null,
            int? exitCode = null)
        {
            Type = type;
            Thread = thread;
            Breakpoint = breakpoint;
            Exception = exception;
            Message = message;
            ExitCode = exitCode;
        }
    }
}
