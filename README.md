# Visual Studio Code Meadow Extension

This is the add-in for Visual Studio Code that enables Meadow projects to be built and deployed to Meadow device.

## Building

### Prerequisites

1. Node.js (and npm)
2. Typescript
3. .NET (Mono on macOS, .NET 4.7.2 on Windows)
4. .NET Core 3.1.x+
5. Powershell

### Initial setup

With all the listed preqrequisites installed, run `npm i` to ensure all of the packages are installed for the project.

### Building the Extension

To build both the C# and Typescript code simply run: `pwsh build.ps1`

### Packaging VSIX

To produce a VSIX for the VSCode extension run: `pwsh build.ps1 vsix`


## Debugging

1. Open the folder in VSCode
2. Run `pwsh build.ps1` once at least once.
3. Choose `Extension` from the Debug menu in VSCode and run it.
4. Open a meadow project in the new instance of VSCode which now includes the extension.

You can set breakpoints in the host instance of VSCode and debug the typescript.

### Debugging C#

You can choose the `Extension + Server` option in the debug menu in VSCode to debug both parts at the same time.

This will launch the server process in debug listening mode.

This requires that you add `"debugServer": 4711` to the launch.json configuration of the Meadow project you are debugging in the hosted debug extension instance of VSCode.

This does not allow you to debug the arbitrary commands sent to the vscode-meadow.exe process from the extension for things like getting a list of devices.  This will only allow you to debug the code path of a VSCode instance starting a Deploy/Debug session.


