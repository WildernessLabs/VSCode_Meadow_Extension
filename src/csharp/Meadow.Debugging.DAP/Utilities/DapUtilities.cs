/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;
using Mono.Debugging.Client;

namespace Meadow.Debugging.DAP.Utilities
{
    /// <summary>
    /// Utility functions for the DAP debug adapter.
    /// </summary>
    public static class DapUtilities
    {
        private const string WHICH = "/usr/bin/which";
        private const string WHERE = "where";

        private static readonly Regex VARIABLE = new Regex(@"\{(\w+)\}");

        private static char[] ARGUMENT_SEPARATORS = new char[] { ' ', '\t' };

        /// <summary>
        /// Enclose the given string in quotes if it contains space or tab characters.
        /// </summary>
        public static string Quote(string arg)
        {
            if (arg.IndexOfAny(ARGUMENT_SEPARATORS) >= 0)
            {
                return '"' + arg + '"';
            }
            return arg;
        }

        /// <summary>
        /// Fix path separators for the current platform.
        /// </summary>
        public static string? FixPathSeparators(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                return path.Replace("/", "\\");

            return path.Replace("\\", "/");
        }

        /// <summary>
        /// Is the given runtime executable on the PATH.
        /// </summary>
        public static bool IsOnPath(string runtime)
        {
            var process = new Process();

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.FileName = File.Exists(WHICH) ? WHICH : WHERE;
            process.StartInfo.Arguments = Quote(runtime);

            try
            {
                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (Exception)
            {
                // ignore
            }

            return false;
        }

        /// <summary>
        /// Concatenate command-line arguments with optional quoting.
        /// </summary>
        public static string ConcatArgs(string[]? args, bool quote = true)
        {
            var arg = "";
            if (args != null)
            {
                foreach (var r in args)
                {
                    if (arg.Length > 0)
                    {
                        arg += " ";
                    }
                    arg += quote ? Quote(r) : r;
                }
            }
            return arg;
        }

        /// <summary>
        /// Resolve hostname, dotted-quad notation for IPv4, or colon-hexadecimal notation for IPv6 to IPAddress.
        /// Returns null on failure.
        /// </summary>
        public static IPAddress? ResolveIPAddress(string addressString)
        {
            try
            {
                IPAddress? ipaddress = null;
                if (IPAddress.TryParse(addressString, out ipaddress))
                {
                    return ipaddress;
                }

                IPHostEntry entry = Dns.GetHostEntry(addressString);
                if (entry != null && entry.AddressList != null && entry.AddressList.Length > 0)
                {
                    if (entry.AddressList.Length == 1)
                    {
                        return entry.AddressList[0];
                    }
                    foreach (IPAddress address in entry.AddressList)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return address;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // fall through
            }

            return null;
        }

        /// <summary>
        /// Find a free socket port.
        /// </summary>
        public static int FindFreePort(int fallback)
        {
            TcpListener? l = null;
            try
            {
                l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                return ((IPEndPoint)l.LocalEndpoint).Port;
            }
            catch (Exception)
            {
                // ignore
            }
            finally
            {
                l?.Stop();
            }
            return fallback;
        }

        /// <summary>
        /// Expand variables in a format string using property values from a dynamic object.
        /// </summary>
        public static string ExpandVariables(string? format, dynamic? variables, bool underscoredOnly = true)
        {
            if (string.IsNullOrWhiteSpace(format))
                return format ?? string.Empty;
            if (variables == null)
            {
                variables = new { };
            }
            Type type = variables.GetType();
            return VARIABLE.Replace(format, match =>
            {
                string name = match.Groups[1].Value;
                if (!underscoredOnly || name.StartsWith("_"))
                {
                    PropertyInfo? property = type.GetProperty(name);
                    if (property != null)
                    {
                        object? value = property.GetValue(variables, null);
                        return value?.ToString() ?? string.Empty;
                    }
                    return '{' + name + ": not found}";
                }
                return match.Groups[0].Value;
            });
        }

        /// <summary>
        /// Converts the given absPath into a path that is relative to the given dirPath.
        /// </summary>
        public static string MakeRelativePath(string dirPath, string absPath)
        {
            if (!dirPath.EndsWith("/"))
            {
                dirPath += "/";
            }
            if (absPath.StartsWith(dirPath))
            {
                return absPath.Replace(dirPath, "");
            }
            return absPath;
        }
    }

    /// <summary>
    /// Custom logger implementation for Mono.Debugging.
    /// </summary>
    public class CustomLogger : ICustomLogger
    {
        public void LogError(string message, Exception ex)
        {
        }

        public void LogAndShowException(string message, Exception ex)
        {
        }

        public void LogMessage(string format, params object[] args)
        {
        }

        public string? GetNewDebuggerLogFilename()
        {
            return null;
        }
    }
}
