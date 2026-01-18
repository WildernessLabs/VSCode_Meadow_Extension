#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Meadow.Debugging.Core.Debugger;

namespace Meadow.Debugging.DAP.Adapters
{
    /// <summary>
    /// Adapter that wraps Mono.Debugging.Soft.SoftDebuggerSession to implement IDebugger.
    /// </summary>
    public class SoftDebuggerAdapter : IDebugger
    {
        private const int MAX_CONNECTION_ATTEMPTS = 20;
        private const int CONNECTION_ATTEMPT_INTERVAL = 500;

        private readonly Mono.Debugging.Soft.SoftDebuggerSession _session;
        private readonly Mono.Debugging.Client.DebuggerSessionOptions _sessionOptions;
        private readonly object _lock = new object();
        private readonly SortedDictionary<long, Mono.Debugging.Client.BreakEvent> _breakpointMap;
        private long _nextBreakpointId = 0;
        private bool _disposed;

        public event EventHandler<DebuggerEventArgs>? SessionEvent;

        public SoftDebuggerAdapter() : this(new Mono.Debugging.Client.DebuggerSessionOptions
        {
            EvaluationOptions = Mono.Debugging.Client.EvaluationOptions.DefaultOptions
        })
        {
        }

        public SoftDebuggerAdapter(Mono.Debugging.Client.DebuggerSessionOptions sessionOptions)
        {
            _sessionOptions = sessionOptions;
            _session = new Mono.Debugging.Soft.SoftDebuggerSession();
            _session.Breakpoints = new Mono.Debugging.Client.BreakpointStore();
            _breakpointMap = new SortedDictionary<long, Mono.Debugging.Client.BreakEvent>();

            // Wire up session events
            _session.TargetStopped += OnTargetStopped;
            _session.TargetHitBreakpoint += OnTargetHitBreakpoint;
            _session.TargetExceptionThrown += OnTargetExceptionThrown;
            _session.TargetUnhandledException += OnTargetUnhandledException;
            _session.TargetStarted += OnTargetStarted;
            _session.TargetReady += OnTargetReady;
            _session.TargetExited += OnTargetExited;
            _session.TargetInterrupted += OnTargetInterrupted;
            _session.TargetThreadStarted += OnTargetThreadStarted;
            _session.TargetThreadStopped += OnTargetThreadStopped;

            // Suppress internal logging
            _session.ExceptionHandler = ex => true;
            _session.LogWriter = (isStdErr, text) => { };
        }

        #region IDebugger Properties

        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _session.IsRunning;
                }
            }
        }

        public bool HasExited
        {
            get
            {
                lock (_lock)
                {
                    return _session.HasExited;
                }
            }
        }

        public IDebugThread? ActiveThread
        {
            get
            {
                lock (_lock)
                {
                    var thread = _session.ActiveThread;
                    return thread != null ? new SoftDebugThread(thread) : null;
                }
            }
        }

        #endregion

        #region IDebugger Connection

        public void Connect(IPAddress address, int port, IEnumerable<string>? assemblyNames = null)
        {
            lock (_lock)
            {
                var assemblyName = assemblyNames?.FirstOrDefault() ?? string.Empty;

                var args = new Mono.Debugging.Soft.SoftDebuggerConnectArgs(assemblyName, address, port)
                {
                    MaxConnectionAttempts = MAX_CONNECTION_ATTEMPTS,
                    TimeBetweenConnectionAttempts = CONNECTION_ATTEMPT_INTERVAL
                };

                _session.Run(new Mono.Debugging.Soft.SoftDebuggerStartInfo(args), _sessionOptions);
            }
        }

        #endregion

        #region IDebugger Execution Control

        public void Continue()
        {
            lock (_lock)
            {
                if (!_session.IsRunning && !_session.HasExited)
                {
                    _session.Continue();
                }
            }
        }

        public void StepOver()
        {
            lock (_lock)
            {
                if (!_session.IsRunning && !_session.HasExited)
                {
                    _session.NextLine();
                }
            }
        }

        public void StepInto()
        {
            lock (_lock)
            {
                if (!_session.IsRunning && !_session.HasExited)
                {
                    _session.StepLine();
                }
            }
        }

        public void StepOut()
        {
            lock (_lock)
            {
                if (!_session.IsRunning && !_session.HasExited)
                {
                    _session.Finish();
                }
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (_session.IsRunning)
                {
                    _session.Stop();
                }
            }
        }

        public void Exit()
        {
            lock (_lock)
            {
                if (!_session.HasExited)
                {
                    _session.Exit();
                }
            }
        }

        #endregion

        #region IDebugger Process & Thread Access

        public IReadOnlyList<IDebugProcess> GetProcesses()
        {
            lock (_lock)
            {
                var processes = _session.GetProcesses();
                return processes?.Select(p => (IDebugProcess)new SoftDebugProcess(p)).ToList()
                    ?? new List<IDebugProcess>();
            }
        }

        #endregion

        #region IDebugger Breakpoint Management

        public IDebugBreakpoint AddLineBreakpoint(string filePath, int lineNumber)
        {
            lock (_lock)
            {
                var bp = _session.Breakpoints.Add(filePath, lineNumber);
                var id = ++_nextBreakpointId;
                _breakpointMap[id] = bp;
                return new SoftDebugBreakpoint(id, bp);
            }
        }

        public IDebugBreakpoint AddCatchpoint(string exceptionTypeName)
        {
            lock (_lock)
            {
                var cp = _session.Breakpoints.AddCatchpoint(exceptionTypeName);
                var id = ++_nextBreakpointId;
                _breakpointMap[id] = cp;
                return new SoftDebugBreakpoint(id, cp);
            }
        }

        public void RemoveBreakpoint(IDebugBreakpoint breakpoint)
        {
            lock (_lock)
            {
                if (_breakpointMap.TryGetValue(breakpoint.Id, out var be))
                {
                    _session.Breakpoints.Remove(be);
                    _breakpointMap.Remove(breakpoint.Id);
                }
            }
        }

        public void ClearAllBreakpoints()
        {
            lock (_lock)
            {
                _session.Breakpoints.Clear();
                _breakpointMap.Clear();
            }
        }

        public IReadOnlyList<IDebugBreakpoint> GetBreakpoints()
        {
            lock (_lock)
            {
                return _breakpointMap
                    .Select(kvp => (IDebugBreakpoint)new SoftDebugBreakpoint(kvp.Key, kvp.Value))
                    .ToList();
            }
        }

        public void SetExceptionBreakMode(string exceptionTypeName, ExceptionBreakMode mode)
        {
            lock (_lock)
            {
                // Find existing catchpoint for this exception type
                var existing = _breakpointMap
                    .Where(kvp => kvp.Value is Mono.Debugging.Client.Catchpoint cp && cp.ExceptionName == exceptionTypeName)
                    .Select(kvp => kvp.Key)
                    .FirstOrDefault();

                if (existing != 0)
                {
                    if (mode == ExceptionBreakMode.Never)
                    {
                        var bp = _breakpointMap[existing];
                        _session.Breakpoints.Remove(bp);
                        _breakpointMap.Remove(existing);
                    }
                }
                else if (mode != ExceptionBreakMode.Never)
                {
                    AddCatchpoint(exceptionTypeName);
                }
            }
        }

        #endregion

        #region Internal Access (for MonoDebugSession compatibility during transition)

        /// <summary>
        /// Gets the underlying SoftDebuggerSession for direct access during transition.
        /// </summary>
        internal Mono.Debugging.Soft.SoftDebuggerSession InternalSession => _session;

        /// <summary>
        /// Gets the breakpoint store for direct access during transition.
        /// </summary>
        internal Mono.Debugging.Client.BreakpointStore InternalBreakpoints => _session.Breakpoints;

        /// <summary>
        /// Gets the session options.
        /// </summary>
        internal Mono.Debugging.Client.DebuggerSessionOptions InternalSessionOptions => _sessionOptions;

        /// <summary>
        /// Sets the output writer for the session.
        /// </summary>
        internal void SetOutputWriter(Mono.Debugging.Client.OutputWriterDelegate writer)
        {
            _session.OutputWriter = writer;
        }

        #endregion

        #region Event Handlers

        private void OnTargetStopped(object? sender, Mono.Debugging.Client.TargetEventArgs e)
        {
            var thread = e.Thread != null ? new SoftDebugThread(e.Thread) : null;
            RaiseEvent(new DebuggerEventArgs(DebuggerEventType.StepCompleted, thread: thread));
        }

        private void OnTargetHitBreakpoint(object? sender, Mono.Debugging.Client.TargetEventArgs e)
        {
            var thread = e.Thread != null ? new SoftDebugThread(e.Thread) : null;
            RaiseEvent(new DebuggerEventArgs(DebuggerEventType.BreakpointHit, thread: thread));
        }

        private void OnTargetExceptionThrown(object? sender, Mono.Debugging.Client.TargetEventArgs e)
        {
            var thread = e.Thread != null ? new SoftDebugThread(e.Thread) : null;
            RaiseEvent(new DebuggerEventArgs(DebuggerEventType.ExceptionThrown, thread: thread));
        }

        private void OnTargetUnhandledException(object? sender, Mono.Debugging.Client.TargetEventArgs e)
        {
            var thread = e.Thread != null ? new SoftDebugThread(e.Thread) : null;
            RaiseEvent(new DebuggerEventArgs(DebuggerEventType.UnhandledException, thread: thread));
        }

        private void OnTargetStarted(object? sender, EventArgs e)
        {
            RaiseEvent(new DebuggerEventArgs(DebuggerEventType.SessionStarted));
        }

        private void OnTargetReady(object? sender, Mono.Debugging.Client.TargetEventArgs e)
        {
            RaiseEvent(new DebuggerEventArgs(DebuggerEventType.SessionReady));
        }

        private void OnTargetExited(object? sender, Mono.Debugging.Client.TargetEventArgs e)
        {
            RaiseEvent(new DebuggerEventArgs(DebuggerEventType.SessionExited));
        }

        private void OnTargetInterrupted(object? sender, Mono.Debugging.Client.TargetEventArgs e)
        {
            RaiseEvent(new DebuggerEventArgs(DebuggerEventType.Paused));
        }

        private void OnTargetThreadStarted(object? sender, Mono.Debugging.Client.TargetEventArgs e)
        {
            var thread = e.Thread != null ? new SoftDebugThread(e.Thread) : null;
            RaiseEvent(new DebuggerEventArgs(DebuggerEventType.ThreadStarted, thread: thread));
        }

        private void OnTargetThreadStopped(object? sender, Mono.Debugging.Client.TargetEventArgs e)
        {
            var thread = e.Thread != null ? new SoftDebugThread(e.Thread) : null;
            RaiseEvent(new DebuggerEventArgs(DebuggerEventType.ThreadExited, thread: thread));
        }

        private void RaiseEvent(DebuggerEventArgs args)
        {
            SessionEvent?.Invoke(this, args);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _session.TargetStopped -= OnTargetStopped;
                    _session.TargetHitBreakpoint -= OnTargetHitBreakpoint;
                    _session.TargetExceptionThrown -= OnTargetExceptionThrown;
                    _session.TargetUnhandledException -= OnTargetUnhandledException;
                    _session.TargetStarted -= OnTargetStarted;
                    _session.TargetReady -= OnTargetReady;
                    _session.TargetExited -= OnTargetExited;
                    _session.TargetInterrupted -= OnTargetInterrupted;
                    _session.TargetThreadStarted -= OnTargetThreadStarted;
                    _session.TargetThreadStopped -= OnTargetThreadStopped;

                    _session.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
