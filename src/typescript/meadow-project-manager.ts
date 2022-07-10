import { isDotNetCoreProject, MSBuildProject, TargetFramework } from "./omnisharp/protocol";
import * as vscode from 'vscode';
import { BaseEvent, WorkspaceInformationUpdated } from './omnisharp/loggingEvents';
import { EventType } from './omnisharp/EventType';
import { MsBuildProjectAnalyzer } from './msbuild-project-analyzer';
import { DeviceData, MeadowUtil } from "./meadow-util";

let fs = require('fs');

export enum ProjectType
{
	Meadow,
	Unknown,
}

export class MSBuildProjectInfo implements MSBuildProject {
	public static async fromProject(project: MSBuildProject): Promise<MSBuildProjectInfo> {
		var r = new MSBuildProjectInfo();

		r.ProjectGuid = project.ProjectGuid;
		r.Path = project.Path;
		r.AssemblyName = project.AssemblyName;
		r.TargetPath = project.TargetPath;
		r.TargetFramework = project.TargetFramework;
		r.SourceFiles = project.SourceFiles;
		r.TargetFrameworks = project.TargetFrameworks;
		r.OutputPath = project.OutputPath;
		r.IsExe = project.IsExe;
		r.IsUnityProject = project.IsUnityProject;
		r.IsBlazorWebAssemblyHosted = project.IsBlazorWebAssemblyHosted;
		r.IsBlazorWebAssemblyStandalone = project.IsBlazorWebAssemblyStandalone;
		r.IsWebProject = project.IsWebProject;

		var projXml = fs.readFileSync(r.Path);
		var msbpa = new MsBuildProjectAnalyzer(projXml);
		await msbpa.analyze();

		r.Configurations = msbpa.getConfigurationNames();
		r.Platforms = msbpa.getPlatformNames();
		r.Name = msbpa.getProjectName();
		r.PackageReferences = msbpa.getPackageReferences();
		r.Sdk = msbpa.getSdk();
		r.IsCore = isDotNetCoreProject(project);
		return r;
	}

	Name: string;
	ProjectGuid: string;
	Path: string;
	AssemblyName: string;
	TargetPath: string;
	TargetFramework: string;
	SourceFiles: string[];
	TargetFrameworks: TargetFramework[];
	OutputPath: string;
	IsExe: boolean;
	IsUnityProject: boolean;
	Sdk: string;
	Configurations: string[];
	Platforms: string[];
	PackageReferences: string[];
	IsBlazorWebAssemblyStandalone: boolean;
	IsWebProject: boolean;
	IsBlazorWebAssemblyHosted: boolean;
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

	omnisharp: any;

	context: vscode.ExtensionContext;

	constructor(ctx: vscode.ExtensionContext) {
		MeadowProjectManager.Shared = this;
		
		this.context = ctx;

		this.omnisharp = vscode.extensions.getExtension("ms-dotnettools.csharp").exports;

		this.omnisharp.eventStream.subscribe(async (e: BaseEvent) => {
			if (e.type === EventType.WorkspaceInformationUpdated) {

				this.StartupProjects = new Array<MSBuildProjectInfo>();

				for (var p of (<WorkspaceInformationUpdated>e).info.MsBuild.Projects) {
					
					var msbProjInfo = await MSBuildProjectInfo.fromProject(p);

					if (MeadowProjectManager.getIsSupportedProject(msbProjInfo)) {
						this.StartupProjects.push(await MSBuildProjectInfo.fromProject(p));
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

				this.setupMenus();

				this.updateProjectStatus();
				MeadowProjectManager.refreshDeviceList();
				//this.updateDeviceStatus();
			}
		});
	}

	isMenuSetup: boolean = false;

	setupMenus()
	{
		if (!this.isMenuSetup)
		{
			this.context.subscriptions.push(vscode.commands.registerCommand("meadow.selectProject", () => this.selectStartupProject(true), this));
			//this.context.subscriptions.push(vscode.commands.registerCommand("meadow.selectDevice", this.showDevicePicker, this));
			this.context.subscriptions.push(vscode.commands.registerCommand("meadow.refreshDeviceList", MeadowProjectManager.refreshDeviceList, this));
			this.projectStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
			this.projectStatusBarItem.command = "meadow.selectProject";
			//this.deviceStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
			//this.deviceStatusBarItem.command = "meadow.selectDevice";

			this.isMenuSetup = true;
		}

		this.updateProjectStatus();
		//MeadowProjectManager.refreshDeviceList();
		//this.updateDeviceStatus();

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
				label: x.AssemblyName,
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

		var selectedTargetFramework = undefined;

		if (selectedProject.TargetFrameworks && selectedProject.TargetFrameworks.length > 0)
		{
			// If just 1, use it without asking
			if (selectedProject.TargetFrameworks.length == 1)
			{
				selectedTargetFramework = this.fixTfm(selectedProject.TargetFrameworks[0].ShortName);
			}
			else
			{
				// If more than 1 and we are interactive, prompt the user to pick
				if (interactive)
				{
					var tfms = selectedProject.TargetFrameworks
						// Only return supported tfms
						.filter(x => MeadowProjectManager.getIsSupportedProject(selectedProject))
						.map(x => x.ShortName);

					var t = await vscode.window.showQuickPick(tfms, { placeHolder: "Startup Project's Target Framework" });

					if (t)
						selectedTargetFramework = t;
				}
				else {
					// Pick the first one if not interactive
					selectedTargetFramework = selectedProject.TargetFrameworks[0].ShortName;
				}
			}
		}
		else if (selectedProject.TargetFramework)
		{
			selectedTargetFramework = this.fixTfm(selectedProject.TargetFramework);
		}
		
		if (!selectedTargetFramework)
			return;

		MeadowProjectManager.Shared.StartupInfo.Project = selectedProject;
		MeadowProjectManager.Shared.StartupInfo.TargetFramework = selectedTargetFramework;

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


	fixTfm(targetFramework: string) : string {

		// /^net[0-9]{2}(\-[a-z0-9\.]+)?$/gis
		var r = /^net[0-9]{2}(\-[a-z0-9\.]+)?$/gis.test(targetFramework);
		if (r)
			return 'net' + targetFramework[3] + '.' + targetFramework[4] + targetFramework.substr(5);
		return targetFramework;
	}


	public async updateProjectStatus() {
	
		var selProj = MeadowProjectManager.Shared.StartupInfo?.Project;
		var selConfig = MeadowProjectManager.Shared.StartupInfo?.Configuration;

		var projStr = "Meadow Project";
		if (selProj)
		{
			projStr = selProj.Name ?? selProj.AssemblyName ?? "Meadow Project";

			if (selConfig)
				projStr += " | " + selConfig;
		}

		this.projectStatusBarItem.text = "$(project) " + projStr;
		this.projectStatusBarItem.tooltip = selProj === undefined ? "Select a Meadow Project" : selProj.Path;
		
		if (this.StartupProjects && this.StartupProjects.length > 0)
			this.projectStatusBarItem.show();
		else
			this.projectStatusBarItem.hide();
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
		this.deviceStatusBarItem.tooltip = deviceStr;

		if (this.StartupProjects && this.StartupProjects.length > 0)
			this.deviceStatusBarItem.show();
		else
			this.deviceStatusBarItem.hide();
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

	static launchConfiguration = vscode.workspace.getConfiguration('launch');
	static savedConfigurations: any;

	public static async refreshDeviceList(): Promise<void> {
		var util = new MeadowUtil();
		var meadowDevices : DeviceData[] = [];

		await vscode.window.withProgress({
			location: vscode.ProgressLocation.Notification,
			cancellable: false,
			title: 'Refreshing Device List'
		}, async (progress) => {
			// Load devices
			progress.report({  increment: 0 });
			meadowDevices = await util.GetDevices();
			progress.report({ increment: 100 });
		});

		if (this.savedConfigurations === undefined){
			this.savedConfigurations = this.launchConfiguration['configurations'];
		}

		let configurations = this.savedConfigurations;
		var count = 0;
		meadowDevices.forEach(item => {
			// check if device is already part of the list before adding it
			if (!configurations.some(c => c["name"] === item.name)) {
				// Add newly found device to list.
				configurations.push({
					"name": item.name,
					"type": "meadow",
					"request": "launch",
					"preLaunchTask": "meadow: Build",
				});
				count++;
			}
		});

		if (count > 0) {
			this.launchConfiguration.update('configurations', configurations, false).then(() => 
				vscode.window.showInformationMessage('Devices Added!')
			);
		}
	}

	public static async resetLaunchConfigurations(){
		this.launchConfiguration.update('configurations', this.savedConfigurations, false);
	}
}