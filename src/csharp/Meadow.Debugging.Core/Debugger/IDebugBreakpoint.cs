#nullable enable
namespace Meadow.Debugging.Core.Debugger
{
    /// <summary>
    /// Represents a breakpoint in the debugger.
    /// </summary>
    public interface IDebugBreakpoint
    {
        /// <summary>
        /// Unique identifier for this breakpoint.
        /// </summary>
        long Id { get; }

        /// <summary>
        /// The source file path (for line breakpoints).
        /// </summary>
        string? FileName { get; }

        /// <summary>
        /// The line number (for line breakpoints).
        /// </summary>
        int? LineNumber { get; }

        /// <summary>
        /// The exception type name (for catchpoints/exception breakpoints).
        /// </summary>
        string? ExceptionTypeName { get; }

        /// <summary>
        /// Whether the breakpoint has been verified/bound by the debugger.
        /// </summary>
        bool IsVerified { get; }

        /// <summary>
        /// Whether the breakpoint is currently enabled.
        /// </summary>
        bool IsEnabled { get; }
    }
}
