using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;

namespace Meadow.VS2022
{
    /// <summary>
    /// Provides launch settings for Meadow debugging.
    /// This provider creates debug configurations that use the Meadow debug adapter.
    /// </summary>
    [Export(typeof(IDebugProfileLaunchTargetsProvider))]
    [AppliesTo(ProjectCapabilities.CSharp)]
    [Order(Order.BeforeDefault)]
    public class MeadowLaunchProvider : IDebugProfileLaunchTargetsProvider
    {
        private const string MeadowDebugType = "meadow";

        [ImportingConstructor]
        public MeadowLaunchProvider(ConfiguredProject configuredProject)
        {
            ConfiguredProject = configuredProject;
        }

        private ConfiguredProject ConfiguredProject { get; }

        public bool SupportsProfile(ILaunchProfile profile)
        {
            // Support profiles with commandName "meadow" or debugger type "meadow"
            return string.Equals(profile.CommandName, MeadowDebugType, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(
            DebugLaunchOptions launchOptions,
            ILaunchProfile profile)
        {
            var settings = new DebugLaunchSettings(launchOptions)
            {
                LaunchOperation = DebugLaunchOperation.CreateProcess,
                LaunchDebugEngineGuid = new Guid("{DAB324E9-7B35-454C-ACA8-F6BB0D5A8673}"), // Debug Adapter Host GUID
                CurrentDirectory = Path.GetDirectoryName(ConfiguredProject.UnconfiguredProject.FullPath) ?? "",
            };

            // Get the project path
            var projectPath = ConfiguredProject.UnconfiguredProject.FullPath;

            // Build the launch options JSON that will be passed to the debug adapter
            var launchJson = new Dictionary<string, object>
            {
                ["type"] = MeadowDebugType,
                ["name"] = "Meadow Debug",
                ["request"] = "launch",
                ["projectPath"] = projectPath,
                ["projectConfiguration"] = GetConfiguration()
            };

            // The debug adapter host will use this to communicate with our adapter
            settings.Options = System.Text.Json.JsonSerializer.Serialize(launchJson);

            return new[] { settings };
        }

        public Task OnAfterLaunchAsync(DebugLaunchOptions launchOptions, ILaunchProfile profile)
        {
            return Task.CompletedTask;
        }

        private string GetConfiguration()
        {
            // Try to determine the current build configuration
            // Default to Debug if we can't determine it
            return "Debug";
        }
    }
}
