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
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;

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

			var ports = MeadowDeviceManager.GetSerialPorts() ?? new List<string>();

			return ports.Select(p => new DeviceData { Name = p, Serial = p });
		}
	}
}
