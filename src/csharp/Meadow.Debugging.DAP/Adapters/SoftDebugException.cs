#nullable enable
using Meadow.Debugging.Core.Debugger;

namespace Meadow.Debugging.DAP.Adapters
{
    /// <summary>
    /// Wraps Mono.Debugging.Client.ExceptionInfo to implement IDebugException.
    /// </summary>
    internal class SoftDebugException : IDebugException
    {
        private readonly Mono.Debugging.Client.ExceptionInfo _exception;

        public SoftDebugException(Mono.Debugging.Client.ExceptionInfo exception)
        {
            _exception = exception;
        }

        public string Message => _exception.Message ?? string.Empty;

        public string TypeName => _exception.Type ?? "<unknown>";

        public IDebugVariable? Instance
        {
            get
            {
                var instance = _exception.Instance;
                return instance != null ? new SoftDebugVariable(instance) : null;
            }
        }

        /// <summary>
        /// Gets the underlying ExceptionInfo for direct access during transition.
        /// </summary>
        internal Mono.Debugging.Client.ExceptionInfo InternalException => _exception;
    }
}
