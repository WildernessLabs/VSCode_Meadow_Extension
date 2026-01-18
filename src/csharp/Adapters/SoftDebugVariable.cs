#nullable enable
using System.Collections.Generic;
using System.Linq;
using Meadow.Debugging.Core.Debugger;

namespace VSCodeDebug.Adapters
{
    /// <summary>
    /// Wraps Mono.Debugging.Client.ObjectValue to implement IDebugVariable.
    /// </summary>
    internal class SoftDebugVariable : IDebugVariable
    {
        private readonly Mono.Debugging.Client.ObjectValue _value;

        public SoftDebugVariable(Mono.Debugging.Client.ObjectValue value)
        {
            _value = value;
        }

        public string Name => _value.Name ?? "<unnamed>";

        public string DisplayValue => _value.DisplayValue ?? _value.Value ?? "<null>";

        public string TypeName => _value.TypeName ?? "<unknown>";

        public bool HasChildren => _value.HasChildren;

        public IReadOnlyList<IDebugVariable> GetChildren()
        {
            if (!_value.HasChildren)
                return new List<IDebugVariable>();

            var children = _value.GetAllChildren();
            return children?.Select(c => (IDebugVariable)new SoftDebugVariable(c)).ToList()
                ?? new List<IDebugVariable>();
        }

        /// <summary>
        /// Gets the underlying ObjectValue for direct access during transition.
        /// </summary>
        internal Mono.Debugging.Client.ObjectValue InternalValue => _value;
    }
}
