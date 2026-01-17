#nullable enable
using System;
using Meadow.Debugging.Core.Events;

namespace VSCodeDebug
{
    /// <summary>
    /// VSCode-specific implementation of IDebugEventEmitter.
    /// Translates platform-agnostic events to VSCode DAP events.
    /// </summary>
    public class VSCodeEventEmitter : IDebugEventEmitter
    {
        private readonly ProtocolServer _protocolServer;

        public VSCodeEventEmitter(ProtocolServer protocolServer)
        {
            _protocolServer = protocolServer ?? throw new ArgumentNullException(nameof(protocolServer));
        }

        public void EmitOutput(OutputCategory category, string message)
        {
            Event evt = category switch
            {
                OutputCategory.Meadow => new MeadowOutputEvent(message),
                OutputCategory.Stdout => new MeadowOutputEvent(message),
                OutputCategory.Stderr => new MeadowOutputEvent(message),
                OutputCategory.Console => new ConsoleOutputEvent(message),
                OutputCategory.Telemetry => new ConsoleOutputEvent(message),
                _ => new ConsoleOutputEvent(message)
            };
            _protocolServer.SendEvent(evt);
        }

        public void EmitDeploymentProgress(string fileName, uint percentage)
        {
            _protocolServer.SendEvent(new UpdateProgressBarEvent(fileName, percentage));
        }

        public void EmitDeviceMessage(string source, string message)
        {
            // Route based on source type - device messages typically go to Meadow output
            if (source.StartsWith("stdout", StringComparison.OrdinalIgnoreCase) ||
                source.StartsWith("info", StringComparison.OrdinalIgnoreCase))
            {
                EmitOutput(OutputCategory.Meadow, message);
            }
            else
            {
                EmitOutput(OutputCategory.Console, message);
            }
        }

        public void EmitStopped(int threadId, StopReason reason, string? text = null)
        {
            var reasonStr = reason switch
            {
                StopReason.Step => "step",
                StopReason.Breakpoint => "breakpoint",
                StopReason.Exception => "exception",
                StopReason.Pause => "pause",
                StopReason.Entry => "entry",
                StopReason.Goto => "goto",
                StopReason.FunctionBreakpoint => "function breakpoint",
                StopReason.DataBreakpoint => "data breakpoint",
                _ => "unknown"
            };
            _protocolServer.SendEvent(new StoppedEvent(threadId, reasonStr, text));
        }

        public void EmitThread(int threadId, ThreadEventReason reason)
        {
            var reasonStr = reason == ThreadEventReason.Started ? "started" : "exited";
            _protocolServer.SendEvent(new ThreadEvent(reasonStr, threadId));
        }

        public void EmitInitialized()
        {
            _protocolServer.SendEvent(new InitializedEvent());
        }

        public void EmitTerminated()
        {
            _protocolServer.SendEvent(new TerminatedEvent());
        }

        public void EmitExited(int exitCode)
        {
            _protocolServer.SendEvent(new ExitedEvent(exitCode));
        }
    }
}
