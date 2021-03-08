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

export class MeadowProjectManager {
	static SelectedProject: MSBuildProjectInfo;
	static SelectedProjectConfiguration: string;
	static SelectedTargetFramework: string;
	static SelectedDevice: DeviceData;
	static Devices: DeviceData[];
	static DebugPort: number = 55555;

	static Shared: MeadowProjectManager;

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

					if (MeadowProjectManager.getIsSupportedProject(msbProjInfo, p)) {
						this.StartupProjects.push(await MSBuildProjectInfo.fromProject(p));
					}
				}

				MeadowProjectManager.SelectedProject = undefined;
				MeadowProjectManager.SelectedProjectConfiguration = undefined;
				MeadowProjectManager.SelectedTargetFramework = undefined;
				MeadowProjectManager.SelectedDevice = undefined;

				// Try and auto select some defaults
				if (this.StartupProjects.length == 1)
				{
					var sp = this.StartupProjects[0];

					MeadowProjectManager.SelectedProject = sp;

					var defaultConfig = "Debug";

					if (!sp.Configurations || sp.Configurations.length <= 0)
					{
						MeadowProjectManager.SelectedProjectConfiguration = defaultConfig;
					}
					else
					{
						if (sp.Configurations.includes(defaultConfig))
							MeadowProjectManager.SelectedProjectConfiguration = defaultConfig;
						
						MeadowProjectManager.SelectedProjectConfiguration = sp.Configurations[0];
					}
						
						
					if (sp.TargetFrameworks)
					{
						if (sp.TargetFrameworks.length == 1)
							MeadowProjectManager.SelectedTargetFramework = this.fixTfm(sp.TargetFrameworks[0].ShortName);
					}
					else
					{
						MeadowProjectManager.SelectedTargetFramework = this.fixTfm(sp.TargetFramework);
					}

					var util = new MeadowUtil();
					var devices = await util.GetDevices();
					
					var meadowDevices = devices
						.map(x => ({
							label: x.name,
							device: x,
						}));

					if (meadowDevices.length == 1)
						MeadowProjectManager.SelectedDevice = meadowDevices[0].device;
				}

				this.setupMenus();

				this.updateProjectStatus();
				this.updateDeviceStatus();
			}
		});
	}

	isMenuSetup: boolean = false;

	setupMenus()
	{
		if (this.isMenuSetup)
			return;

		this.context.subscriptions.push(vscode.commands.registerCommand("meadow.selectProject", this.showProjectPicker, this));
		this.context.subscriptions.push(vscode.commands.registerCommand("meadow.selectDevice", this.showDevicePicker, this));

		this.projectStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
		this.deviceStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);

		this.updateProjectStatus();
		this.updateDeviceStatus();

		this.isMenuSetup = true;
	}

	projectStatusBarItem: vscode.StatusBarItem;
	deviceStatusBarItem: vscode.StatusBarItem;

	public StartupProjects = new Array<MSBuildProjectInfo>();

	fixTfm(targetFramework: string) : string {

		// /^net[0-9]{2}(\-[a-z0-9\.]+)?$/gis
		var r = /^net[0-9]{2}(\-[a-z0-9\.]+)?$/gis.test(targetFramework);
		if (r)
			return 'net' + targetFramework[3] + '.' + targetFramework[4] + targetFramework.substr(5);
		return targetFramework;
	}

	public async showTfmPicker(projectInfo: MSBuildProjectInfo): Promise<void> {
		if (projectInfo.TargetFrameworks && projectInfo.TargetFrameworks.length > 0) {
			// Multi targeted app, ask the user which TFM to startup
			var tfms = projectInfo.TargetFrameworks
				.map(x => ({
					label: x.ShortName,
					tfm: x
				}));


			const tfm = await vscode.window.showQuickPick(tfms, { placeHolder: "Target Framework" });
			if (tfm)
				MeadowProjectManager.SelectedTargetFramework = this.fixTfm(tfm.tfm.ShortName);
			else
				MeadowProjectManager.SelectedTargetFramework = this.fixTfm(projectInfo.TargetFramework);
		}
		else {
			// Not multi targeted, don't need to ask the user
			MeadowProjectManager.SelectedTargetFramework = this.fixTfm(projectInfo.TargetFramework);
		}
	}
	public async showProjectPicker(): Promise<void> {
		var projects = this.StartupProjects
			.map(x => ({
				//description: x.type.toString(),
				label: x.AssemblyName,
				project: x,
			}));
		const p = await vscode.window.showQuickPick(projects, { placeHolder: "Select a Startup Project" });
		if (p) {

			// Next pick TFM
			await this.showTfmPicker(p.project);

			var config = "Debug";

			if (p.project.Configurations && p.project.Configurations.length > 0)
			{
				const c = await vscode.window.showQuickPick(p.project.Configurations, { placeHolder: "Build Configuration" });
				if (c)
					config = c;
			}

			MeadowProjectManager.SelectedProject = p.project;
			MeadowProjectManager.SelectedProjectConfiguration = config;
			MeadowProjectManager.SelectedDevice = undefined;
		}
		
		this.updateProjectStatus();
		this.updateDeviceStatus();
	}

	public async updateProjectStatus() {
		var selProj = MeadowProjectManager.SelectedProject;

		var projectString = selProj === undefined ? "Startup Project" : `${selProj.Name ?? selProj.AssemblyName} | ${MeadowProjectManager.SelectedProjectConfiguration}`;
		this.projectStatusBarItem.text = "$(project) " + projectString;
		this.projectStatusBarItem.tooltip = selProj === undefined ? "Select a Startup Project" : selProj.Path;
		this.projectStatusBarItem.command = "meadow.selectProject";
		this.projectStatusBarItem.show();
	}


	public async showDevicePicker(): Promise<void> {

		if (MeadowProjectManager.SelectedProject === undefined) {
			await vscode.window.showInformationMessage("Select a Startup Project first.");
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
			MeadowProjectManager.SelectedDevice = p.device;
		}
		

		this.updateDeviceStatus();
	}

	public async updateDeviceStatus() {
		var deviceStr = MeadowProjectManager.SelectedDevice === undefined ? "Select a Device" : `${MeadowProjectManager.SelectedDevice.name}`;
		this.deviceStatusBarItem.text = "$(device-mobile) " + deviceStr;
		this.deviceStatusBarItem.tooltip = MeadowProjectManager.SelectedProject === undefined ? "Select a Device" : deviceStr;
		this.deviceStatusBarItem.command = "meadow.selectDevice";
		this.deviceStatusBarItem.show();
	}

	public static getIsSupportedProject(projectInfo: MSBuildProjectInfo, project: MSBuildProject): boolean
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