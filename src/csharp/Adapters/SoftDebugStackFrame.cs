#nullable enable
using System.Collections.Generic;
using System.Linq;
using Meadow.Debugging.Core.Debugger;

namespace VSCodeDebug.Adapters
{
    /// <summary>
    /// Wraps Mono.Debugging.Client.StackFrame to implement IDebugStackFrame.
    /// </summary>
    internal class SoftDebugStackFrame : IDebugStackFrame
    {
        private readonly Mono.Debugging.Client.StackFrame _frame;

        public SoftDebugStackFrame(Mono.Debugging.Client.StackFrame frame, int index)
        {
            _frame = frame;
            Index = index;
        }

        public int Index { get; }

        public string MethodName => _frame.SourceLocation?.MethodName ?? "<unknown>";

        public string? FileName => _frame.SourceLocation?.FileName;

        public int LineNumber => _frame.SourceLocation?.Line ?? 0;

        public int ColumnNumber => _frame.SourceLocation?.Column ?? 0;

        public string? FullTypeName => _frame.FullTypeName;

        public IDebugVariable? GetThisReference()
        {
            var thisRef = _frame.GetThisReference();
            return thisRef != null ? new SoftDebugVariable(thisRef) : null;
        }

        public IReadOnlyList<IDebugVariable> GetParameters()
        {
            var parameters = _frame.GetParameters();
            return parameters?.Select(p => (IDebugVariable)new SoftDebugVariable(p)).ToList()
                ?? new List<IDebugVariable>();
        }

        public IReadOnlyList<IDebugVariable> GetLocalVariables()
        {
            var locals = _frame.GetLocalVariables();
            return locals?.Select(l => (IDebugVariable)new SoftDebugVariable(l)).ToList()
                ?? new List<IDebugVariable>();
        }

        public IDebugException? GetException()
        {
            var ex = _frame.GetException();
            return ex != null ? new SoftDebugException(ex) : null;
        }

        public bool ValidateExpression(string expression)
        {
            return _frame.ValidateExpression(expression);
        }

        public IDebugVariable? EvaluateExpression(string expression, IEvaluationOptions? options = null)
        {
            var evalOptions = options != null
                ? ToMonoEvaluationOptions(options)
                : Mono.Debugging.Client.EvaluationOptions.DefaultOptions;

            var result = _frame.GetExpressionValue(expression, evalOptions);
            return result != null ? new SoftDebugVariable(result) : null;
        }

        private static Mono.Debugging.Client.EvaluationOptions ToMonoEvaluationOptions(IEvaluationOptions options)
        {
            return new Mono.Debugging.Client.EvaluationOptions
            {
                AllowMethodEvaluation = options.AllowMethodEvaluation,
                AllowTargetInvoke = options.AllowTargetInvoke,
                EvaluationTimeout = options.EvaluationTimeout,
                GroupPrivateMembers = true,
                GroupStaticMembers = true,
            };
        }

        /// <summary>
        /// Gets the underlying StackFrame for direct access during transition.
        /// </summary>
        internal Mono.Debugging.Client.StackFrame InternalFrame => _frame;
    }
}
