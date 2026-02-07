/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace Meadow.Debugging.DAP.Protocol
{
    // ---- Events -------------------------------------------------------------------------

    public class InitializedEvent : Event
    {
        public InitializedEvent()
            : base("initialized") { }
    }

    public class StoppedEvent : Event
    {
        public StoppedEvent(int tid, string reasn, string? txt = null)
            : base("stopped", new
            {
                threadId = tid,
                reason = reasn,
                text = txt
            })
        { }
    }

    public class ExitedEvent : Event
    {
        public ExitedEvent(int exCode)
            : base("exited", new { exitCode = exCode }) { }
    }

    public class TerminatedEvent : Event
    {
        public TerminatedEvent()
            : base("terminated") { }
    }

    public class ThreadEvent : Event
    {
        public ThreadEvent(string reasn, int tid)
            : base("thread", new
            {
                reason = reasn,
                threadId = tid
            })
        { }
    }

    public class ConsoleOutputEvent : Event
    {
        public ConsoleOutputEvent(string outpt)
            : base("output", new
            {
                category = "console",
                output = outpt
            })
        { }
    }

    public class MeadowOutputEvent : Event
    {
        public MeadowOutputEvent(string outpt)
            : base("output", new
            {
                category = "stdout",
                output = outpt
            })
        { }
    }

    public class ProgressStartEvent : Event
    {
        public ProgressStartEvent(string progressId, string title, int? percentage = null)
            : base("progressStart", new
            {
                progressId = progressId,
                title = title,
                percentage = percentage
            })
        { }
    }

    public class ProgressUpdateEvent : Event
    {
        public ProgressUpdateEvent(string progressId, string? message = null, int? percentage = null)
            : base("progressUpdate", new
            {
                progressId = progressId,
                message = message,
                percentage = percentage
            })
        { }
    }

    public class ProgressEndEvent : Event
    {
        public ProgressEndEvent(string progressId, string? message = null)
            : base("progressEnd", new
            {
                progressId = progressId,
                message = message
            })
        { }
    }
}
