/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System.Collections.Generic;
using System.Linq;

namespace Meadow.Debugging.DAP.Protocol
{
    // ---- Response Bodies -------------------------------------------------------------------------

    public class Capabilities : ResponseBody
    {
        public bool supportsConfigurationDoneRequest;
        public bool supportsFunctionBreakpoints;
        public bool supportsConditionalBreakpoints;
        public bool supportsEvaluateForHovers;
        public bool supportsProgressReporting;
        public dynamic[]? exceptionBreakpointFilters;
    }

    public class ErrorResponseBody : ResponseBody
    {
        public Message error { get; }

        public ErrorResponseBody(Message error)
        {
            this.error = error;
        }
    }

    public class StackTraceResponseBody : ResponseBody
    {
        public StackFrame[] stackFrames { get; }
        public int totalFrames { get; }

        public StackTraceResponseBody(List<StackFrame> frames, int total)
        {
            stackFrames = frames.ToArray<StackFrame>();
            totalFrames = total;
        }
    }

    public class ScopesResponseBody : ResponseBody
    {
        public Scope[] scopes { get; }

        public ScopesResponseBody(List<Scope> scps)
        {
            scopes = scps.ToArray<Scope>();
        }
    }

    public class VariablesResponseBody : ResponseBody
    {
        public Variable[] variables { get; }

        public VariablesResponseBody(List<Variable> vars)
        {
            variables = vars.ToArray<Variable>();
        }
    }

    public class ThreadsResponseBody : ResponseBody
    {
        public Thread[] threads { get; }

        public ThreadsResponseBody(List<Thread> ths)
        {
            threads = ths.ToArray<Thread>();
        }
    }

    public class EvaluateResponseBody : ResponseBody
    {
        public string result { get; }
        public int variablesReference { get; }

        public EvaluateResponseBody(string value, int reff = 0)
        {
            result = value;
            variablesReference = reff;
        }
    }

    public class SetBreakpointsResponseBody : ResponseBody
    {
        public Breakpoint[] breakpoints { get; }

        public SetBreakpointsResponseBody(List<Breakpoint>? bpts = null)
        {
            if (bpts == null)
                breakpoints = new Breakpoint[0];
            else
                breakpoints = bpts.ToArray<Breakpoint>();
        }
    }
}
