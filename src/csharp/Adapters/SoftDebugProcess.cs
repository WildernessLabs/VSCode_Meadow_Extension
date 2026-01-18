#nullable enable
using System.Collections.Generic;
using System.Linq;
using Meadow.Debugging.Core.Debugger;

namespace VSCodeDebug.Adapters
{
    /// <summary>
    /// Wraps Mono.Debugging.Client.ProcessInfo to implement IDebugProcess.
    /// </summary>
    internal class SoftDebugProcess : IDebugProcess
    {
        private readonly Mono.Debugging.Client.ProcessInfo _process;

        public SoftDebugProcess(Mono.Debugging.Client.ProcessInfo process)
        {
            _process = process;
        }

        public long Id => _process.Id;

        public string Name => _process.Name ?? $"Process {_process.Id}";

        public IReadOnlyList<IDebugThread> GetThreads()
        {
            return _process.GetThreads()
                ?.Select(t => (IDebugThread)new SoftDebugThread(t))
                .ToList() ?? new List<IDebugThread>();
        }

        /// <summary>
        /// Gets the underlying ProcessInfo for direct access during transition.
        /// </summary>
        internal Mono.Debugging.Client.ProcessInfo InternalProcess => _process;
    }
}
