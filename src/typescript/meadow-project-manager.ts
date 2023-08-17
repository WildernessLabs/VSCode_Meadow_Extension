import * as vscode from 'vscode';
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
	public static async fromProject(project: vscode.Uri): Promise<MSBuildProjectInfo> {
		var r = new MSBuildProjectInfo();

		var projXml = fs.readFileSync(project.fsPath);
		var msbpa = new MsBuildProjectAnalyzer(projXml);
		await msbpa.analyze();

		r.Path = project.fsPath;
		r.Configurations = msbpa.getConfigurationNames();
		r.Name = project.toString().split('/').pop().split('.')[0];
		r.PackageReferences = msbpa.getPackageReferences();
		r.Sdk = msbpa.getSdk();
		r.IsCore = r.Sdk !== undefined && r.Sdk !== null;
		return r;
	}

	Name: string;
	Path: string;
	IsExe: boolean;
	Sdk: string;
	Configurations: string[];
	PackageReferences: string[];
	IsCore: Boolean;
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

	StartupInfo: MeadowStartupInfo = new MeadowStartupInfo();

	HasSupportedProjects: boolean;

	context: vscode.ExtensionContext;

	constructor(ctx: vscode.ExtensionContext) {
		MeadowProjectManager.Shared = this;
		
		this.context = ctx;
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
			this.context.subscriptions.push(vscode.commands.registerCommand("meadow.selectProject", () => this.selectStartupProject(true), this));
			this.context.subscriptions.push(vscode.commands.registerCommand("meadow.selectDevice", this.showDevicePicker, this));
			this.projectStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
			this.projectStatusBarItem.command = "meadow.selectProject";
			this.deviceStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
			this.deviceStatusBarItem.command = "meadow.selectDevice";

			this.isMenuSetup = true;
		}

		this.updateProjectStatus();
		this.updateDeviceStatus();

		
		if (!hasSupportedProjects) {
			this.deviceStatusBarItem.hide();
			this.projectStatusBarItem.hide();
		} else {
			this.deviceStatusBarItem.show();
			this.projectStatusBarItem.show();
		}

		this.isMenuSetup = true;
	}

	projectStatusBarItem: vscode.StatusBarItem;
	deviceStatusBarItem: vscode.StatusBarItem;

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
		var selConfig = MeadowProjectManager.Shared.StartupInfo?.Configuration;

		var projStr = "Meadow Project";
		if (selProj)
		{
			projStr = selProj.Name ?? selProj.Name ?? "Meadow Project";

			if (selConfig)
				projStr += " | " + selConfig;
		}

		this.projectStatusBarItem.text = "$(project) " + projStr;
		this.projectStatusBarItem.tooltip = selProj === undefined ? "Select a Meadow Project" : selProj.Path;
	}


	public async showDevicePicker(): Promise<void> {

		if (MeadowProjectManager.Shared?.StartupInfo?.Project === undefined) {
			await vscode.window.showInformationMessage("Select a Meadow Project first.");
			return;
		}

		var util = new MeadowUtil();


		var meadowDevices : DeviceData[] = [];

		await vscode.window.withProgress({
			location: vscode.ProgressLocation.Notification,
			cancellable: false,
			title: 'Loading Meadow Devices'
		}, async (progress) => {
			
			progress.report({  increment: 0 });
			meadowDevices = await util.GetDevices();
			progress.report({ increment: 100 });
		});

		var pickerDevices = meadowDevices
			.map(x => ({
				//description: x.type.toString(),
				label: x.name,
				device: x,
			}));

		const p = await vscode.window.showQuickPick(pickerDevices, { placeHolder: "Select a Device" });
		if (p) {
			MeadowProjectManager.Shared.StartupInfo.Device = p.device;
		}
		

		this.updateDeviceStatus();
	}

	public async updateDeviceStatus() {
		
		var selDevice = MeadowProjectManager.Shared?.StartupInfo?.Device;

		var deviceStr = "Meadow Device";
		
		if (selDevice && selDevice?.name)
			deviceStr = selDevice.name;
		
		this.deviceStatusBarItem.text = "$(device-mobile) " + deviceStr;
		if (extension.isMacOS) {
			this.deviceStatusBarItem.tooltip = "Select Device - Cmd+Alt+Shift+R";
		}
		else{
			this.deviceStatusBarItem.tooltip = "Select Device - Ctrl+Alt+Shift+R";
		}
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
}