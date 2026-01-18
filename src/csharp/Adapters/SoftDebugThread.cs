#nullable enable
using System.Collections.Generic;
using Meadow.Debugging.Core.Debugger;

namespace VSCodeDebug.Adapters
{
    /// <summary>
    /// Wraps Mono.Debugging.Client.ThreadInfo to implement IDebugThread.
    /// </summary>
    internal class SoftDebugThread : IDebugThread
    {
        private readonly Mono.Debugging.Client.ThreadInfo _thread;

        public SoftDebugThread(Mono.Debugging.Client.ThreadInfo thread)
        {
            _thread = thread;
        }

        public long Id => _thread.Id;

        public string Name => _thread.Name ?? $"Thread {_thread.Id}";

        public IReadOnlyList<IDebugStackFrame> GetStackFrames(int maxFrames = 50)
        {
            var backtrace = _thread.Backtrace;
            if (backtrace == null)
                return new List<IDebugStackFrame>();

            var frameCount = System.Math.Min(backtrace.FrameCount, maxFrames);
            var frames = new List<IDebugStackFrame>(frameCount);

            for (int i = 0; i < frameCount; i++)
            {
                var frame = backtrace.GetFrame(i);
                if (frame != null)
                {
                    frames.Add(new SoftDebugStackFrame(frame, i));
                }
            }

            return frames;
        }

        public void SetActive()
        {
            _thread.SetActive();
        }

        /// <summary>
        /// Gets the underlying ThreadInfo for direct access during transition.
        /// </summary>
        internal Mono.Debugging.Client.ThreadInfo InternalThread => _thread;
    }
}
