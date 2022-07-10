import * as vscode from 'vscode';
import { WorkspaceFolder, DebugConfiguration } from 'vscode';
import { MeadowProjectManager, ProjectType } from './meadow-project-manager';

export class MeadowConfiguration implements DebugConfiguration {
	[key: string]: any;
	type: string;
	name: string;
	request: string;
}

export class MeadowConfigurationProvider implements vscode.DebugConfigurationProvider {

	constructor() {
	}



	provideDebugConfigurations?(folder: WorkspaceFolder | undefined, token?: vscode.CancellationToken): vscode.ProviderResult<DebugConfiguration[]> {
		return [
			{
				name: "Deploy",
				type: "meadow",
				request: "launch",
				preLaunchTask: "meadow: Build"
			}
		];
	}


	async resolveDebugConfiguration(folder: WorkspaceFolder | undefined, config: DebugConfiguration, token?: vscode.CancellationToken): Promise<DebugConfiguration> {

		if (!MeadowProjectManager.Shared.HasSupportedProjects)
			return null;

		var startupInfo = MeadowProjectManager.Shared.StartupInfo;

		if (!config.request)
			config.request = 'launch';

		if (!config.name)
			config.name = 'Deploy';

		if (!config.type)
			config.type = 'meadow';

		var project = startupInfo.Project;

		if (!project)
		{
			await MeadowProjectManager.Shared.selectStartupProject();
			project = startupInfo.Project;
		}

		if (!project) {
			vscode.window.showErrorMessage("Startup Project not selected!");
			return undefined;
		}

		startupInfo = MeadowProjectManager.Shared.StartupInfo;

		if (project) {

			if (!config['projectPath'])
				config['projectPath'] = project.Path;

			if (!config['projectOutputPath'])
				config['projectOutputPath'] = project.OutputPath;

			if (!config['projectConfiguration'])
				config['projectConfiguration'] = startupInfo.Configuration;

			var projectIsCore = startupInfo.Project.IsCore;

			config['projectIsCore'] = projectIsCore;
			config['projectTargetFramework'] = startupInfo.TargetFramework;

			// Only set the debug port for debug config
			if (startupInfo.Configuration.toLowerCase() === 'debug')
				config['debugPort'] = startupInfo.DebugPort;

			config['serial'] = config['name'];

			/*var device = startupInfo.Device;

			if (!device)
			{ 
				await MeadowProjectManager.Shared.showDevicePicker();
				device = startupInfo.Device;
			}

			if (!device) {
				vscode.window.showErrorMessage("Device not selected!");
				return undefined;
			}

			if (device && device.serial) {
				config['serial'] = device.name;
			}*/
		}

		return config;
	}

	resolveDebugConfigurationWithSubstitutedVariables?(folder: WorkspaceFolder | undefined, debugConfiguration: DebugConfiguration, token?: vscode.CancellationToken): vscode.ProviderResult<DebugConfiguration>
	{
		return debugConfiguration;
	}
}
