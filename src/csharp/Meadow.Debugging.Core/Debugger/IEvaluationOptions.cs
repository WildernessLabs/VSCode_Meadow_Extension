#nullable enable
namespace Meadow.Debugging.Core.Debugger
{
    /// <summary>
    /// Options for expression evaluation.
    /// </summary>
    public interface IEvaluationOptions
    {
        /// <summary>
        /// Whether to allow method invocation during evaluation.
        /// </summary>
        bool AllowMethodEvaluation { get; }

        /// <summary>
        /// Whether to allow target invocation (running code on debuggee).
        /// </summary>
        bool AllowTargetInvoke { get; }

        /// <summary>
        /// Timeout for evaluation in milliseconds.
        /// </summary>
        int EvaluationTimeout { get; }

        /// <summary>
        /// Maximum number of items to enumerate for collections.
        /// </summary>
        int MaxEnumerationItems { get; }

        /// <summary>
        /// Maximum string length to return.
        /// </summary>
        int MaxStringLength { get; }
    }
}
