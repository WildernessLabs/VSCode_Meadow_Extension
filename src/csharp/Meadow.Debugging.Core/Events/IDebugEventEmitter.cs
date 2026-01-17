using System;

namespace Meadow.Debugging.Core.Events
{
    /// <summary>
    /// Abstraction for emitting debug events to the IDE.
    /// Implemented by each platform (VSCode, Visual Studio, Rider).
    /// </summary>
    public interface IDebugEventEmitter
    {
        /// <summary>
        /// Emit a debug-related output message.
        /// </summary>
        /// <param name="category">Output category (e.g., Console, Meadow)</param>
        /// <param name="message">The message content</param>
        void EmitOutput(OutputCategory category, string message);

        /// <summary>
        /// Report deployment progress to the IDE.
        /// </summary>
        /// <param name="fileName">File being deployed</param>
        /// <param name="percentage">Progress percentage (0-100)</param>
        void EmitDeploymentProgress(string fileName, uint percentage);

        /// <summary>
        /// Report a device message received from Meadow.
        /// </summary>
        /// <param name="source">Message source (e.g., "stdout", "info")</param>
        /// <param name="message">The message content</param>
        void EmitDeviceMessage(string source, string message);

        /// <summary>
        /// Emit a stopped event (breakpoint hit, step complete, exception).
        /// </summary>
        void EmitStopped(int threadId, StopReason reason, string? text = null);

        /// <summary>
        /// Emit a thread event (started/exited).
        /// </summary>
        void EmitThread(int threadId, ThreadEventReason reason);

        /// <summary>
        /// Emit session initialization complete.
        /// </summary>
        void EmitInitialized();

        /// <summary>
        /// Emit session terminated.
        /// </summary>
        void EmitTerminated();

        /// <summary>
        /// Emit exited event with exit code.
        /// </summary>
        void EmitExited(int exitCode);
    }

    /// <summary>
    /// Output message categories
    /// </summary>
    public enum OutputCategory
    {
        /// <summary>IDE console/output window</summary>
        Console,
        /// <summary>Standard output from process</summary>
        Stdout,
        /// <summary>Standard error from process</summary>
        Stderr,
        /// <summary>Meadow device output</summary>
        Meadow,
        /// <summary>Telemetry/diagnostic data</summary>
        Telemetry
    }

    /// <summary>
    /// Reasons for stopping execution
    /// </summary>
    public enum StopReason
    {
        Step,
        Breakpoint,
        Exception,
        Pause,
        Entry,
        Goto,
        FunctionBreakpoint,
        DataBreakpoint
    }

    /// <summary>
    /// Thread event reasons
    /// </summary>
    public enum ThreadEventReason
    {
        Started,
        Exited
    }
}
