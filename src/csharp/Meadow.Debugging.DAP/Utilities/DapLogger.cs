/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Wilderness Labs. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.IO;

namespace Meadow.Debugging.DAP.Utilities
{
    /// <summary>
    /// Provides logging functionality for the DAP debug adapter.
    /// </summary>
    public static class DapLogger
    {
        private static TextWriter? _logFile;
        private static string? _logFilePath;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets or sets the log file path. When set, logs are written to this file.
        /// </summary>
        public static string? LogFilePath
        {
            get => _logFilePath;
            set
            {
                lock (_lock)
                {
                    _logFilePath = value;
                    if (_logFile != null)
                    {
                        _logFile.Flush();
                        _logFile.Close();
                        _logFile = null;
                    }
                }
            }
        }

        /// <summary>
        /// Log a message if the predicate is true.
        /// </summary>
        public static void Log(bool predicate, string format, params object[] data)
        {
            if (predicate)
            {
                Log(format, data);
            }
        }

        /// <summary>
        /// Log a message.
        /// </summary>
        public static void Log(string format, params object[] data)
        {
            try
            {
                Console.Error.WriteLine(format, data);

                lock (_lock)
                {
                    if (_logFilePath != null)
                    {
                        if (_logFile == null)
                        {
                            _logFile = File.CreateText(_logFilePath);
                        }

                        string msg = string.Format(format, data);
                        _logFile.WriteLine(string.Format("{0} {1}", DateTime.UtcNow.ToLongTimeString(), msg));
                        _logFile.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logFilePath != null)
                {
                    try
                    {
                        File.WriteAllText(_logFilePath + ".err", ex.ToString());
                    }
                    catch
                    {
                        // Ignore errors writing error log
                    }
                }
            }
        }

        /// <summary>
        /// Close the log file if open.
        /// </summary>
        public static void Close()
        {
            lock (_lock)
            {
                if (_logFile != null)
                {
                    _logFile.Flush();
                    _logFile.Close();
                    _logFile = null;
                }
            }
        }
    }
}
