'use strict';
let fs = require("fs");
let path = require('path')
import * as vscode from 'vscode';
import { MeadowProjectManager as MeadowProjectManager, ProjectType } from './meadow-project-manager';
import { MeadowConfiguration } from './meadow-configuration';

interface MeadowBuildTaskDefinition extends vscode.TaskDefinition {

	task: string;

	command:string;

	csproj:string;

	configuration:string;

	target:string;

	flags?: string[];

	VSCodeMeadowDebugInfoFile?: string;
}

export class MeadowBuildTaskProvider implements vscode.TaskProvider {
	static MeadowBuildScriptType: string = 'meadow';

	constructor(private extensionContext: vscode.ExtensionContext){

	}

	public async provideTasks(): Promise<vscode.Task[]> {
		return this.getTasks();
	}

	public resolveTask(_task: vscode.Task): vscode.Task | undefined {
		// All supported tasks are provided by getTasks. If that changes, the easiest
		// thing to do might be to move msbuild location into util, and launch util directly.
		return undefined;
	}

	private async getTasks(): Promise<vscode.Task[]> {
		var startupInfo = MeadowProjectManager.Shared.StartupInfo;

		if (!startupInfo.Project)
		{
			vscode.window.showInformationMessage("Startup Project not selected!");
			return undefined;
		}

		var flags = [];
		var command = "dotnet";

		return [
			this.getTask(command, "Build", flags),
		]
	}

	private getTask(command:string, target: string, flags: string[], definition?: MeadowBuildTaskDefinition): vscode.Task{

		var debugConfig = this.extensionContext.workspaceState.get('currentDebugConfiguration') as MeadowConfiguration
		// Clear out the set debug info for the next time this provider is called
		// which may not be for a debug session
		this.extensionContext.workspaceState.update('currentDebugConfiguration', undefined)

		const configuration = this.extensionContext.workspaceState.get('csharpBuildConfiguration', 'Debug');

		var debugConfig = this.extensionContext.workspaceState.get('currentDebugConfiguration') as MeadowConfiguration
		// Clear out the set debug info for the next time this provider is called
		// which may not be for a debug session
		this.extensionContext.workspaceState.update('currentDebugConfiguration', undefined)

		var startupInfo = MeadowProjectManager.Shared.StartupInfo;

		var configuration = startupInfo.Configuration ?? 'Debug';
		var csproj = startupInfo.Project.Path;
		var device = startupInfo.Device;

		if (definition === undefined) {
			definition = {
				task: "MSBuild",
				command,
				type: MeadowBuildTaskProvider.MeadowBuildScriptType,
				csproj,
				configuration,
				target,
				flags
			};
		}

		var args = [`-t:${target}`, `-p:Configuration=${configuration}`];

		// dotnet needs the build verb
		args.unshift("build");

		const meadowDebugTargetsPath = path.join(this.extensionContext.extensionPath, 'src', 'Meadow.Debug.targets')
		args.push(`-p:CustomAfterMicrosoftCSharpTargets="${meadowDebugTargetsPath}"`)
		args.push(`-p:VSCodeMeadowDebugInfoFile=${debugConfig['msbuildPropertyFile']}`)

		args.push(csproj);

		args.concat(flags);
		var task = new vscode.Task(definition, vscode.TaskScope.Workspace, definition.target, 'meadow', new vscode.ProcessExecution(command, args), "$msCompile");
		return task;
	}
}