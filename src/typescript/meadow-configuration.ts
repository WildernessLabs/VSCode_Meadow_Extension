import * as vscode from 'vscode';
import { WorkspaceFolder, DebugConfiguration } from 'vscode';
import { MeadowProjectManager, ProjectType } from './meadow-project-manager';
import { getTempFile } from './meadow-util';

export class MeadowConfiguration implements DebugConfiguration {
	[key: string]: any;
	type: string;
	name: string;
	request: string;

	VSCodeMeadowDebugInfoFile: string;
}

export class MeadowConfigurationProvider implements vscode.DebugConfigurationProvider {

	constructor(private extensionContext: vscode.ExtensionContext) {
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

		if (!config.type)
			config.type = 'meadow';

		if (!config['preLaunchTask']) {
			config['preLaunchTask'] = 'meadow: Build';
		}

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

		// TODO startupInfo = MeadowProjectManager.Shared.StartupInfo;

		if (project) {
			if (!config['msbuildPropertyFile'])
				config['msbuildPropertyFile'] = getTempFile();
				
			if (!config['projectPath'])
				config['projectPath'] = project.Path;

			const currentBuildConfiguration = await this.extensionContext.workspaceState.get('csharpBuildConfiguration', 'Debug')
			if (!config['projectConfiguration'])
				config['projectConfiguration'] = currentBuildConfiguration;

			// Only set the debug port for debug config
			if (currentBuildConfiguration.toLowerCase() === 'debug')
				config['debugPort'] = startupInfo.DebugPort;

			var device = config['deviceInfo'];

			if (!device)
			{ 
				await MeadowProjectManager.Shared.showDevicePicker();
				device = startupInfo.Device;
			}

			if (!device) {
				vscode.window.showErrorMessage("Device not selected or attached!");
				return undefined;
			}

			if (device && device.serial) {
				config['serial'] = device.serial;
			}

			if (!config.name) {
				if (device.name)
					config.name = device.name;
				else
					config.name = 'Deploy';
			}
		}

		this.extensionContext.workspaceState.update('currentDebugConfiguration', config)

		return config;
	}

	resolveDebugConfigurationWithSubstitutedVariables?(folder: WorkspaceFolder | undefined, debugConfiguration: DebugConfiguration, token?: vscode.CancellationToken): vscode.ProviderResult<DebugConfiguration>
	{
		return debugConfiguration;
	}
}