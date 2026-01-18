#nullable enable
using System.Collections.Generic;

namespace Meadow.Debugging.Core.Debugger
{
    /// <summary>
    /// Represents a stack frame in the debugger.
    /// </summary>
    public interface IDebugStackFrame
    {
        /// <summary>
        /// The index of this frame in the call stack (0 = top).
        /// </summary>
        int Index { get; }

        /// <summary>
        /// The method/function name.
        /// </summary>
        string MethodName { get; }

        /// <summary>
        /// The source file name (may be null for external code).
        /// </summary>
        string? FileName { get; }

        /// <summary>
        /// The line number in the source file.
        /// </summary>
        int LineNumber { get; }

        /// <summary>
        /// The column number in the source file.
        /// </summary>
        int ColumnNumber { get; }

        /// <summary>
        /// The fully qualified name of the containing type.
        /// </summary>
        string? FullTypeName { get; }

        /// <summary>
        /// Gets the 'this' reference for instance methods.
        /// </summary>
        IDebugVariable? GetThisReference();

        /// <summary>
        /// Gets the method parameters.
        /// </summary>
        IReadOnlyList<IDebugVariable> GetParameters();

        /// <summary>
        /// Gets the local variables.
        /// </summary>
        IReadOnlyList<IDebugVariable> GetLocalVariables();

        /// <summary>
        /// Gets the active exception in this frame (if any).
        /// </summary>
        IDebugException? GetException();

        /// <summary>
        /// Validates an expression in this frame's context.
        /// </summary>
        bool ValidateExpression(string expression);

        /// <summary>
        /// Evaluates an expression in this frame's context.
        /// </summary>
        IDebugVariable? EvaluateExpression(string expression, IEvaluationOptions? options = null);
    }
}
