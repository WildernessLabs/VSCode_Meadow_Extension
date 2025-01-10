import * as vscode from 'vscode';
import * as path from 'path';
import { MsBuildProjectAnalyzer } from './msbuild-project-analyzer';
import { DeviceData, MeadowUtil } from "./meadow-util";
import * as extension from './extension';

let fs = require('fs');

export enum ProjectType
{
	Meadow,
	Unknown,
}

export class MSBuildProjectInfo {
	Configurations: string[];
	IsCore: Boolean;
	IsExe: boolean;
	LaunchJsonPath: string;
	Name: string;
	PackageReferences: string[];
	Path: string;
	Sdk: string;

	public static async fromProject(project: vscode.Uri): Promise<MSBuildProjectInfo> {
		var r = new MSBuildProjectInfo();

		var projXml = fs.readFileSync(project.fsPath);
		var msbpa = new MsBuildProjectAnalyzer(projXml);
		await msbpa.analyze();

		r.Path = project.fsPath;
		var projectDir = path.dirname(project.fsPath);
		r.LaunchJsonPath = path.join(projectDir, '.vscode', 'launch.json');
		r.Configurations = msbpa.getConfigurationNames();
		r.Name = this.getFilenameFromFullPath(project.fsPath).split('.')[0];
		r.PackageReferences = msbpa.getPackageReferences();
		r.Sdk = msbpa.getSdk();
		r.IsCore = r.Sdk !== undefined && r.Sdk !== null;
		return r;
	}

	public static getFilenameFromFullPath(fullpath: string) {
		const filename = path.basename(fullpath);
		return filename;
	}
}

export class MeadowStartupInfo {
	Project: MSBuildProjectInfo = undefined;
	Configuration: string = undefined;
	TargetFramework: string = undefined;
	Device: DeviceData = undefined;
	Devices: DeviceData[] = [];
	DebugPort: number = 5881;
}

export class MeadowProjectManager {
	
	static Shared: MeadowProjectManager;

	meadowDevices: DeviceData[];

	StartupInfo: MeadowStartupInfo = new MeadowStartupInfo();

	HasSupportedProjects: boolean;

	extensionContext: vscode.ExtensionContext;

	constructor(ctx: vscode.ExtensionContext) {
		MeadowProjectManager.Shared = this;

		this.extensionContext = ctx;
		this.StartupProjects = new Array<MSBuildProjectInfo>();

		vscode.workspace.findFiles('**/*.csproj').then(allProjFiles => this.parseProjects(allProjFiles));

		var watcher = vscode.workspace.createFileSystemWatcher('**/*.csproj', false, false, false);

		watcher.onDidChange(changedFileUri => this.parseProject(changedFileUri));
		watcher.onDidCreate(createdFileUri => this.parseProject(createdFileUri));
		watcher.onDidDelete(deletedFileUri => this.parseProject(deletedFileUri));
	}

	async parseProject(projectUri: vscode.Uri): Promise<void> {
		const projects:vscode.Uri[] = [ projectUri ];

		await this.parseProjects(projects);
	}

	async parseProjects(projectUris: vscode.Uri[]): Promise<void> {
		this.StartupProjects = new Array<MSBuildProjectInfo>();

		for (var p of projectUris) {
					
			var msbProjInfo = await MSBuildProjectInfo.fromProject(p);

			if (MeadowProjectManager.getIsSupportedProject(msbProjInfo)) {
				this.StartupProjects.push(msbProjInfo);
			}
		}

		// Determine if meadow projects found at all
		this.HasSupportedProjects = (this.StartupProjects?.length ?? 0) > 0;

		if (!this.StartupInfo)
			this.StartupInfo = new MeadowStartupInfo();

		if (!this.StartupInfo.Project || !this.StartupProjects.find(p => p.Path === this.StartupInfo.Project.Path))
		{
			this.StartupInfo.Project = undefined;
			this.StartupInfo.Configuration = undefined;
			this.StartupInfo.TargetFramework = undefined;
			this.StartupInfo.Device = undefined;
			this.StartupInfo.Devices = [];

			this.selectStartupProject(false);
		}

		this.setupMenus(this.HasSupportedProjects);
	}

	isMenuSetup: boolean = false;

	setupMenus(hasSupportedProjects: boolean)
	{
		if (!this.isMenuSetup)
		{
			this.extensionContext.subscriptions.push(vscode.commands.registerCommand("meadow.selectProject", () => this.selectStartupProject(true), this));
			this.projectStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
			this.projectStatusBarItem.command = "meadow.selectProject";

			this.extensionContext.subscriptions.push(vscode.commands.registerCommand("meadow.refreshDeviceList", MeadowProjectManager.refreshDeviceList, this));

			this.extensionContext.subscriptions.push(vscode.commands.registerCommand("meadow.toggleBuildConfiguration", this.toggleBuildConfiguration, this));
			this.buildConfigurationStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
			this.buildConfigurationStatusBarItem.command = "meadow.toggleBuildConfiguration";
			this.buildConfigurationStatusBarItem.text = "Toggle Build Configuration";

			this.isMenuSetup = true;
		}

		this.updateProjectStatus();
		MeadowProjectManager.refreshDeviceList();
		
		if (!hasSupportedProjects) {
			this.projectStatusBarItem.hide();
			this.buildConfigurationStatusBarItem.hide();
		} else {
			this.projectStatusBarItem.show();
			this.buildConfigurationStatusBarItem.show();
		}

		this.isMenuSetup = true;
	}

	projectStatusBarItem: vscode.StatusBarItem;
	buildConfigurationStatusBarItem: vscode.StatusBarItem;

	public StartupProjects = new Array<MSBuildProjectInfo>();

	public async selectStartupProject(interactive: boolean = false): Promise<any> {

		var availableProjects = this.StartupProjects;

		if (!availableProjects || availableProjects.length <= 0)
			return;

		var selectedProject = undefined;

		// Try and auto select some defaults
		if (availableProjects.length == 1)
			selectedProject = availableProjects[0];
		
		// If there are multiple projects and we allow interactive, show a picker
		if (!selectedProject && availableProjects.length > 0 && interactive)
		{
			var projects = availableProjects
			.map(x => ({
				label: x.Name,
				project: x,
			}));

			if (projects && projects.length > 0)
				selectedProject = (await vscode.window.showQuickPick(projects, { placeHolder: "Meadow Project" })).project;
		}

		// If we were interactive and didn't select a project, don't assume the first
		// if non interactive, it's ok to assume the first project
		if (!selectedProject && !interactive)
			selectedProject = availableProjects[0];
		
		if (!selectedProject)
			return;

		MeadowProjectManager.Shared.StartupInfo.Project = selectedProject;

		var defaultConfig = "Debug";

		var selectedConfiguration = undefined;

		if (selectedProject && selectedProject.Configurations)
		{
			if (selectedProject.Configurations.length > 0)
			{
				if (selectedProject.Configurations.length == 1)
				{
					selectedConfiguration = selectedProject.Configurations[0];
				}
				else
				{
					if (interactive)
					{
						var c = await vscode.window.showQuickPick(selectedProject.Configurations, { placeHolder: "Startup Project's Configuration" });

						if (c)
							selectedConfiguration = c;
					}
					else
					{
						if (selectedProject.Configurations.includes(defaultConfig))
							selectedConfiguration = defaultConfig;
						else
							selectedConfiguration = selectedProject.Configurations[0];
					}

				}
			}
			else
			{
				selectedConfiguration = defaultConfig;
			}
		}

		if (selectedConfiguration)
			MeadowProjectManager.Shared.StartupInfo.Configuration = selectedConfiguration;
	}

	public async updateProjectStatus() {
	
		var selProj = MeadowProjectManager.Shared.StartupInfo?.Project;

		this.projectStatusBarItem.tooltip = selProj === undefined ? "Select a Meadow Project" : selProj.Path;
		this.updateConfigurationStatus(selProj);
	}

	public async showDevicePicker(showPicker: boolean = true): Promise<void> {
		if (this.HasSupportedProjects === false) {
			return;
		}

		if (MeadowProjectManager.Shared?.StartupInfo?.Project === undefined) {
			await vscode.window.showInformationMessage("Select a Meadow Project first.");
			return;
		}

		var util = new MeadowUtil();

		MeadowProjectManager.Shared.meadowDevices = [];

		await vscode.window.withProgress({
			location: vscode.ProgressLocation.Notification,
			cancellable: false,
			title: 'Loading Meadow Devices'
		}, async (progress) => {
			
			progress.report({  increment: 0 });
			MeadowProjectManager.Shared.meadowDevices = await util.GetDevices();
			progress.report({ increment: 100 });
		});

		if (MeadowProjectManager.Shared.meadowDevices.length > 1) {
			if (showPicker) {
				var pickerDevices = MeadowProjectManager.Shared.meadowDevices
					.map(x => ({
						//description: x.type.toString(),
						label: x.name,
						device: x,
					}));

				const p = await vscode.window.showQuickPick(pickerDevices, { placeHolder: "Select a Device" });
				if (p) {
					MeadowProjectManager.Shared.StartupInfo.Device = p.device;
				}
			}
		}
		else {
			MeadowProjectManager.Shared.StartupInfo.Device = MeadowProjectManager.Shared.meadowDevices[0];
		}
	}

	public async updateConfigurationStatus(selProj: MSBuildProjectInfo) {
		const currentConfig = await this.extensionContext.workspaceState.get('csharpBuildConfiguration', 'Debug');

		//var selConfig = MeadowProjectManager.Shared.StartupInfo?.Configuration;

		var projStr = "Meadow Project";
		if (selProj)
		{
			projStr = selProj.Name ?? selProj.Name ?? "Meadow Project";

			if (currentConfig)
				projStr += " | " + currentConfig;
		}

		this.projectStatusBarItem.text = "$(project) " + projStr;
	}

	public static getIsSupportedProject(projectInfo: MSBuildProjectInfo): boolean
	{
		if ((projectInfo?.Sdk?.toLowerCase()?.indexOf("meadow") ?? -1) >= 0)
			return true;

		var numMeadowPkgs = projectInfo.PackageReferences.filter(pr => 
			pr.toLowerCase() === "meadow.sdk"
			|| pr.toLowerCase() === "meadow.foundation").length;

		if (numMeadowPkgs && numMeadowPkgs > 0)
			return true;

		return false;
	}

	static launchConfiguration: vscode.WorkspaceConfiguration;
	static savedConfigurations: vscode.WorkspaceConfiguration[];

	public static async refreshDeviceList(): Promise<void> {

		await MeadowProjectManager.Shared.showDevicePicker(false);

		this.launchConfiguration = vscode.workspace.getConfiguration('launch');
		var configurations = this.launchConfiguration.get('configurations', []);
		if (this.savedConfigurations === undefined) {
			if (configurations != undefined) {
				this.savedConfigurations = configurations;
			}
		}

		configurations = [];
		MeadowProjectManager.Shared.meadowDevices.forEach(item => {
			// check if device is already part of the list before adding it
			if (!configurations.some(c => c["name"] === item.name)) {
				// Add newly found device to list.
				configurations.push({
					"name": item.name,
					"type": "meadow",
					"request": "launch",
					"preLaunchTask": "meadow: Build",
					"deviceInfo": item
				});
			}
		});

		if (configurations.length > 0) {
			this.launchConfiguration.update('configurations', configurations, false).then(() =>
				vscode.window.showInformationMessage('Device List Updated!')
			);
		}
	}

	public static async resetLaunchConfigurations() {
		this.launchConfiguration.update('configurations', this.savedConfigurations, false);
	}

	public async toggleBuildConfiguration()
	{
        const currentConfig = await this.extensionContext.workspaceState.get('csharpBuildConfiguration', 'Debug');

        // Toggle the build configuration and update the setting
		const newConfig = currentConfig === 'Debug' ? 'Release' : 'Debug';
		await this.extensionContext.workspaceState.update('csharpBuildConfiguration', newConfig);

		this.updateConfigurationStatus(MeadowProjectManager.Shared.StartupInfo?.Project);
	}
}