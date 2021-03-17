'use strict';
let fs = require("fs");
let path = require('path')
import * as vscode from 'vscode';
import { MeadowProjectManager as MeadowProjectManager, ProjectType } from './meadow-project-manager';
import * as child from 'child_process';

interface ExecResult {
    stdout: string,
    stderr: string
}

function execFileAsync(file: string, args?: string[]): Thenable<ExecResult> {

    return new Promise((resolve, reject) => {
        child.execFile(file, args, (error, stdout, stderr) => {
            if (error) {
                reject(error);
            }
            resolve({ stdout, stderr });
        });
    });
}

function existsAsync(path: string | Buffer): Promise<boolean> {

    return new Promise((resolve) => fs.exists(path, resolve));
}

interface MeadowBuildTaskDefinition extends vscode.TaskDefinition {

	task: string;

	command:string;

	csproj:string;

	configuration:string;

	target:string;

	flags?: string[];
}

export class MeadowBuildTaskProvider implements vscode.TaskProvider {
	static MeadowBuildScriptType: string = 'meadow';
	private csproj:string;
	private configuration:string;
	
	// We use a CustomExecution task when state needs to be shared across runs of the task or when 
	// the task requires use of some VS Code API to run.
	// If you don't need to share state between runs and if you don't need to execute VS Code API in your task, 
	// then a simple ShellExecution or ProcessExecution should be enough.
	// Since our build has this shared state, the CustomExecution is used below.
	private sharedState: string | undefined;

	constructor(private workspaceRoot: string){
		console.log(workspaceRoot);
	}

	public async provideTasks(): Promise<vscode.Task[]> {
		return this.getTasks();
	}

	public resolveTask(_task: vscode.Task): vscode.Task | undefined {
		// All supported tasks are provided by getTasks. If that changes, the easiest
		// thing to do might be to move msbuild location into util, and launch util directly.
		return undefined;
	}

	static msBuildPromise:Promise<string>;

	private static locateMSBuild(): Promise<string> {

		if (!MeadowBuildTaskProvider.msBuildPromise) {
			MeadowBuildTaskProvider.msBuildPromise = MeadowBuildTaskProvider.locateMSBuildImpl();
		}
		return MeadowBuildTaskProvider.msBuildPromise;
	}

	private static async locateMSBuildImpl(): Promise<string> {

		if (process.platform !== "win32") {
			return "msbuild";
		}
	
		const progFiles = process.env["ProgramFiles(x86)"];
		const vswhere = path.join(progFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe");
		if (!await existsAsync(vswhere)) {
			return "msbuild";
		}
	
		var findMSBuild = ["-latest", "-requires", "Microsoft.Component.MSBuild", "-find", "MSBuild\\**\\Bin\\MSBuild.exe"];
		var { stdout } = await execFileAsync(vswhere, findMSBuild);
		stdout = stdout.trim();
		if (stdout.length > 0) {
			return stdout;
		}
	
		findMSBuild.push("-preRelease");
		var { stdout } = await execFileAsync(vswhere, findMSBuild);
		stdout = stdout.trim();
		if (stdout.length > 0) {
			return stdout;
		} else {
			return "msbuild";
		}
	}

	private async getTasks(): Promise<vscode.Task[]> {
		
		var startupInfo = MeadowProjectManager.Shared.StartupInfo;

		if (!startupInfo.Project)
		{
			vscode.window.showInformationMessage("Startup Project not selected!");
			return undefined;
		}

		this.csproj = startupInfo.Project.Path;
		this.configuration = startupInfo.Configuration;

		var flags = [];
		var command = "dotnet";
		
		//TODO: Eventually can support dotnet core and use dotnet build
		// Use MSBuild for old projects
		//if (!MeadowProjectManager.SelectedProject.IsCore)
		command = await MeadowBuildTaskProvider.locateMSBuild();

		return [
			this.getTask(command, "Build", flags)
		]
	}

	private getTask(command:string ,target: string, flags: string[], definition?: MeadowBuildTaskDefinition): vscode.Task{

		var startupInfo = MeadowProjectManager.Shared.StartupInfo;

		var configuration = startupInfo.Configuration;
		var csproj = startupInfo.Project.Path;
		var isCore = startupInfo.Project.IsCore;
		var tfm = startupInfo.TargetFramework;
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

		var args = [csproj, `-r`, `-t:${target}`, `-p:Configuration=${configuration}`];

		// dotnet needs the build verb
		// if (isCore) {
		// 	args.unshift("build");
		// 	if (tfm)
		// 		args.push(`-p:TargetFramework=${tfm}`);
		// }

		args.concat(flags);
		var task = new vscode.Task(definition, definition.target, 'meadow', new vscode.ProcessExecution(command, args),
			"$msCompile");
		return task;
	}
}