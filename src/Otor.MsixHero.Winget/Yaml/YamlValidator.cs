﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Otor.MsixHero.Appx.Packaging.Installation;

namespace Otor.MsixHero.Winget.Yaml
{
    public class YamlValidator
    {
        /// <summary>
        /// Validates a given YAML file.
        /// </summary>
        /// <param name="yamlPath">The path to a YAML file.</param>
        /// <param name="throwIfWinGetMissing">If set to <c>true</c> an exception will be thrown if WinGet is not installed. Otherwise it will skip the check.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the validation job.</returns>
        public async Task<string> ValidateAsync(string yamlPath, bool throwIfWinGetMissing = false, CancellationToken cancellationToken = default)
        {
            var pkg = await Task.Run(() => AppxPackageManager.PackageManager.Value.FindPackagesForUser(string.Empty, "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe").FirstOrDefault(), cancellationToken).ConfigureAwait(false);
            if (pkg == null)
            {
                pkg = await Task.Run(() => AppxPackageManager.PackageManager.Value.FindPackagesForUser(string.Empty, "Microsoft.WindowsTerminal_8wekyb3d8bbwe").FirstOrDefault(), cancellationToken).ConfigureAwait(false);
            }

            if (pkg == null)
            {
                if (throwIfWinGetMissing)
                {
                    throw new FileNotFoundException("winget not found.", "winget");
                }

                return null;
            }

            const string cmd = "cmd.exe";
            var outputPath = Path.GetTempFileName();
            var args = $"/c winget validate \"{yamlPath}\" >> {outputPath}";
            try
            {
                var psi = new ProcessStartInfo(cmd, args)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                var p = Process.Start(psi);
                if (p == null)
                {
                    if (throwIfWinGetMissing)
                    {
                        throw new InvalidOperationException("Could not start winget.");
                    }

                    return null;
                }

                p.WaitForExit();

                return await File.ReadAllTextAsync(outputPath, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }
    }
}