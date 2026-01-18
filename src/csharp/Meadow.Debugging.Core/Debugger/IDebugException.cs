#nullable enable
namespace Meadow.Debugging.Core.Debugger
{
    /// <summary>
    /// Represents an exception in the debugger.
    /// </summary>
    public interface IDebugException
    {
        /// <summary>
        /// The exception message.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// The fully qualified type name of the exception.
        /// </summary>
        string TypeName { get; }

        /// <summary>
        /// The exception instance as a debug variable for inspection.
        /// </summary>
        IDebugVariable? Instance { get; }
    }
}
