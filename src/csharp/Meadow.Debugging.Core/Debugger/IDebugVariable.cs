#nullable enable
using System.Collections.Generic;

namespace Meadow.Debugging.Core.Debugger
{
    /// <summary>
    /// Represents a variable or value in the debugger.
    /// </summary>
    public interface IDebugVariable
    {
        /// <summary>
        /// The name of the variable.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The display value as a string.
        /// </summary>
        string DisplayValue { get; }

        /// <summary>
        /// The type name of the variable.
        /// </summary>
        string TypeName { get; }

        /// <summary>
        /// Whether this variable has child members.
        /// </summary>
        bool HasChildren { get; }

        /// <summary>
        /// Gets the child variables (properties, fields, array elements).
        /// </summary>
        IReadOnlyList<IDebugVariable> GetChildren();
    }
}
