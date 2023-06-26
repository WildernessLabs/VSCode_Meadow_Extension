using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using VSCodeDebug;

namespace VsCodeMeadowUtil
{
	public class DeviceData
	{
		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("serial")]
		public string Serial { get; set; }

		[JsonProperty("platform")]
		public string Platform { get; set; }

		[JsonProperty("version")]
		public string Version { get; set; }
	}

	public class SimpleResult
	{
		[JsonProperty("success")]
		public bool Success { get; set; }
	}
	public enum ProjectType {
		Meadow,
		Unknown,
	}

	public class LaunchData {
		
		public string ProjectPath { get; set; }
		public string ProjectConfiguration { get; set; }
		public string MSBuildPropertyFile { get; set; }

		public int DebugPort { get; set; } = -1;

		public string Serial { get;set; }

		public IReadOnlyDictionary<string, string> MSBuildProperties { get; set; }

		public LaunchData ()
		{
		}

		public LaunchData(dynamic args)
		{
			ProjectPath = getString (args, VSCodeKeys.LaunchConfig.ProjectPath);
			ProjectConfiguration = getString (args, VSCodeKeys.LaunchConfig.Configuration, "Debug");
			DebugPort = getInt(args, VSCodeKeys.LaunchConfig.DebugPort, 55555);
			Serial = getString(args, VSCodeKeys.LaunchConfig.Serial);
			MSBuildPropertyFile = cleanseStringPaths(getString(args, VSCodeKeys.LaunchConfig.MSBuildPropertyFile));
		}

		private Dictionary<string, string> debugInfoProps = new Dictionary<string, string>();

		public string GetBuildProperty(string propertyName, string defaultValue = null)
		{
			if (debugInfoProps.Count <= 0)
			{
				ParseDebugInfoPropsFile();
			}

			if (debugInfoProps.TryGetValue(propertyName.ToLowerInvariant(), out var value))
			{
				return value;
			}

			return defaultValue;
		}

		void ParseDebugInfoPropsFile()
		{
			if (File.Exists(MSBuildPropertyFile))
			{
				var lines = File.ReadAllLines(MSBuildPropertyFile);
				foreach (var line in lines)
				{
					var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
					if (parts.Length == 2)
						debugInfoProps[parts[0].ToLowerInvariant()] = parts[1];
				}
			}
		}

		public (bool success, string message) Validate ()
		{
			(bool success, string message) validateString (string value, string name)
				=> string.IsNullOrWhiteSpace(value) ? (false, $"{name} is not valid") : (true, "");
			var checks = new[] {
				validateString(ProjectPath,nameof(ProjectPath)),
				validateString(ProjectConfiguration,nameof(ProjectConfiguration)),
				validateString(Serial, nameof(Serial)),
				validateString(MSBuildPropertyFile,nameof(MSBuildPropertyFile)),
			};
			foreach(var check in checks) {
				if (!check.success)
					return check;
			}
			
			if (string.IsNullOrWhiteSpace (Serial))
				return (false, "Meadow Serial is not valid");
		
			return (true, "");
		}

		static string cleanseStringPaths(string path)
		{
			if (ShellProcessRunner.IsWindows)
				return path;
			return path.Replace ("\\", "/");
		}

		private static bool getBool (dynamic container, string propertyName, bool dflt = false)
		{
			try {
				return (bool)container [propertyName];
			} catch (Exception) {
				// ignore and return default value
			}
			return dflt;
		}

		private static int getInt (dynamic container, string propertyName, int dflt = 0)
		{
			try {
				return (int)container [propertyName];
			} catch (Exception) {
				// ignore and return default value
			}
			return dflt;
		}

		private static string getString (dynamic args, string property, string dflt = null)
		{
			var s = (string)args [property];
			if (s == null) {
				return dflt;
			}
			s = s.Trim ();
			if (s.Length == 0) {
				return dflt;
			}
			return s;
		}

		private static IReadOnlyDictionary<string, string> getDictionary(dynamic container, string propertyName)
		{
			try
			{
				var c = container[propertyName];
				var d = new Dictionary<string, string>();
				foreach (var propertyDescriptor in System.ComponentModel.TypeDescriptor.GetProperties(c))
				{
					string obj = propertyDescriptor?.GetValue(c);
					if (!string.IsNullOrEmpty(obj))
						d.Add(propertyDescriptor.Name, obj);
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
}
