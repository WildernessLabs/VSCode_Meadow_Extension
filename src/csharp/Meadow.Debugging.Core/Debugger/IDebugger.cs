#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Meadow.Debugging.Core.Debugger
{
    /// <summary>
    /// Abstraction for debugger operations.
    /// Allows support for different debugging backends (Mono.Debugging, Visual Studio, Rider, etc.)
    /// </summary>
    public interface IDebugger : IDisposable
    {
        // =====================================================
        // Events
        // =====================================================

        /// <summary>
        /// Raised when a debugger session event occurs.
        /// </summary>
        event EventHandler<DebuggerEventArgs>? SessionEvent;

        // =====================================================
        // State Properties
        // =====================================================

        /// <summary>
        /// Gets whether the debuggee is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets whether the debuggee has exited.
        /// </summary>
        bool HasExited { get; }

        /// <summary>
        /// Gets the currently active thread.
        /// </summary>
        IDebugThread? ActiveThread { get; }

        // =====================================================
        // Connection & Initialization
        // =====================================================

        /// <summary>
        /// Connect to a running debuggee and start debugging.
        /// </summary>
        /// <param name="address">IP address to connect to.</param>
        /// <param name="port">Port number.</param>
        /// <param name="assemblyNames">Assembly names to load symbols for.</param>
        void Connect(IPAddress address, int port, IEnumerable<string>? assemblyNames = null);

        // =====================================================
        // Execution Control
        // =====================================================

        /// <summary>
        /// Resume execution of the debuggee.
        /// </summary>
        void Continue();

        /// <summary>
        /// Step over the next statement.
        /// </summary>
        void StepOver();

        /// <summary>
        /// Step into the next statement (into function calls).
        /// </summary>
        void StepInto();

        /// <summary>
        /// Step out of the current function.
        /// </summary>
        void StepOut();

        /// <summary>
        /// Pause/interrupt execution.
        /// </summary>
        void Pause();

        /// <summary>
        /// Exit the debugging session gracefully.
        /// </summary>
        void Exit();

        // =====================================================
        // Process & Thread Access
        // =====================================================

        /// <summary>
        /// Get all active processes.
        /// </summary>
        IReadOnlyList<IDebugProcess> GetProcesses();

        // =====================================================
        // Breakpoint Management
        // =====================================================

        /// <summary>
        /// Add a line breakpoint.
        /// </summary>
        /// <param name="filePath">Source file path.</param>
        /// <param name="lineNumber">Line number (1-based).</param>
        /// <returns>The created breakpoint.</returns>
        IDebugBreakpoint AddLineBreakpoint(string filePath, int lineNumber);

        /// <summary>
        /// Add an exception catchpoint.
        /// </summary>
        /// <param name="exceptionTypeName">Full name of exception type.</param>
        /// <returns>The created catchpoint.</returns>
        IDebugBreakpoint AddCatchpoint(string exceptionTypeName);

        /// <summary>
        /// Remove a breakpoint.
        /// </summary>
        void RemoveBreakpoint(IDebugBreakpoint breakpoint);

        /// <summary>
        /// Clear all breakpoints.
        /// </summary>
        void ClearAllBreakpoints();

        /// <summary>
        /// Get all active breakpoints.
        /// </summary>
        IReadOnlyList<IDebugBreakpoint> GetBreakpoints();

        // =====================================================
        // Exception Handling
        // =====================================================

        /// <summary>
        /// Set exception break mode for a specific exception type.
        /// </summary>
        void SetExceptionBreakMode(string exceptionTypeName, ExceptionBreakMode mode);
    }
}
