using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MeadowCLI.DeviceManagement;
using MeadowCLI.Hcom;

namespace VsCodeMeadowUtil
{
    public class MeadowDeployer
    {
        public MeadowSerialDevice MeadowDevice { get; private set; }

        public MeadowDeployer(MeadowSerialDevice meadowSerialDevice, Action<string> outputHandler)
        {
            MeadowDevice = meadowSerialDevice;
            OutputHandler = outputHandler;
        }

        public Action<string> OutputHandler { get; private set; }

        void Log(string message)
        {
            OutputHandler?.Invoke(message);
            Console.WriteLine(message);
        }

        public async Task<bool> Deploy(MeadowSerialDevice meadow, CancellationTokenSource cts, string folder)
        {
            try
            {
                var localFiles = await GetLocalFiles(cts, folder);

                var meadowFiles = await GetFilesOnDevice(meadow, cts);

                await DeleteUnusedFiles(meadow, cts, meadowFiles, localFiles);

                await DeployApp(meadow, cts, folder, meadowFiles, localFiles);

                await MeadowDeviceManager.MonoEnable(meadow);

                //MeadowDeviceManager.VSDebugging(55556);

                Log("Resetting Meadow and starting app");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }

            return false;
        }

        async Task<(List<string> files, List<UInt32> crcs)> GetFilesOnDevice(MeadowSerialDevice meadow, CancellationTokenSource cts)
        {
            if (cts.IsCancellationRequested) { return (new List<string>(), new List<UInt32>()); }

            Log("Checking files on device (may take several seconds)");

            var meadowFiles = await meadow.GetFilesAndCrcs(30000);

            foreach (var f in meadowFiles.files)
            {
                if (cts.IsCancellationRequested)
                    break;

                Log($"Found {f}");
            }

            return meadowFiles;
        }

        async Task<(List<string> files, List<UInt32> crcs)> GetLocalFiles(CancellationTokenSource cts, string folder)
        {
            //get list of files in folder
            var paths = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(s => s.EndsWith(".exe") ||
                        //s.EndsWith(".dll") ||
                        s.EndsWith(".bmp") ||
                        s.EndsWith(".jpg") ||
                        s.EndsWith(".jpeg") ||
                        s.EndsWith(".txt") ||
                        s.EndsWith(".json") ||
                        s.EndsWith(".xml") ||
                        s.EndsWith(".yml") ||
                        s.EndsWith("Meadow.Foundation.dll"));

            var dependences = AssemblyManager.GetDependencies("App.exe", folder);

            var files = new List<string>();
            var crcs = new List<UInt32>();

            //crawl other files (we can optimize)
            foreach (var file in paths)
            {
                if (cts.IsCancellationRequested) break;

                using (FileStream fs = File.Open(file, FileMode.Open))
                {
                    var len = (int)fs.Length;
                    var bytes = new byte[len];

                    fs.Read(bytes, 0, len);

                    //0x
                    var crc = CrcTools.Crc32part(bytes, len, 0);// 0x04C11DB7);

                    Log($"{file} crc is {crc}");
                    files.Add(Path.GetFileName(file));
                    crcs.Add(crc);
                }
            }

            //crawl dependences
            foreach (var file in dependences)
            {
                if (cts.IsCancellationRequested) { break; }

                using (FileStream fs = File.Open(Path.Combine(folder, file), FileMode.Open))
                {
                    var len = (int)fs.Length;
                    var bytes = new byte[len];

                    fs.Read(bytes, 0, len);

                    //0x
                    var crc = CrcTools.Crc32part(bytes, len, 0);// 0x04C11DB7);

                    Log($"{file} crc is {crc}");
                    files.Add(Path.GetFileName(file));
                    crcs.Add(crc);
                }
            }

            return (files, crcs);
        }

        async Task DeleteUnusedFiles(MeadowSerialDevice meadow, CancellationTokenSource cts,
            (List<string> files, List<UInt32> crcs) meadowFiles, (List<string> files, List<UInt32> crcs) localFiles)
        {
            if (cts.IsCancellationRequested)
                return;

            foreach (var file in meadowFiles.files)
            {
                if (cts.IsCancellationRequested) { break; }

                if (localFiles.files.Contains(file) == false)
                {
                    await meadow.DeleteFile(file);
                    Log($"Removing {file}");
                }
            }
        }

        async Task DeployApp(MeadowSerialDevice meadow, CancellationTokenSource cts, string folder,
            (List<string> files, List<UInt32> crcs) meadowFiles, (List<string> files, List<UInt32> crcs) localFiles)
        {
            if (cts.IsCancellationRequested)
            { return; }

            for (int i = 0; i < localFiles.files.Count; i++)
            {
                if (meadowFiles.crcs.Contains(localFiles.crcs[i])) { continue; }

                await WriteFileToMeadow(meadow, cts,
                    folder, localFiles.files[i], true);
            }
        }

        async Task WriteFileToMeadow(MeadowSerialDevice meadow, CancellationTokenSource cts, string folder, string file, bool overwrite = false)
        {
            if (cts.IsCancellationRequested) { return; }

            if (overwrite || await meadow.IsFileOnDevice(file).ConfigureAwait(false) == false)
            {
                Log($"Writing {file}");
                await meadow.WriteFile(file, folder).ConfigureAwait(false);
            }
        }
    }
}
