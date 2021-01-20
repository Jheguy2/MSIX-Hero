﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Otor.MsixHero.Appx.Diagnostic.System;
using Otor.MsixHero.Appx.Packaging.Manifest.Entities.Build;
using Otor.MsixHero.Appx.Packaging.Manifest.FileReaders;

namespace Otor.MsixHero.Appx.Packaging.Manifest.Helpers
{
    public class AuthoringAppDetector
    {
        private readonly IAppxFileReader fileReader;

        public AuthoringAppDetector(IAppxFileReader fileReader)
        {
            this.fileReader = fileReader;
        }

        public bool TryDetectAny(IReadOnlyDictionary<string, string> buildKeyValues, out BuildInfo buildInfo)
        {
            return this.TryDetectAdvancedInstaller(buildKeyValues, out buildInfo)
                   || this.TryDetectVisualStudio(buildKeyValues, out buildInfo)
                   || this.TryDetectRayPack(buildKeyValues, out buildInfo)
                   || this.TryDetectMsixHero(buildKeyValues, out buildInfo);
        }
        
        public bool TryDetectVisualStudio(IReadOnlyDictionary<string, string> buildValues, out BuildInfo buildInfo)
        {
            buildInfo = null;

            if (buildValues == null || !buildValues.Any())
            {
                return false;
            }

            if (!buildValues.TryGetValue("VisualStudio", out var visualStudio))
            {
                return false;
            }

            buildInfo = new BuildInfo
            {
                ProductName = "Microsoft Visual Studio",
                ProductVersion = visualStudio,

            };

            if (buildValues.TryGetValue("OperatingSystem", out var win10))
            {
                var firstUnit = win10.Split(' ')[0];
                buildInfo.OperatingSystem = Windows10Parser.GetOperatingSystemFromNameAndVersion(firstUnit).ToString();
                buildInfo.Components = new Dictionary<string, string>(buildValues);
            }

            return true;
        }

        public bool TryDetectAdvancedInstaller(IReadOnlyDictionary<string, string> buildValues, out BuildInfo buildInfo)
        {
            buildInfo = null;

            if (buildValues == null || !buildValues.Any())
            {
                return false;
            }

            if (!buildValues.TryGetValue("AdvancedInstaller", out var advInst))
            {
                return false;
            }

            buildValues.TryGetValue("ProjectLicenseType", out var projLic);
            buildInfo = new BuildInfo
            {
                ProductLicense = projLic,
                ProductName = "Advanced Installer",
                ProductVersion = advInst
            };

            if (buildValues.TryGetValue("OperatingSystem", out var os))
            {
                var win10Version = Windows10Parser.GetOperatingSystemFromNameAndVersion(os);
                buildInfo.OperatingSystem = win10Version.ToString();
            }

            return true;
        }

        public bool TryDetectMsixHero(IReadOnlyDictionary<string, string> buildValues, out BuildInfo buildInfo)
        {
            buildInfo = null;

            if (buildValues == null || !buildValues.Any())
            {
                return false;
            }

            if (!buildValues.TryGetValue("MsixHero", out var msixHero))
            {
                return false;
            }

            buildInfo = new BuildInfo
            {
                ProductName = "MSIX Hero",
                ProductVersion = msixHero
            };

            if (buildValues.TryGetValue("OperatingSystem", out var os))
            {
                var win10Version = Windows10Parser.GetOperatingSystemFromNameAndVersion(os);
                buildInfo.OperatingSystem = win10Version.ToString();
            }

            return true;
        }

        public bool TryDetectRayPack(IReadOnlyDictionary<string, string> buildValues, out BuildInfo buildInfo)
        {
            buildInfo = null;

            if (buildValues == null || !buildValues.Any())
            {
                // Detect RayPack by taking a look at the metadata of PsfLauncher.
                // This is a fallback in case there are no other build values.
                const string fileLauncher = "PsfLauncher.exe";
                if (!fileReader.FileExists(fileLauncher))
                {
                    return false;
                }
                
                using (var launcher = this.fileReader.GetFile(fileLauncher))
                {
                    FileVersionInfo fileVersionInfo;
                    if (launcher is FileStream fileStream)
                    {
                        fileVersionInfo = FileVersionInfo.GetVersionInfo(fileStream.Name);
                    }
                    else
                    {
                        var tempFilePath = Path.GetTempFileName();
                        try
                        {
                            using (var fs = File.OpenWrite(tempFilePath))
                            {
                                launcher.CopyTo(fs);
                                fs.Flush();
                            }

                            fileVersionInfo = FileVersionInfo.GetVersionInfo(tempFilePath);
                        }
                        finally
                        {
                            File.Delete(tempFilePath);
                        }
                    }

                    if (fileVersionInfo.ProductName != null && fileVersionInfo.ProductName.StartsWith("Raynet", StringComparison.OrdinalIgnoreCase))
                    {
                        var pv = fileVersionInfo.ProductVersion;
                        buildInfo = new BuildInfo
                        {
                            ProductName = "RayPack " + Version.Parse(pv).ToString(2),
                            ProductVersion = fileVersionInfo.ProductVersion
                        };

                        return true;
                    }
                }
            }
            else
            {
                // Detect RayPack 6.2 which uses build meta data like this:
                // <build:Item Name="OperatingSystem" Version="6.2.9200.0" /><build:Item Name="Raynet.RaySuite.Common.Appx" Version="6.2.5306.1168" /></build:Metadata>
                if (!buildValues.TryGetValue("Raynet.RaySuite.Common.Appx", out var rayPack))
                {
                    return false;
                }
                
                if (Version.TryParse(rayPack, out var parsedVersion))
                {
                    buildInfo = new BuildInfo
                    {
                        ProductName = $"RayPack {parsedVersion.Major}.{parsedVersion.Minor}",
                        ProductVersion = $"(MSIX builder v{parsedVersion})"
                    };
                }
                else
                {
                    buildInfo = new BuildInfo
                    {
                        ProductName = "RayPack",
                        ProductVersion = $"(MSIX builder v{rayPack})"
                    };
                }

                if (buildValues.TryGetValue("OperatingSystem", out var os))
                {
                    var win10Version = Windows10Parser.GetOperatingSystemFromNameAndVersion(os);
                    buildInfo.OperatingSystem = win10Version.ToString();
                }
                
                return true;
            }

            return false;
        }
    }
}
