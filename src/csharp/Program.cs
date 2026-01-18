/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Meadow.Debugging.DAP.Session;
using Meadow.Debugging.DAP.Utilities;
using VsCodeMeadowUtil;

namespace VSCodeDebug
{
    internal class Program
    {
        const int DEFAULT_PORT = 4711;

        private static bool trace_requests;
        private static bool trace_responses;

        private static async Task Main(string[] argv)
        {
            if (argv.Length > 0 && argv[0] == "util")
            {
                UtilRunner.UtilMain(argv.ToList().Skip(1).ToArray());
                return;
            }

            int port = -1;

            // parse command line arguments
            foreach (var a in argv)
            {
                switch (a)
                {
                    case "--trace":
                        trace_requests = true;
                        break;
                    case "--trace=response":
                        trace_requests = true;
                        trace_responses = true;
                        break;
                    case "--server":
                        port = DEFAULT_PORT;
                        break;
                    default:
                        if (a.StartsWith("--server="))
                        {
                            if (!int.TryParse(a.Substring("--server=".Length), out port))
                            {
                                port = DEFAULT_PORT;
                            }
                        }
                        else if (a.StartsWith("--log-file="))
                        {
                            DapLogger.LogFilePath = a.Substring("--log-file=".Length);
                        }
                        break;
                }
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("mono_debug_logfile")))
            {
                DapLogger.LogFilePath = Environment.GetEnvironmentVariable("mono_debug_logfile");
                trace_requests = true;
                trace_responses = true;
            }

            if (port > 0)
            {
                // TCP/IP server
                DapLogger.Log("waiting for debug protocol on port " + port);
                await RunServer(port);
            }
            else
            {
                // stdin/stdout
                DapLogger.Log("waiting for debug protocol on stdin/stdout");
                await RunSession(Console.OpenStandardInput(), Console.OpenStandardOutput());
            }
        }

        private static async Task RunSession(Stream inputStream, Stream outputStream)
        {
            var debugSession = new MeadowDebugSession(LaunchPropertyKeys.VSCode);
            debugSession.TRACE = trace_requests;
            debugSession.TRACE_RESPONSE = trace_responses;
            await debugSession.Start(inputStream, outputStream);

            DapLogger.Close();
        }

        private static async Task RunServer(int port)
        {
            using var serverSocket = new TcpListener(IPAddress.Loopback, port);
            serverSocket.Start();

            while (true)
            {
                try
                {
                    var clientSocket = await serverSocket.AcceptSocketAsync();
                    if (clientSocket != null)
                    {
                        DapLogger.Log(">> Accepted connection from client");

                        try
                        {
                            using (var networkStream = new NetworkStream(clientSocket))
                            {
                                await RunSession(networkStream, networkStream);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine("Exception during session:", e);
                        }
                        finally
                        {
                            if (clientSocket.Connected)
                            {
                                clientSocket.Shutdown(SocketShutdown.Both);
                            }
                            clientSocket.Close();
                            DapLogger.Log(">> Client connection closed");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Exception in server loop:", e);
                }
            }
        }
    }
}
