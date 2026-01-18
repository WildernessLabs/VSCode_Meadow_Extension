#nullable enable
using Meadow.Debugging.Core.Debugger;

namespace Meadow.Debugging.DAP.Adapters
{
    /// <summary>
    /// Wraps Mono.Debugging.Client.BreakEvent to implement IDebugBreakpoint.
    /// </summary>
    internal class SoftDebugBreakpoint : IDebugBreakpoint
    {
        private readonly Mono.Debugging.Client.BreakEvent _breakEvent;

        public SoftDebugBreakpoint(long id, Mono.Debugging.Client.BreakEvent breakEvent)
        {
            Id = id;
            _breakEvent = breakEvent;
        }

        public long Id { get; }

        public string? FileName
        {
            get
            {
                if (_breakEvent is Mono.Debugging.Client.Breakpoint bp)
                    return bp.FileName;
                return null;
            }
        }

        public int? LineNumber
        {
            get
            {
                if (_breakEvent is Mono.Debugging.Client.Breakpoint bp)
                    return bp.Line;
                return null;
            }
        }

        public string? ExceptionTypeName
        {
            get
            {
                if (_breakEvent is Mono.Debugging.Client.Catchpoint cp)
                    return cp.ExceptionName;
                return null;
            }
        }

        public bool IsVerified => true; // Mono breakpoints are always considered verified once added

        public bool IsEnabled => _breakEvent.Enabled;

        /// <summary>
        /// Gets the underlying BreakEvent for direct access during transition.
        /// </summary>
        internal Mono.Debugging.Client.BreakEvent InternalBreakEvent => _breakEvent;
    }
}
