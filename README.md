<img src="Design/banner.jpg" style="margin-bottom:10px" />

<img width="1392" alt="Screen Shot 2021-09-26 at 2 40 16 PM" src="https://user-images.githubusercontent.com/271950/134820282-83c9842a-023a-47ae-976e-7b6c58e851c0.png">

## Build Status
[![Build](https://github.com/WildernessLabs/VSCode_Meadow_Extension/actions/workflows/main.yml/badge.svg)](https://github.com/WildernessLabs/VSCode_Meadow_Extension/actions)

## Getting Started

The [Meadow.CLI](https://github.com/WildernessLabs/Meadow.CLI) repo must be cloned adjacent to this checkout.

## Using the Extension

### Installation

1. Download the extension .vsix file from the [latest GitHub release](https://github.com/WildernessLabs/VSCode_Meadow_Extension/releases).
2. In the _Extensions_ tab in VS Code, click the `...` menu and choose _Install from VSIX..._.
3. Pick the file you downloaded to install.

### Create a new Meadow Project

In the terminal:

1. Run `dotnet new install WildernessLabs.Meadow.Template`
2. Create and/or navigate into a directory with the name of your new app (ie: `MeadowApp1`).
3. Run `dotnet new Meadow`

    Alternatively, you can also specify the folder where your new project will go directly by appending the `--output` parameter.
    
    ```console
    dotnet new Meadow --output MyNewMeadowApp
    ```

5. Open the app directory in VS Code

### Building and Deploying your App in VSCode

> **IMPORTANT - macOS Users**: If you had previously set `omnisharp.useGlobalMono` to `always` in VSCode's settings (ie: `"omnisharp.useGlobalMono": "always"`), try removing the setting.  As of `0.3.0` of the Meadow Extension, you _may_ need to set this to `never` (the opposite of the previous requirement) since the extension is now built with .NET 5.0.

1. Open your new app's folder in VSCode.
2. Ensure your Meadow board is plugged in, and up to date.
3. Choose _Run -> Start Debugging_ (Your code will automatically be built first).
4. From the list of debugging providers, choose `Meadow`.
5. If prompted, pick the serial port for your Meadow board.
6. Watch the output and see your app deploy!

### .NET Version

You may need to add a `global.json` file to your project's directory to tell it to use .NET 6.0:

```
"sdk": {
		"version": "6.0.101",
		"allowPrerelease": false,
		"rollForward": "latestMinor"
	}
```

### Example launch.json

You can optionally create a _launch.json_ file to keep your debug configuration instead of always running it dynamically.  Simply navigate to the Debug tab of VSCode and use the button to create a launch.json file.  Choose `Meadow` again from the list, and the default launch settings will be created for you.

```
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Deploy",
      "type": "meadow",
      "request": "launch",
      "preLaunchTask": "meadow: Build"
    }
  ]
}
```

### Change the target Meadow device

After selecting the device deployment target the first time, the selected serial port value will be used for future deployments. If you want to change which device is targeted for deployment, select the current device serial port section to prompt for a different connected Meadow device.

![Visual Studio Code status bar showing the Meadow device selection details.](/Design/vscode-device-target-status.png)

You can also use the following short-cut on

Mac use: `Cmd+Alt+Shift+R`

Other platforms use: `Ctrl+Alt+Shift+R`

or search for the select device command by pressing `Ctrl+Shift+P` and typing "Meadow" when prompted

## Building the Extension

### Prerequisites

1. [Node.js](https://nodejs.org/en/download/) (and npm)
2. [TypeScript](https://www.typescriptlang.org/download) (`npm install typescript --save-dev`)
3. .NET ([Mono on macOS](https://www.mono-project.com/download/stable/#download-mac), [.NET 4.7.2 on Windows](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472))
4. [.NET Core 3.1.x+](https://dotnet.microsoft.com/en-us/download/dotnet/3.1)
5. [PowerShell](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell)

### Initial setup

With all the listed preqrequisites installed, run `npm i` to ensure all of the packages are installed for the project.

### Building the Extension

To build both the C# and TypeScript code simply run: `pwsh build.ps1`

### Packaging VSIX

To produce a VSIX for the VSCode extension run: `pwsh build.ps1 vsix`


## Debugging

1. Open the folder in VSCode
2. Run `pwsh build.ps1` once at least once.
3. Choose `Extension` from the Debug menu in VSCode and run it.
4. Open a meadow project in the new instance of VSCode which now includes the extension.

You can set breakpoints in the host instance of VSCode and debug the TypeScript.

### Debugging C#

You can choose the `Extension + Server` option in the debug menu in VSCode to debug both parts at the same time.

This will launch the server process in debug listening mode.

This requires that you add `"debugServer": 4711` to the launch.json configuration of the Meadow project you are debugging in the hosted debug extension instance of VSCode.

This does not allow you to debug the arbitrary commands sent to the vscode-meadow.exe process from the extension for things like getting a list of devices.  This will only allow you to debug the code path of a VSCode instance starting a Deploy/Debug session.


