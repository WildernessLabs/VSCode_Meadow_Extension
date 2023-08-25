<img src="Design/banner.jpg" style="margin-bottom:10px" />

<img width="1392" alt="Screen Shot 2021-09-26 at 2 40 16 PM" src="https://user-images.githubusercontent.com/271950/134820282-83c9842a-023a-47ae-976e-7b6c58e851c0.png">

## Build Status
[![Build](https://github.com/WildernessLabs/VSCode_Meadow_Extension/actions/workflows/main.yml/badge.svg)](https://github.com/WildernessLabs/VSCode_Meadow_Extension/actions)

## Supported Operating Systems
We tested this extension on the following operating systems:
- Windows
- macOS 
- Linux (Ubuntu)

## Using the Extension

### Installation

#### Marketplace Installation
1. In VSCode goto the _Extensions_ tab (macOS: Cmd+Shift+X. Others: Ctrl+Shift+X)
2. In the search bar type `VSCode Tools for Meadow`. It should be the 1st extension in the list.
3. On the bottom right of the listed item should be an `Install` button. Click it.
4. The extension should now be installed.

It should look similar to this:
![VS Code showing the extension in the Visual Studio Marketplace.](/Design/vscode-extension-marketplace.png)

#### Manual Installation of Alpha/Beta CI builds
1. Download the alpah/beta extension (.vsix file) from out [latest GitHub CI](https://github.com/WildernessLabs/VSCode_Meadow_Extension/actions).
2. In VSCode goto the _Extensions_ tab (macOS: `Cmd+Shift+X`. Others: `Ctrl+Shift+X`)
3. click the `...` menu and choose _Install from VSIX..._.
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

### Building and Deploying your Meadow App in VSCode

1. Ensure your Meadow board is plugged in, and up to date.
2. Open your new app's folder in VSCode.
3. Any attached devices should appear in the _Run and Debug_ list
4. Choose _Run -> Start Debugging_ (short-cut: `F5`) (Your code will automagically be built first).
5. If you have move than 1 Meadow device attached, you will be prompted to pick a serial port/device to deploy to. If you have only have 1 Meadow device attached it will use that automagically.
6. Watch the output in the _Termainal_  and _Debug Console_ tabs, as your app is deployed!
7. You will be able to set breakpoints and debug your Meadow App.

### .NET Version

You may need to add a `global.json` file to your project's directory to tell it to use .NET 6.0:

```
"sdk": {
		"version": "6.0.413",
		"allowPrerelease": false,
		"rollForward": "latestMinor"
	}
```

### Refresh the attached device list
You can refresh the list of attached device list by using the following short-cut on:
- macOS use: `Cmd+Alt+Shift+R`
- Other platforms use: `Ctrl+Alt+Shift+R`

or search for the select device command by pressing `Ctrl+Shift+P` and typing "Meadow" when prompted

### Toggle the Build Configuration
You can toggle the project's build configuration, using the `Toggle Build Configuraton` button on the bottom status bar, to toggle between Debug and Release builds: 

![VSCode status bar toggle build configutation button.](/Design/vscode-toggle-build-configuration.png)

You can also use the following short-cut on:
- macOS use: `Cmd+Alt+Shift+T`
- Other platforms use: `Ctrl+Alt+Shift+T`

## Building the Extension and Contributing

### Prerequisites

- [Node.js](https://nodejs.org/en/download/) (and npm)
- [TypeScript](https://www.typescriptlang.org/download) (`npm install typescript --save-dev`)
- .NET ([Mono on macOS](https://www.mono-project.com/download/stable/#download-mac), [.NET 6.x on Windows/Linux](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- [.NET Core 3.1.x+](https://dotnet.microsoft.com/en-us/download/dotnet/3.1)

### Initial setup

With all the listed preqrequisites installed, run `npm i` to ensure all of the packages are installed for the project.

### Checkout

- Be sure to checkout this repo with submodules: `git clone --recurse-submodules git@github.com:WildernessLabs/VSCode_Meadow_Extension.git`
- [Meadow.CLI](https://github.com/WildernessLabs/Meadow.CLI) repo must be cloned adjacent to this checkout.

### Building the Extension

The extension has 2 parts. There is a client, which is written in _TypeScript_ and a server which is writtne in _C#_.

- Open the extension folder VSCode.
- Got to _Run and Debug_ (macOS: `Cmd+Shift+D`. Others: `Ctrl+Shift+D`)

![VSCode Run and Debug Configurations](/Design/vscode-run-and-debug.png)

You can choose the `Debug Extension + Server` option in the debug menu in VSCode to debug both parts at the same time.

This will launch the server process in debug listening mode.

You will be able to set breakpoints in the host instance of VSCode and debug the TypeScript extension.

This does not allow you to debug the arbitrary commands sent to the vscode-meadow.exe process from the extension for things like getting a list of devices.  This will only allow you to debug the code path of a VSCode instance starting a Deploy/Debug session.


## Debugging just the TypeScript Extension

- Choose `Debug Extension` from the Debug menu in VSCode and run it.
- Open a meadow project in the new instance of VSCode which now includes the extension.

You can set breakpoints in the host instance of VSCode and debug the TypeScript.

### Packaging VSIX

To produce a VSIX for the VSCode extension:
- Open a Terminal Window in the extension folder
- Run the following command (we recommend building with `--pre-release` flag when buiding locally, to avoid confusion when installed into VSCode)
    ```console
    vsce package --pre-release
    ```


## License

Released under the [Apache 2 license](LICENSE.txt).