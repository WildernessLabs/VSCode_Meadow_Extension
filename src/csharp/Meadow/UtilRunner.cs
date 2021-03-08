using MeadowCLI.DeviceManagement;
using Mono.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using VSCodeDebug;

namespace VsCodeMeadowUtil
{
	class UtilRunner
	{
		const string helpCommand = "help";

		public static void UtilMain(string[] args)
		{
			var options = new OptionSet();

			var command = helpCommand;
			var id = Guid.NewGuid().ToString();

			options.Add("c|command=", "get the tool version", s => command = s?.ToLowerInvariant()?.Trim() ?? helpCommand);
			options.Add("h|help", "prints the help", s => command = helpCommand);
			options.Add("i|id=", "unique identifier of the command", s => id = s);
			
			var extras = options.Parse(args);

			if (command.Equals(helpCommand))
			{
				ShowHelp(options);
				return;
			}

			var response = new CommandResponse
			{
				Id = id,
				Command = command
			};

			object responseObject = null;

			try
			{
				responseObject = command switch
				{
					"version" => Version(),
					"devices" => AllDevices(),
					_ => Version()
				};
			}
			catch (Exception ex)
			{
				response.Error = ex.Message;
			}

			response.Response = responseObject;

			var json = Newtonsoft.Json.JsonConvert.SerializeObject(response,
				Newtonsoft.Json.Formatting.None, new Newtonsoft.Json.JsonSerializerSettings
				{
					Formatting = Newtonsoft.Json.Formatting.None,
					NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
				});
			
			Console.WriteLine(json);
		}

		static void ShowHelp(OptionSet p)
		{
			Console.WriteLine("Usage: vscode-meadow [OPTIONS]+");
			Console.WriteLine();
			Console.WriteLine("Options:");
			p.WriteOptionDescriptions(Console.Out);
		}

		static object Version()
			=> new { Version = "0.1.0" };

		static IEnumerable<DeviceData> AllDevices()
		{
			var devices = new List<DeviceData>();

			if (ShellProcessRunner.IsWindows)
				return System.IO.Ports.SerialPort.GetPortNames().Select(p => new DeviceData { Name = p, Serial = p });

			return GetMeadowSerialPortsMac().Select(p => new DeviceData { Name = p, Serial = p });
		}


		static List<string> GetMeadowSerialPortsMac()
		{
			var ports = new List<string>();

			var psi = new ProcessStartInfo
			{
				FileName = "/usr/sbin/ioreg",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				Arguments = "-r -c IOUSBHostDevice -l"
			};

			string output = string.Empty;

			using (var p = Process.Start(psi))
			{
				if (p != null)
				{
					output = p.StandardOutput.ReadToEnd();
					p.WaitForExit();
				}
			}

			//split into lines
			var lines = output.Split("\n\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

			bool foundMeadow = false;
			for (int i = 0; i < lines.Length; i++)
			{
				//first level devices 
				if (lines[i].IndexOf("+-o") == 0)
				{
					//we reset here so we don't read a serial port name for a non-Meadow device
					foundMeadow = false;
					if (lines[i].Contains("Meadow"))
					{
						//found a meadow device
						foundMeadow = true;
					}
				}

				//now find the IODialinDevice entry which contains the serial port name
				if (foundMeadow && lines[i].Contains("IODialinDevice"))
				{
					int startIndex = lines[i].IndexOf("/");
					int endIndex = lines[i].IndexOf("\"", startIndex + 1);
					var port = lines[i].Substring(startIndex, endIndex - startIndex);

					ports.Add(port);
					foundMeadow = false;
				}
			}
			return ports;
		}
	}
}
