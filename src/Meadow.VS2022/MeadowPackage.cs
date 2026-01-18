using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Meadow.VS2022
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// The Meadow package registers the debug adapter for Meadow debugging via DAP.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(MeadowPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class MeadowPackage : AsyncPackage
    {
        /// <summary>
        /// MeadowPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "c1d4e8a2-3f5b-4c6d-9e8f-0a1b2c3d4e5f";

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Package is now initialized - debug adapter registration is handled via the JSON configuration
            // and the Debug Adapter Host in Visual Studio
        }
    }
}
