using System;
using System.Collections.Generic;
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
		public string AppName { get; set; } = "";
		public string Project { get; set; }
		public string Configuration { get; set; }
		public string ProjectTargetFramework { get; set; }
		public bool ProjectIsCore { get; set; }
		public string OutputDirectory { get; set; }
		public int DebugPort { get; set; }

		public string Serial { get;set; }

		public LaunchData ()
		{

		}
		public LaunchData(dynamic args)
		{
			Project = getString (args, VSCodeKeys.LaunchConfig.ProjectPath);
			Configuration = getString (args, VSCodeKeys.LaunchConfig.Configuration, "Debug");
			//Platform = getString (args, VSCodeKeys.LaunchConfig.Platform, "AnyCPU");
			OutputDirectory = cleanseStringPaths(getString (args, VSCodeKeys.LaunchConfig.Output));
			ProjectTargetFramework = getString(args, VSCodeKeys.LaunchConfig.ProjectTargetFramework);
			ProjectIsCore = getBool(args, VSCodeKeys.LaunchConfig.ProjectIsCore, false);
			DebugPort = getInt(args, VSCodeKeys.LaunchConfig.DebugPort, 55555);
			Serial = getString(args, VSCodeKeys.LaunchConfig.Serial);
		}

		public (bool success, string message) Validate ()
		{
			(bool success, string message) validateString (string value, string name)
				=> string.IsNullOrWhiteSpace(value) ? (false, $"{name} is not valid") : (true, "");
			var checks = new[] {
				validateString(Project,nameof(Project)),
				validateString(Configuration,nameof(Configuration)),
				validateString(OutputDirectory,nameof(OutputDirectory)),
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

	}
}
