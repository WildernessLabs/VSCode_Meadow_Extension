/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Wilderness Labs. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using Meadow.Debugging.Core.Events;
using Meadow.Debugging.DAP.Deployment;
using Meadow.Debugging.DAP.Protocol;
using Meadow.Debugging.DAP.Utilities;
using Mono.Debugging.Client;
using Meadow.Hcom;

namespace Meadow.Debugging.DAP.Session
{
    /// <summary>
    /// The main Meadow debug session implementation.
    /// Handles DAP protocol commands for debugging Meadow applications.
    /// IDE-agnostic - works with VSCode, VS2022, Rider, or any DAP-compliant client.
    /// </summary>
    public class MeadowDebugSession : DebugSession
    {
        private const int MAX_CHILDREN = 100;
        private const int MAX_CONNECTION_ATTEMPTS = 20;
        private const int CONNECTION_ATTEMPT_INTERVAL = 500;

        private readonly string[] MONO_EXTENSIONS = new string[] {
            ".cs", ".csx",
            ".cake",
            ".fs", ".fsi", ".ml", ".mli", ".fsx", ".fsscript",
            ".hx"
        };

        private AutoResetEvent _resumeEvent = new AutoResetEvent(false);
        private bool _debuggeeExecuting = false;
        private readonly object _lock = new object();
        private Mono.Debugging.Soft.SoftDebuggerSession? _session;
        private volatile bool _debuggeeKilled = true;
        private ProcessInfo? _activeProcess;
        private Mono.Debugging.Client.StackFrame? _activeFrame;
        private long _nextBreakpointId = 0;
        private SortedDictionary<long, BreakEvent> _breakpoints;
        private List<Catchpoint> _catchpoints;
        private DebuggerSessionOptions _debuggerSessionOptions;

        private System.Diagnostics.Process? _process;
        private Handles<ObjectValue[]> _variableHandles;
        private Handles<Mono.Debugging.Client.StackFrame> _frameHandles;
        private ObjectValue? _exception;
        private Dictionary<int, Protocol.Thread> _seenThreads = new Dictionary<int, Protocol.Thread>();
        private bool _attachMode = false;
        private bool _terminated = false;
        private bool _stderrEOF = true;
        private bool _stdoutEOF = true;

        private CancellationTokenSource? _ctsDeployMeadow;
        private MeadowDeployer? _meadowDeployer;
        private DebuggingServer? _meadowDebuggingServer;
        private string _previousLogMessage = string.Empty;

        private IDebugEventEmitter? _eventEmitter;
        private readonly LaunchPropertyKeys _launchPropertyKeys;

        /// <summary>
        /// Creates a new MeadowDebugSession.
        /// </summary>
        /// <param name="launchPropertyKeys">The property keys for parsing launch configuration</param>
        public MeadowDebugSession(LaunchPropertyKeys? launchPropertyKeys = null) : base()
        {
            _launchPropertyKeys = launchPropertyKeys ?? LaunchPropertyKeys.VSCode;

            // Create the event emitter - it needs 'this' as the ProtocolServer,
            // so we must create it after calling base constructor
            _eventEmitter = new Events.DapEventEmitter(this);

            _variableHandles = new Handles<ObjectValue[]>();
            _frameHandles = new Handles<Mono.Debugging.Client.StackFrame>();
            _seenThreads = new Dictionary<int, Protocol.Thread>();

            _debuggerSessionOptions = new DebuggerSessionOptions
            {
                EvaluationOptions = EvaluationOptions.DefaultOptions
            };

            _session = new Mono.Debugging.Soft.SoftDebuggerSession();
            _session.Breakpoints = new BreakpointStore();

            _breakpoints = new SortedDictionary<long, BreakEvent>();
            _catchpoints = new List<Catchpoint>();

            DebuggerLoggingService.CustomLogger = new CustomLogger();

            _session.ExceptionHandler = ex =>
            {
                return true;
            };

            _session.LogWriter = (isStdErr, text) =>
            {
            };

            _session.TargetStopped += (sender, e) =>
            {
                Stopped();
                SendEvent(CreateStoppedEvent("step", e.Thread));
                _resumeEvent.Set();
            };

            _session.TargetHitBreakpoint += (sender, e) =>
            {
                Stopped();
                SendEvent(CreateStoppedEvent("breakpoint", e.Thread));
                _resumeEvent.Set();
            };

            _session.TargetExceptionThrown += (sender, e) =>
            {
                Stopped();
                var ex = DebuggerActiveException();
                if (ex != null)
                {
                    _exception = ex.Instance;
                    SendEvent(CreateStoppedEvent("exception", e.Thread, ex.Message));
                }
                _resumeEvent.Set();
            };

            _session.TargetUnhandledException += (sender, e) =>
            {
                Stopped();
                var ex = DebuggerActiveException();
                if (ex != null)
                {
                    _exception = ex.Instance;
                    SendEvent(CreateStoppedEvent("exception", e.Thread, ex.Message));
                }
                _resumeEvent.Set();
            };

            _session.TargetStarted += (sender, e) =>
            {
                _activeFrame = null;
            };

            _session.TargetReady += (sender, e) =>
            {
                _activeProcess = _session?.GetProcesses().SingleOrDefault();
            };

            _session.TargetExited += (sender, e) =>
            {
                DebuggerKill();

                _debuggeeKilled = true;

                Terminate("target exited");

                _resumeEvent.Set();
            };

            _session.TargetInterrupted += (sender, e) =>
            {
                _resumeEvent.Set();
            };

            _session.TargetEvent += (sender, e) =>
            {
            };

            _session.TargetThreadStarted += (sender, e) =>
            {
                int tid = (int)e.Thread.Id;
                lock (_seenThreads)
                {
                    _seenThreads[tid] = new Protocol.Thread(tid, e.Thread.Name);
                }
                SendEvent(new ThreadEvent("started", tid));
            };

            _session.TargetThreadStopped += (sender, e) =>
            {
                int tid = (int)e.Thread.Id;
                lock (_seenThreads)
                {
                    _seenThreads.Remove(tid);
                }
                SendEvent(new ThreadEvent("exited", tid));
            };

            _session.OutputWriter = (isStdErr, text) =>
            {
                SendOutput(isStdErr ? "stderr" : "stdout", text);
            };
        }

        public override void Initialize(Response response, dynamic args)
        {
            OperatingSystem os = Environment.OSVersion;
            if (os.Platform != PlatformID.MacOSX && os.Platform != PlatformID.Unix && os.Platform != PlatformID.Win32NT)
            {
                SendErrorResponse(response, 3000, "Mono Debug is not supported on this platform ({_platform}).", new { _platform = os.Platform.ToString() }, true, true);
                return;
            }

            SendResponse(response, new Capabilities()
            {
                supportsConfigurationDoneRequest = false,
                supportsFunctionBreakpoints = false,
                supportsConditionalBreakpoints = false,
                supportsEvaluateForHovers = false,
                supportsProgressReporting = true,
                exceptionBreakpointFilters = new dynamic[0]
            });

            // Mono Debug is ready to accept breakpoints immediately
            SendEvent(new InitializedEvent());
        }

        public override async Task Launch(Response response, dynamic args)
        {
            _attachMode = false;

            var errorMsg = string.Empty;

            try
            {
                // Safely access __exceptionOptions - it may not exist on the dynamic args
                dynamic? exceptionOptions = null;
                try
                {
                    exceptionOptions = args.__exceptionOptions;
                }
                catch (Exception)
                {
                    // Property doesn't exist, which is fine - SetExceptionBreakpoints handles null
                }
                SetExceptionBreakpoints(exceptionOptions);

                var launchOptions = new LaunchData(args, _launchPropertyKeys);

                var valid = launchOptions.Validate();
                if (!valid.success)
                {
                    SendErrorResponse(response, 3002, valid.message);
                    return;
                }

                var host = GetString(args, "address");
                IPAddress address = string.IsNullOrWhiteSpace(host) ? IPAddress.Loopback : DapUtilities.ResolveIPAddress(host)!;
                if (address == null)
                {
                    SendErrorResponse(response, 3013, "Invalid address '{address}'.", new { address = address });
                    return;
                }

                if (_ctsDeployMeadow != null && !_ctsDeployMeadow.IsCancellationRequested)
                    _ctsDeployMeadow.Cancel();

                _ctsDeployMeadow = new CancellationTokenSource();

                string? outputPath = launchOptions.GetBuildProperty("OutputPath");
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    throw new InvalidOperationException($"MSBuild property 'OutputPath' not found or empty. Check that MSBuildPropertyFile exists at: {launchOptions.MSBuildPropertyFile}");
                }

                var fullOutputPath = DapUtilities.FixPathSeparators(outputPath);

                if (string.IsNullOrWhiteSpace(fullOutputPath))
                {
                    throw new InvalidOperationException("FixPathSeparators returned empty output path");
                }

                if (!Directory.Exists(fullOutputPath))
                {
                    throw new DirectoryNotFoundException($"Output path does not exist: {fullOutputPath}");
                }

                var logger = new DebugSessionLogger(l => Log(l));
                var deploymentCallbacks = new DeploymentCallbackAdapter(_eventEmitter);

                var isDebugging = launchOptions.DebugPort > 1024;

                _meadowDeployer = new MeadowDeployer(deploymentCallbacks, logger, launchOptions.Serial!, _ctsDeployMeadow.Token);

                IMeadowConnection? meadowConnection;
                try
                {
                    if (launchOptions.SkipDeploy)
                    {
                        meadowConnection = await _meadowDeployer.ConnectForDebuggingAsync();
                    }
                    else
                    {
                        meadowConnection = await _meadowDeployer.DeployAsync(fullOutputPath!, isDebugging);
                    }

                    if (meadowConnection == null)
                    {
                        throw new InvalidOperationException("Failed to establish Meadow connection");
                    }
                }
                catch (Exception deployEx)
                {
                    throw new InvalidOperationException($"Deployment/connection failed: {deployEx.Message}", deployEx);
                }

                if (isDebugging)
                {
                    _attachMode = true;

                    // Start the debugging session in a background task.
                    // StartDebuggingSession internally:
                    //   1. Starts a TCP listener on the debug port
                    //   2. Waits for a client (us) to connect
                    //   3. Then tells the device to start debugging
                    // We must connect AFTER the listener starts but BEFORE it times out waiting.
                    var meadowDebuggingServerTask = Task.Run(async () =>
                    {
                        try
                        {
                            await meadowConnection!.StartDebuggingSession(launchOptions.DebugPort, logger, _ctsDeployMeadow.Token, "DAP");
                        }
                        catch (Exception ex)
                        {
                            Log($"[Launch] Debug server task error: {ex.GetType().Name}: {ex.Message}");
                            throw;
                        }
                    }, _ctsDeployMeadow.Token);

                    // Give the TCP listener time to start before we try to connect
                    await Task.Delay(250, _ctsDeployMeadow.Token);

                    try
                    {
                        Connect(address, launchOptions.DebugPort);
                    }
                    catch (Exception connectEx)
                    {
                        throw new InvalidOperationException($"Failed to connect to debugger: {connectEx.Message}", connectEx);
                    }

                    // Wait for the debugging session to be fully established
                    try
                    {
                        await meadowDebuggingServerTask;
                    }
                    catch (Exception sessionEx)
                    {
                        throw new InvalidOperationException($"Debug session start failed: {sessionEx.Message}", sessionEx);
                    }
                }
                else
                {
                }

                SendResponse(response);
                return;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                Log($"[Launch] CRITICAL ERROR: {ex.GetType().Name}: {errorMsg}");
                if (ex.InnerException != null)
                {
                    Log($"[Launch] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
            }

            SendErrorResponse(response, 3002, $"Launch failed: {errorMsg}");

            await Disconnect(response, null);

            Terminate("Launch failed.");
        }

        private void Log(string message)
        {
            if (_previousLogMessage != message)
            {
                Console.WriteLine(message);

                if (message.StartsWith("stdout") || message.StartsWith("info"))
                {
                    // This appears in blue as it is from "Meadow"
                    var spliter = message.Split(':');
                    if (spliter.Length > 1)
                    {
                        string output = string.Empty;

                        for (int i = 1; i < spliter.Length; i++)
                        {
                            output += spliter[i];
                        }
                        _eventEmitter.EmitOutput(OutputCategory.Meadow, output + Environment.NewLine);
                    }
                }
                else
                {
                    // This appears in yellow as it's coming from VS
                    _eventEmitter.EmitOutput(OutputCategory.Console, message + Environment.NewLine);
                }

                _previousLogMessage = message;
            }
        }

        private void Connect(LaunchData options, IPAddress address, int port)
        {
            lock (_lock)
            {
                _debuggeeKilled = false;

                var args = new Mono.Debugging.Soft.SoftDebuggerConnectArgs(options.GetBuildProperty("AssemblyName", "") ?? "", address, port)
                {
                    MaxConnectionAttempts = MAX_CONNECTION_ATTEMPTS,
                    TimeBetweenConnectionAttempts = CONNECTION_ATTEMPT_INTERVAL
                };

                _debuggeeExecuting = true;
                _session?.Run(new Mono.Debugging.Soft.SoftDebuggerStartInfo(args), _debuggerSessionOptions);
            }
        }

        public override void Attach(Response response, dynamic args)
        {
            _attachMode = true;

            // Safely access __exceptionOptions - it may not exist on the dynamic args
            dynamic? exceptionOptions = null;
            try
            {
                exceptionOptions = args.__exceptionOptions;
            }
            catch
            {
                // Property doesn't exist, which is fine - SetExceptionBreakpoints handles null
            }
            SetExceptionBreakpoints(exceptionOptions);

            // validate argument 'address'
            var host = GetString(args, "address");
            if (host == null)
            {
                SendErrorResponse(response, 3007, "Property 'address' is missing or empty.");
                return;
            }

            // validate argument 'port'
            var port = GetInt(args, "port", -1);
            if (port == -1)
            {
                SendErrorResponse(response, 3008, "Property 'port' is missing.");
                return;
            }

            IPAddress? address = DapUtilities.ResolveIPAddress(host);
            if (address == null)
            {
                SendErrorResponse(response, 3013, "Invalid address '{address}'.", new { address = address });
                return;
            }

            Connect(address, port);

            SendResponse(response);
        }

        public override async Task Disconnect(Response response, dynamic arguments)
        {
            if (_meadowDeployer != null)
                _meadowDeployer.Dispose();

            if (_meadowDebuggingServer != null)
            {
                try { await _meadowDebuggingServer.StopListening(); } catch { }

                try { _meadowDebuggingServer.Dispose(); }
                finally { _meadowDebuggingServer = null; }
            }

            if (_ctsDeployMeadow != null && !_ctsDeployMeadow.IsCancellationRequested)
                _ctsDeployMeadow.Cancel();

            if (_attachMode)
            {
                lock (_lock)
                {
                    if (_session != null)
                    {
                        _debuggeeExecuting = true;
                        _breakpoints.Clear();
                        _session.Breakpoints.Clear();
                        _session.Continue();
                        _session = null;
                    }
                }
            }
            else
            {
                // Let's not leave dead Mono processes behind...
                if (_process != null)
                {
                    _process.Kill();
                    _process = null;
                }
                else
                {
                    PauseDebugger();
                    DebuggerKill();

                    while (!_debuggeeKilled)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }
            }

            SendResponse(response);
        }

        public override void Continue(Response response, dynamic args)
        {
            WaitForSuspend();
            SendResponse(response);
            lock (_lock)
            {
                if (_session != null && !_session.IsRunning && !_session.HasExited)
                {
                    _session.Continue();
                    _debuggeeExecuting = true;
                }
            }
        }

        public override void Next(Response response, dynamic args)
        {
            WaitForSuspend();
            SendResponse(response);
            lock (_lock)
            {
                if (_session != null && !_session.IsRunning && !_session.HasExited)
                {
                    _session.NextLine();
                    _debuggeeExecuting = true;
                }
            }
        }

        public override void StepIn(Response response, dynamic args)
        {
            WaitForSuspend();
            SendResponse(response);
            lock (_lock)
            {
                if (_session != null && !_session.IsRunning && !_session.HasExited)
                {
                    _session.StepLine();
                    _debuggeeExecuting = true;
                }
            }
        }

        public override void StepOut(Response response, dynamic args)
        {
            WaitForSuspend();
            SendResponse(response);
            lock (_lock)
            {
                if (_session != null && !_session.IsRunning && !_session.HasExited)
                {
                    _session.Finish();
                    _debuggeeExecuting = true;
                }
            }
        }

        public override void Pause(Response response, dynamic args)
        {
            SendResponse(response);
            PauseDebugger();
        }

        public override void SetExceptionBreakpoints(Response response, dynamic args)
        {
            SetExceptionBreakpoints(args.exceptionOptions);
            SendResponse(response);
        }

        public override void SetBreakpoints(Response response, dynamic args)
        {
            string? path = null;
            if (args.source != null)
            {
                string? p = (string?)args.source.path;
                if (p != null && p.Trim().Length > 0)
                {
                    path = p;
                }
            }
            if (path == null)
            {
                SendErrorResponse(response, 3010, "setBreakpoints: property 'source' is empty or misformed", null, false, true);
                return;
            }
            path = ConvertClientPathToDebugger(path);

            if (path == null || !HasMonoExtension(path))
            {
                // we only support breakpoints in files mono can handle
                SendResponse(response, new SetBreakpointsResponseBody());
                return;
            }

            var clientLines = args.lines.ToObject<int[]>();
            HashSet<int> lin = new HashSet<int>();
            for (int i = 0; i < clientLines.Length; i++)
            {
                lin.Add(ConvertClientLineToDebugger(clientLines[i]));
            }

            // find all breakpoints for the given path and remember their id and line number
            var bpts = new List<Tuple<int, int>>();
            foreach (var be in _breakpoints)
            {
                var bp = be.Value as Mono.Debugging.Client.Breakpoint;
                if (bp != null && bp.FileName == path)
                {
                    bpts.Add(new Tuple<int, int>((int)be.Key, (int)bp.Line));
                }
            }

            HashSet<int> lin2 = new HashSet<int>();
            foreach (var bpt in bpts)
            {
                if (lin.Contains(bpt.Item2))
                {
                    lin2.Add(bpt.Item2);
                }
                else
                {
                    BreakEvent? b;
                    if (_breakpoints.TryGetValue(bpt.Item1, out b))
                    {
                        _breakpoints.Remove(bpt.Item1);
                        _session?.Breakpoints.Remove(b);
                    }
                }
            }

            for (int i = 0; i < clientLines.Length; i++)
            {
                var l = ConvertClientLineToDebugger(clientLines[i]);
                if (!lin2.Contains(l))
                {
                    var id = _nextBreakpointId++;
                    var bp = _session?.Breakpoints.Add(path, l);
                    if (bp != null)
                    {
                        _breakpoints.Add(id, bp);
                    }
                }
            }

            var breakpoints = new List<Protocol.Breakpoint>();
            foreach (var l in clientLines)
            {
                breakpoints.Add(new Protocol.Breakpoint(true, l));
            }

            SendResponse(response, new SetBreakpointsResponseBody(breakpoints));
        }

        public override void StackTrace(Response response, dynamic args)
        {
            int maxLevels = GetInt(args, "levels", 10);
            int threadReference = GetInt(args, "threadId", 0);

            WaitForSuspend();

            ThreadInfo? thread = DebuggerActiveThread();
            if (thread != null && thread.Id != threadReference)
            {
                thread = FindThread(threadReference);
                if (thread != null)
                {
                    thread.SetActive();
                }
            }

            var stackFrames = new List<Protocol.StackFrame>();
            int totalFrames = 0;

            var bt = thread?.Backtrace;
            if (bt != null && bt.FrameCount >= 0)
            {
                totalFrames = bt.FrameCount;

                for (var i = 0; i < Math.Min(totalFrames, maxLevels); i++)
                {
                    var frame = bt.GetFrame(i);

                    string? path = frame.SourceLocation.FileName;

                    var hint = "subtle";
                    Protocol.Source? source = null;
                    if (!string.IsNullOrEmpty(path))
                    {
                        string sourceName = Path.GetFileName(path);
                        if (!string.IsNullOrEmpty(sourceName))
                        {
                            if (File.Exists(path))
                            {
                                source = new Protocol.Source(sourceName, ConvertDebuggerPathToClient(path), 0, "normal");
                                hint = "normal";
                            }
                            else
                            {
                                source = new Protocol.Source(sourceName, null, 1000, "deemphasize");
                            }
                        }
                    }

                    var frameHandle = _frameHandles.Create(frame);
                    string? name = frame.SourceLocation.MethodName;
                    int line = frame.SourceLocation.Line;
                    stackFrames.Add(new Protocol.StackFrame(frameHandle, name ?? "<unknown>", source, ConvertDebuggerLineToClient(line), 0, hint));
                }
            }

            SendResponse(response, new StackTraceResponseBody(stackFrames, totalFrames));
        }

        public override void Source(Response response, dynamic arguments)
        {
            SendErrorResponse(response, 1020, "No source available");
        }

        public override void Scopes(Response response, dynamic args)
        {
            int frameId = GetInt(args, "frameId", 0);
            var frame = _frameHandles.Get(frameId, null);

            var scopes = new List<Protocol.Scope>();

            if (frame != null)
            {
                if (frame.Index == 0 && _exception != null)
                {
                    scopes.Add(new Protocol.Scope("Exception", _variableHandles.Create(new ObjectValue[] { _exception })));
                }

                var locals = new[] { frame.GetThisReference() }.Concat(frame.GetParameters()).Concat(frame.GetLocalVariables()).Where(x => x != null).ToArray();
                if (locals.Length > 0)
                {
                    scopes.Add(new Protocol.Scope("Local", _variableHandles.Create(locals!)));
                }
            }

            SendResponse(response, new ScopesResponseBody(scopes));
        }

        public override void Variables(Response response, dynamic args)
        {
            int reference = GetInt(args, "variablesReference", -1);
            if (reference == -1)
            {
                SendErrorResponse(response, 3009, "variables: property 'variablesReference' is missing", null, false, true);
                return;
            }

            WaitForSuspend();
            var variables = new List<Protocol.Variable>();

            ObjectValue[]? children;
            if (_variableHandles.TryGet(reference, out children))
            {
                if (children != null && children.Length > 0)
                {
                    bool more = false;
                    if (children.Length > MAX_CHILDREN)
                    {
                        children = children.Take(MAX_CHILDREN).ToArray();
                        more = true;
                    }

                    if (children.Length < 20)
                    {
                        // Wait for all values at once.
                        WaitHandle.WaitAll(children.Select(x => x.WaitHandle).ToArray());
                        foreach (var v in children)
                        {
                            variables.Add(CreateVariable(v));
                        }
                    }
                    else
                    {
                        foreach (var v in children)
                        {
                            v.WaitHandle.WaitOne();
                            variables.Add(CreateVariable(v));
                        }
                    }

                    if (more)
                    {
                        variables.Add(new Protocol.Variable("...", null, null));
                    }
                }
            }

            SendResponse(response, new VariablesResponseBody(variables));
        }

        public override void Threads(Response response, dynamic args)
        {
            var threads = new List<Protocol.Thread>();
            var process = _activeProcess;
            if (process != null)
            {
                Dictionary<int, Protocol.Thread> d;
                lock (_seenThreads)
                {
                    d = new Dictionary<int, Protocol.Thread>(_seenThreads);
                }
                foreach (var t in process.GetThreads())
                {
                    int tid = (int)t.Id;
                    d[tid] = new Protocol.Thread(tid, t.Name);
                }
                threads = d.Values.ToList();
            }
            SendResponse(response, new ThreadsResponseBody(threads));
        }

        public override void Evaluate(Response response, dynamic args)
        {
            string? error = null;

            var expression = GetString(args, "expression");
            if (expression == null)
            {
                error = "expression missing";
            }
            else
            {
                int frameId = GetInt(args, "frameId", -1);
                var frame = _frameHandles.Get(frameId, null);
                if (frame != null)
                {
                    if (frame.ValidateExpression(expression))
                    {
                        var val = frame.GetExpressionValue(expression, _debuggerSessionOptions.EvaluationOptions);
                        val.WaitHandle.WaitOne();

                        var flags = val.Flags;
                        if (flags.HasFlag(ObjectValueFlags.Error) || flags.HasFlag(ObjectValueFlags.NotSupported))
                        {
                            error = val.DisplayValue;
                            if (error != null && error.IndexOf("reference not available in the current evaluation context") > 0)
                            {
                                error = "not available";
                            }
                        }
                        else if (flags.HasFlag(ObjectValueFlags.Unknown))
                        {
                            error = "invalid expression";
                        }
                        else if (flags.HasFlag(ObjectValueFlags.Object) && flags.HasFlag(ObjectValueFlags.Namespace))
                        {
                            error = "not available";
                        }
                        else
                        {
                            int handle = 0;
                            if (val.HasChildren)
                            {
                                handle = _variableHandles.Create(val.GetAllChildren());
                            }
                            SendResponse(response, new EvaluateResponseBody(val.DisplayValue, handle));
                            return;
                        }
                    }
                    else
                    {
                        error = "invalid expression";
                    }
                }
                else
                {
                    error = "no active stackframe";
                }
            }
            SendErrorResponse(response, 3014, "Evaluate request failed ({_reason}).", new { _reason = error });
        }

        //---- private ------------------------------------------

        private void SetExceptionBreakpoints(dynamic? exceptionOptions)
        {
            if (exceptionOptions != null)
            {
                try
                {
                    // clear all existing catchpoints
                    foreach (var cp in _catchpoints)
                    {
                        _session?.Breakpoints.Remove(cp);
                    }
                    _catchpoints.Clear();

                    var exceptions = exceptionOptions.ToObject<dynamic[]>();
                    for (int i = 0; i < exceptions.Length; i++)
                    {
                        var exception = exceptions[i];

                        string? exName = null;
                        string exBreakMode = exception.breakMode;

                        if (exception.path != null)
                        {
                            var paths = exception.path.ToObject<dynamic[]>();
                            var path = paths[0];
                            if (path.names != null)
                            {
                                var names = path.names.ToObject<dynamic[]>();
                                if (names.Length > 0)
                                {
                                    exName = names[0];
                                }
                            }
                        }

                        if (exName != null && exBreakMode == "always")
                        {
                            var cp = _session?.Breakpoints.AddCatchpoint(exName);
                            if (cp != null)
                            {
                                _catchpoints.Add(cp);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Exception setting exception breakpoints, but don't crash
                }
            }
        }

        private void SendOutput(string category, string data)
        {
            if (!String.IsNullOrEmpty(data))
            {
                if (data[data.Length - 1] != '\n')
                {
                    data += '\n';
                }

                if (category.Contains("stdout") || category.Contains("stderr"))
                {
                    // This appears in the "Meadow" tab
                    _eventEmitter.EmitOutput(OutputCategory.Meadow, data);
                }
                else
                {
                    // This appears in the "Console" tab
                    _eventEmitter.EmitOutput(OutputCategory.Console, data + Environment.NewLine);
                }
            }
        }

        private void Terminate(string reason)
        {
            if (!_terminated)
            {
                // wait until we've seen the end of stdout and stderr
                for (int i = 0; i < 100 && (_stdoutEOF == false || _stderrEOF == false); i++)
                {
                    System.Threading.Thread.Sleep(100);
                }

                SendEvent(new TerminatedEvent());

                _terminated = true;
                _process = null;
            }
        }

        private StoppedEvent CreateStoppedEvent(string reason, ThreadInfo ti, string? text = null)
        {
            return new StoppedEvent((int)ti.Id, reason, text);
        }

        private ThreadInfo? FindThread(int threadReference)
        {
            if (_activeProcess != null)
            {
                foreach (var t in _activeProcess.GetThreads())
                {
                    if (t.Id == threadReference)
                    {
                        return t;
                    }
                }
            }
            return null;
        }

        private void Stopped()
        {
            _exception = null;
            _variableHandles.Reset();
            _frameHandles.Reset();
        }

        private Protocol.Variable CreateVariable(ObjectValue v)
        {
            var dv = v.DisplayValue;
            if (dv == null)
            {
                dv = "<error getting value>";
            }

            if (dv.Length > 1 && dv[0] == '{' && dv[dv.Length - 1] == '}')
            {
                dv = dv.Substring(1, dv.Length - 2);
            }
            return new Protocol.Variable(v.Name, dv, v.TypeName, v.HasChildren ? _variableHandles.Create(v.GetAllChildren()) : 0);
        }

        private bool HasMonoExtension(string path)
        {
            foreach (var e in MONO_EXTENSIONS)
            {
                if (path.EndsWith(e))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool GetBool(dynamic container, string propertyName, bool dflt = false)
        {
            try
            {
                return (bool)container[propertyName];
            }
            catch (Exception)
            {
                // ignore and return default value
            }
            return dflt;
        }

        private static int GetInt(dynamic container, string propertyName, int dflt = 0)
        {
            try
            {
                return (int)container[propertyName];
            }
            catch (Exception)
            {
                // ignore and return default value
            }
            return dflt;
        }

        private static string? GetString(dynamic args, string property, string? dflt = null)
        {
            try
            {
                var s = (string?)args[property];
                if (s == null)
                {
                    return dflt;
                }
                s = s.Trim();
                if (s.Length == 0)
                {
                    return dflt;
                }
                return s;
            }
            catch
            {
                return dflt;
            }
        }

        //-----------------------

        private void WaitForSuspend()
        {
            if (_debuggeeExecuting)
            {
                _resumeEvent.WaitOne();
                _debuggeeExecuting = false;
            }
        }

        private ThreadInfo? DebuggerActiveThread()
        {
            lock (_lock)
            {
                return _session?.ActiveThread;
            }
        }

        private Backtrace? DebuggerActiveBacktrace()
        {
            var thr = DebuggerActiveThread();
            return thr?.Backtrace;
        }

        private Mono.Debugging.Client.StackFrame? DebuggerActiveFrame()
        {
            if (_activeFrame != null)
                return _activeFrame;

            var bt = DebuggerActiveBacktrace();
            if (bt != null)
                return _activeFrame = bt.GetFrame(0);

            return null;
        }

        private ExceptionInfo? DebuggerActiveException()
        {
            var bt = DebuggerActiveBacktrace();
            return bt?.GetFrame(0).GetException();
        }

        private void Connect(IPAddress address, int port)
        {
            lock (_lock)
            {
                _debuggeeKilled = false;

                var args0 = new Mono.Debugging.Soft.SoftDebuggerConnectArgs(string.Empty, address, port)
                {
                    MaxConnectionAttempts = MAX_CONNECTION_ATTEMPTS,
                    TimeBetweenConnectionAttempts = CONNECTION_ATTEMPT_INTERVAL
                };

                _session?.Run(new Mono.Debugging.Soft.SoftDebuggerStartInfo(args0), _debuggerSessionOptions);

                _debuggeeExecuting = true;
            }
        }

        private void PauseDebugger()
        {
            lock (_lock)
            {
                if (_session != null && _session.IsRunning)
                    _session.Stop();
            }
        }

        private void DebuggerKill()
        {
            lock (_lock)
            {
                if (_session != null)
                {
                    _debuggeeExecuting = false;

                    if (!_session.HasExited)
                        _session.Exit();

                    _session.Dispose();
                    _session = null;
                }
            }
        }
    }
}
