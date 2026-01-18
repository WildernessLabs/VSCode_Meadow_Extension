/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using Newtonsoft.Json;

namespace Meadow.Debugging.DAP.Protocol
{
    /// <summary>
    /// Base class for all DAP protocol messages.
    /// </summary>
    public class ProtocolMessage
    {
        public int seq;
        public string type = string.Empty;

        public ProtocolMessage()
        {
        }

        public ProtocolMessage(string typ)
        {
            type = typ;
        }

        public ProtocolMessage(string typ, int sq)
        {
            type = typ;
            seq = sq;
        }
    }

    /// <summary>
    /// A DAP request message.
    /// </summary>
    public class Request : ProtocolMessage
    {
        public string command = string.Empty;
        public dynamic? arguments;

        public Request()
        {
        }

        public Request(string cmd, dynamic? arg) : base("request")
        {
            command = cmd;
            arguments = arg;
        }

        public Request(int id, string cmd, dynamic? arg) : base("request", id)
        {
            command = cmd;
            arguments = arg;
        }
    }

    /// <summary>
    /// Base class for response bodies. Subclasses are serialized as the body of a response.
    /// Don't change their instance variables since that will break the debug protocol.
    /// </summary>
    public class ResponseBody
    {
        // empty
    }

    /// <summary>
    /// A DAP response message.
    /// </summary>
    public class Response : ProtocolMessage
    {
        public bool success;
        public string? message;
        public int request_seq;
        public string command = string.Empty;
        public ResponseBody? body;

        public Response()
        {
        }

        public Response(Request req) : base("response")
        {
            success = true;
            request_seq = req.seq;
            command = req.command;
        }

        public void SetBody(ResponseBody bdy)
        {
            success = true;
            body = bdy;
        }

        public void SetErrorBody(string msg, ResponseBody? bdy = null)
        {
            success = false;
            message = msg;
            body = bdy;
        }
    }

    /// <summary>
    /// A DAP event message.
    /// </summary>
    public class Event : ProtocolMessage
    {
        [JsonProperty(PropertyName = "event")]
        public string eventType { get; }

        public dynamic? body { get; }

        public Event(string type, dynamic? bdy = null) : base("event")
        {
            eventType = type;
            body = bdy;
        }
    }
}
