#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace Meadow.Debugging.DAP.Utilities
{
    /// <summary>
    /// Configuration data for launching a Meadow debug session.
    /// IDE-agnostic representation of launch configuration.
    /// </summary>
    public class LaunchData
    {
        public string? ProjectPath { get; set; }
        public string? ProjectConfiguration { get; set; }
        public string? MSBuildPropertyFile { get; set; }
        public int DebugPort { get; set; } = -1;
        public string? Serial { get; set; }
        public bool SkipDeploy { get; set; } = false;
        public IReadOnlyDictionary<string, string>? MSBuildProperties { get; set; }

        private Dictionary<string, string> _debugInfoProps = new Dictionary<string, string>();

        public LaunchData()
        {
        }

        /// <summary>
        /// Create LaunchData from dynamic launch arguments.
        /// </summary>
        /// <param name="args">The dynamic arguments object from DAP</param>
        /// <param name="propertyKeys">The property key names to extract (IDE-specific)</param>
        public LaunchData(dynamic args, LaunchPropertyKeys propertyKeys)
        {
            ProjectPath = GetString(args, propertyKeys.ProjectPath);
            ProjectConfiguration = GetString(args, propertyKeys.Configuration, "Debug");
            DebugPort = GetInt(args, propertyKeys.DebugPort, 55555);
            Serial = GetString(args, propertyKeys.Serial);
            MSBuildPropertyFile = CleanseStringPaths(GetString(args, propertyKeys.MSBuildPropertyFile));
            SkipDeploy = GetBool(args, propertyKeys.SkipDeploy, false);
        }

        public string? GetBuildProperty(string propertyName, string? defaultValue = null)
        {
            if (_debugInfoProps.Count <= 0)
            {
                ParseDebugInfoPropsFile();
            }

            if (_debugInfoProps.TryGetValue(propertyName.ToLowerInvariant(), out var value))
            {
                return value;
            }

            return defaultValue;
        }

        void ParseDebugInfoPropsFile()
        {
            if (string.IsNullOrEmpty(MSBuildPropertyFile))
            {
                return;
            }

            try
            {
                if (!File.Exists(MSBuildPropertyFile))
                {
                    throw new FileNotFoundException($"MSBuild property file not found at: {MSBuildPropertyFile}");
                }

                var lines = File.ReadAllLines(MSBuildPropertyFile);
                if (lines.Length == 0)
                {
                    throw new InvalidOperationException($"MSBuild property file is empty: {MSBuildPropertyFile}");
                }

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2)
                    {
                        _debugInfoProps[parts[0].ToLowerInvariant()] = parts[1];
                    }
                }

                if (_debugInfoProps.Count == 0)
                {
                    throw new InvalidOperationException($"No valid key=value properties found in MSBuild property file: {MSBuildPropertyFile}");
                }
            }
            catch (Exception ex)
            {
                // Re-throw the exception with context so Launch() can catch and handle it
                throw new InvalidOperationException($"Failed to parse MSBuild property file: {ex.Message}", ex);
            }
        }

        public (bool success, string message) Validate()
        {
            (bool success, string message) validateString(string? value, string name)
                => string.IsNullOrWhiteSpace(value) ? (false, $"{name} is not valid") : (true, "");
            
            var checks = new[] {
                validateString(ProjectPath, nameof(ProjectPath)),
                validateString(ProjectConfiguration, nameof(ProjectConfiguration)),
                validateString(Serial, nameof(Serial)),
                validateString(MSBuildPropertyFile, nameof(MSBuildPropertyFile)),
            };
            foreach (var check in checks)
            {
                if (!check.success)
                    return check;
            }

            // Check if the MSBuildPropertyFile actually exists
            if (!File.Exists(MSBuildPropertyFile))
            {
                return (false, $"MSBuildPropertyFile does not exist at: {MSBuildPropertyFile}");
            }

            if (string.IsNullOrWhiteSpace(Serial))
                return (false, "Meadow Serial is not valid");

            return (true, "");
        }

        private static string? CleanseStringPaths(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            if (IsWindows)
                return path;
            return path.Replace("\\", "/");
        }

        private static bool IsWindows =>
            Environment.OSVersion.Platform == PlatformID.Win32NT ||
            Environment.OSVersion.Platform == PlatformID.Win32S ||
            Environment.OSVersion.Platform == PlatformID.Win32Windows ||
            Environment.OSVersion.Platform == PlatformID.WinCE;

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
                var s = (string)args[property];
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

        private static IReadOnlyDictionary<string, string> GetDictionary(dynamic container, string propertyName)
        {
            try
            {
                var c = container[propertyName];
                var d = new Dictionary<string, string>();
                foreach (var propertyDescriptor in System.ComponentModel.TypeDescriptor.GetProperties(c))
                {
                    string? obj = propertyDescriptor?.GetValue(c);
                    if (!string.IsNullOrEmpty(obj))
                        d.Add(propertyDescriptor!.Name, obj);
                }

                return d;
            }
            catch (Exception)
            {
                // ignore and return default value
            }
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Property key names used to extract launch configuration values.
    /// Different IDEs may use different key names.
    /// </summary>
    public class LaunchPropertyKeys
    {
        public string Configuration { get; set; } = "projectConfiguration";
        public string MSBuildPropertyFile { get; set; } = "msbuildPropertyFile";
        public string ProjectPath { get; set; } = "projectPath";
        public string DebugPort { get; set; } = "debugPort";
        public string Serial { get; set; } = "serial";
        public string SkipDeploy { get; set; } = "skipDeploy";

        /// <summary>
        /// Default VSCode property keys.
        /// </summary>
        public static LaunchPropertyKeys VSCode => new LaunchPropertyKeys();

        /// <summary>
        /// Default Visual Studio property keys (same as VSCode for now).
        /// </summary>
        public static LaunchPropertyKeys VisualStudio => new LaunchPropertyKeys();
    }
}
