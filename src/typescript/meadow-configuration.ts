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

	async resolveDebugConfiguration(folder: WorkspaceFolder | undefined, config: DebugConfiguration, token?: vscode.CancellationToken): Promise<DebugConfiguration> {

		// No launch.json exists, let's help fill out a nice default
		if (!config.type)
			config.type = 'meadow';

		if (!config.request)
			config.request = 'launch';

		if (!config.name)
			config.name = 'Deploy';

		// if launch.json is missing or empty
		if (config.type == 'meadow') {

			var project = MeadowProjectManager.SelectedProject;

			if (!project)
			{
				await MeadowProjectManager.Shared.showProjectPicker();
				project = MeadowProjectManager.SelectedProject;
			}

			if (!project) {
				vscode.window.showErrorMessage("Startup Project not selected!");
				return undefined;
			}

			if (project) {

				if (!config['projectPath'])
					config['projectPath'] = project.Path;

				if (!config['projectOutputPath'])
					config['projectOutputPath'] = project.OutputPath;

				if (!config['projectConfiguration'])
					config['projectConfiguration'] = MeadowProjectManager.SelectedProjectConfiguration;

				var projectIsCore = MeadowProjectManager.SelectedProject.IsCore;

				config['projectIsCore'] = projectIsCore;
				config['projectTargetFramework'] = MeadowProjectManager.SelectedTargetFramework;

				config['debugPort'] = MeadowProjectManager.DebugPort;

				var device = MeadowProjectManager.SelectedDevice;

				if (!device)
				{ 
					await MeadowProjectManager.Shared.showDevicePicker();
					device = MeadowProjectManager.SelectedDevice;
				}

				if (!device) {
					vscode.window.showErrorMessage("Device not selected!");
					return undefined;
				}

				if (device && device.serial) {
					config['serial'] = device.serial;
				}
			}
		}

		return config;
	}
}
