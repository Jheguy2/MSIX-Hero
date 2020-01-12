﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;
using otor.msixhero.lib.BusinessLayer.Appx.Manifest.FileReaders;
using otor.msixhero.lib.BusinessLayer.Helpers;
using otor.msixhero.lib.Domain.Appx.Manifest.Build;
using otor.msixhero.lib.Domain.Appx.Manifest.Full;
using otor.msixhero.lib.Domain.Appx.Packages;

namespace otor.msixhero.lib.BusinessLayer.Appx.Manifest
{
    public class AppxManifestReader : IAppxManifestReader
    {
        protected readonly PsfReader PsfReader = new PsfReader();

        /*private readonly IAppxPackageManager packageManager;
        
        public AppxManifestReader(IAppxPackageManager packageManager)
        {
            this.packageManager = packageManager;
        }*/

        public AppxPackage Read(IAppxFileReader fileReader)
        {
            var isMsix = fileReader.FileExists("AppxManifest.xml");
            if (isMsix)
            {
                return this.ReadMsix(fileReader);
            }

            var isAppxBundle = fileReader.FileExists("AppxMetaData\\AppxBundleManigest.xml");
            if (isAppxBundle)
            {
                return this.ReadBundle(fileReader);
            }

            throw new NotSupportedException("Required package source is not supported.");
        }

        private AppxPackage ReadBundle(IAppxFileReader fileReader)
        {
            throw new NotImplementedException();
        }

        private AppxPackage ReadMsix(IAppxFileReader fileReader)
        {
            var xmlDocument = new XmlDocument();
            using (var file = fileReader.GetFile("AppxManifest.xml"))
            {
                xmlDocument.Load(file);

                var nodePackage = xmlDocument.SelectSingleNode("/*[local-name()='Package']");
                if (nodePackage == null)
                {
                    throw new FormatException("The manifest is malformed. The document root must be <Package /> element.");
                }

                var nodeIdentity = GetNode(nodePackage, "Identity");
                var nodeProperties = GetNode(nodePackage, "Properties");
                var nodeApplicationsRoot = GetNode(nodePackage, "Applications");
                var nodeDependenciesRoot = GetNode(nodePackage, "Dependencies");
                var nodeBuild = GetNode(nodePackage, "Metadata", true);

                var appxPackage = new AppxPackage();
                if (nodeIdentity != null)
                {
                    appxPackage.Name = GetNodeAttribute(nodeIdentity, "Name");

                    if (Enum.TryParse(typeof(AppxPackageArchitecture), GetNodeAttribute(nodeIdentity, "ProcessorArchitecture"), true, out object parsedArchitecture))
                    {
                        appxPackage.ProcessorArchitecture = (AppxPackageArchitecture)parsedArchitecture;
                    }

                    appxPackage.Publisher = GetNodeAttribute(nodeIdentity, "Publisher");
                    appxPackage.Version = GetNodeAttribute(nodeIdentity, "Version");
                }

                if (nodeProperties != null)
                {
                    appxPackage.PublisherDisplayName = GetNodeValue(nodeProperties, "PublisherDisplayName");
                    appxPackage.DisplayName = GetNodeValue(nodeProperties, "DisplayName");
                    var logo = GetNodeValue(nodeProperties, "Logo");
                    if (!string.IsNullOrEmpty(logo))
                    {
                        using (var resourceStream = fileReader.GetResource(logo))
                        {
                            if (resourceStream != null)
                            {
                                using (var memoryStream = new MemoryStream())
                                {
                                    resourceStream.CopyTo(memoryStream);
                                    memoryStream.Flush();
                                    appxPackage.Logo = memoryStream.ToArray();
                                }
                            }
                        }
                    }
                    appxPackage.Description = GetNodeValue(nodeProperties, "Description");
                    appxPackage.IsFramework = string.Equals(GetNodeValue(nodeProperties, "Framework") ?? "false", "true", StringComparison.OrdinalIgnoreCase);
                }

                appxPackage.PackageDependencies = new List<AppxPackageDependency>();
                appxPackage.OperatingSystemDependencies = new List<AppxOperatingSystemDependency>();
                appxPackage.Applications = new List<AppxApplication>();

                if (nodeApplicationsRoot != null)
                {
                    foreach (var node in GetNodes(nodeApplicationsRoot, "Application"))
                    {
                        /*
                         *<Application EntryPoint="SparklerApp.App" Executable="XD.exe" Id="App">
                            <uap:VisualElements BackgroundColor="#2D001E" Description="Adobe XD" DisplayName="Adobe XD" Square150x150Logo="Assets\xd_med_tile.png" Square44x44Logo="Assets\xd_app_list_icon.png">
                              <uap:DefaultTile Square310x310Logo="Assets\xd_large_tile.png" Square71x71Logo="Assets\xd_small_tile.png" Wide310x150Logo="Assets\xd_wide_tile.png">
                                <uap:ShowNameOnTiles>
                                  <uap:ShowOn Tile="square150x150Logo" />
                                  <uap:ShowOn Tile="wide310x150Logo" />
                                  <uap:ShowOn Tile="square310x310Logo" />
                                </uap:ShowNameOnTiles>
                              </uap:DefaultTile>
                              <uap:SplashScreen BackgroundColor="#FFFFFF" Image="Assets\xd_splash.png" />
                            </uap:VisualElements>

                         <Application Id="RayEval" Executable="PsfLauncher32.exe" EntryPoint="Windows.FullTrustApplication">
                            <uap:VisualElements DisplayName="RayEval" Description="RayEval Raynet GmbH" BackgroundColor="transparent" 
                                Square150x150Logo="Assets\Square150x150Logo.scale-100.png" Square44x44Logo="Assets\Square44x44Logo.scale-100.png">
                                <uap:DefaultTile Wide310x150Logo="Assets\Square310x150Logo.scale-100.png" Square310x310Logo="Assets\Square310x310Logo.scale-100.png" Square71x71Logo="Assets\Square71x71Logo.scale-100.png" />
                            </uap:VisualElements>
                            <Extensions />
                        </Application>
                         *
                         */

                        var appxApplication = new AppxApplication();

                        appxApplication.EntryPoint = GetNodeAttribute(node, "EntryPoint");
                        appxApplication.StartPage = GetNodeAttribute(node, "StartPage");
                        appxApplication.Executable = GetNodeAttribute(node, "Executable");
                        appxApplication.Id = GetNodeAttribute(node, "Id");

                        var visualElements = GetNode(node, "VisualElements", true);
                        if (visualElements != null)
                        {
                            appxApplication.Description = GetNodeAttribute(visualElements, "Description");
                            appxApplication.DisplayName = GetNodeAttribute(visualElements, "DisplayName");
                            appxApplication.BackgroundColor = GetNodeAttribute(visualElements, "BackgroundColor");
                            appxApplication.Square150x150Logo = GetNodeAttribute(visualElements, "Square150x150Logo");
                            appxApplication.Square44x44Logo = GetNodeAttribute(visualElements, "Square44x44Logo");

                            var defaultTile = GetNode(visualElements, "DefaultTile", true);
                            if (defaultTile != null)
                            {
                                appxApplication.Wide310x150Logo = GetNodeAttribute(defaultTile, "Wide310x150Logo");
                                appxApplication.Square310x310Logo = GetNodeAttribute(defaultTile, "Square310x310Logo");
                                appxApplication.Square71x71Logo = GetNodeAttribute(defaultTile, "Square71x71Logo");
                                appxApplication.ShortName = GetNodeAttribute(defaultTile, "ShortName");
                            }

                            var logo = appxApplication.Square44x44Logo ?? appxApplication.Square30x30Logo ?? appxApplication.Square71x71Logo ?? appxApplication.Square150x150Logo;
                            if (logo == null)
                            {
                                appxApplication.Logo = appxPackage.Logo;
                            }
                            else
                            {
                                Stream stream = null;
                                try
                                {
                                    stream =
                                        fileReader.GetResource(appxApplication.Square44x44Logo) ??
                                        fileReader.GetResource(appxApplication.Square30x30Logo) ??
                                        fileReader.GetResource(appxApplication.Square71x71Logo) ??
                                        fileReader.GetResource(appxApplication.Square150x150Logo);

                                    if (stream != null)
                                    {
                                        using (var memoryStream = new MemoryStream())
                                        {
                                            stream.CopyTo(memoryStream);
                                            memoryStream.Flush();
                                            appxApplication.Logo = memoryStream.ToArray();
                                        }
                                    }
                                    else
                                    {
                                        appxApplication.Logo = appxPackage.Logo;
                                    }
                                }
                                finally
                                {
                                    if (stream != null)
                                    {
                                        stream.Dispose();
                                    }
                                }
                            }
                        }

                        appxPackage.Applications.Add(appxApplication);
                    }

                    var psfApps = appxPackage.Applications.Where(a =>
                        PackageTypeConverter.GetPackageTypeFrom(a.EntryPoint, a.Executable, a.StartPage) ==
                        MsixPackageType.BridgePsf).ToArray();
                    if (psfApps.Any())
                    {
                        foreach (var psfApp in psfApps)
                        {
                            psfApp.Psf = this.PsfReader.Read(psfApp.Id, fileReader);
                        }
                    }
                }

                if (nodeDependenciesRoot != null)
                {
                    var dependencies = nodeDependenciesRoot.ChildNodes;

                    if (dependencies != null)
                    {
                        foreach (var node in dependencies.OfType<XmlNode>())
                        {
                            switch (node.LocalName)
                            {
                                case "TargetDeviceFamily":
                                {
                                    /*
                                     * <TargetDeviceFamily MaxVersionTested="10.0.15063.0" MinVersion="10.0.15063.0" Name="Windows.Universal" />
                                     */

                                    var minVersion = GetNodeAttribute(node, "MinVersion");
                                    var maxVersion = GetNodeAttribute(node, "MaxVersionTested");
                                    var name = GetNodeAttribute(node, "Name");

                                    appxPackage.OperatingSystemDependencies.Add(new AppxOperatingSystemDependency
                                    {
                                        Minimum = Windows10Parser.GetOperatingSystemFromNameAndVersion(name, minVersion),
                                        Tested = Windows10Parser.GetOperatingSystemFromNameAndVersion(name, maxVersion),
                                    });

                                    break;
                                }

                                case "PackageDependency":
                                {
                                    /*
                                     * <PackageDependency MinVersion="1.4.24201.0" Name="Microsoft.NET.Native.Runtime.1.4" Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />
                                     */

                                    var minVersion = GetNodeAttribute(node, "MinVersion");
                                    var name = GetNodeAttribute(node, "Name");
                                    var publisher = GetNodeAttribute(node, "Publisher");

                                    var appxDepdendency = new AppxPackageDependency
                                    {
                                        Publisher = publisher,
                                        Name = name,
                                        Version = minVersion
                                    };

                                    appxPackage.PackageDependencies.Add(appxDepdendency);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (nodeBuild != null)
                {
                    var buildKeyValues = new Dictionary<string, string>();

                    foreach (var buildNode in GetNodes(nodeBuild, "Item"))
                    {
                        var attrName = buildNode.Attributes["Name"]?.Value;
                        if (attrName == null)
                        {
                            continue;
                        }

                        var attrVersion = buildNode.Attributes["Version"]?.Value;
                        if (attrVersion == null)
                        {
                            attrVersion = buildNode.Attributes["Value"]?.Value;
                            if (attrVersion == null)
                            {
                                continue;
                            }
                        }

                        buildKeyValues[attrName] = attrVersion;
                    }

                    if (this.DetectAdvancedInstaller(buildKeyValues, out var buildInfo)
                        || this.DetectVisualStudio(buildKeyValues, out buildInfo)
                        || this.DetectRayPack(buildKeyValues, fileReader, out buildInfo)
                        || this.DetectMsixHero(buildKeyValues, out buildInfo))
                    {
                        appxPackage.BuildInfo = buildInfo;
                    }
                }

                return appxPackage;
            }
        }

        private bool DetectVisualStudio(Dictionary<string, string> buildValues, out BuildInfo buildInfo)
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
            }

            buildInfo.Components = buildValues;
            return true;
        }

        private bool DetectAdvancedInstaller(Dictionary<string, string> buildValues, out BuildInfo buildInfo)
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

        private bool DetectMsixHero(Dictionary<string, string> buildValues, out BuildInfo buildInfo)
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

        private bool DetectRayPack(Dictionary<string, string> buildValues, IAppxFileReader fileReader, out BuildInfo buildInfo)
        {
            buildInfo = null;

            if (buildValues == null || !buildValues.Any())
            {
                // Detect RayPack by taking a look at the metadata of PsfLauncher.
                const string fileLauncher = "PsfLauncher.exe";
                if (fileReader.FileExists(fileLauncher))
                {
                    using (var launcher = fileReader.GetFile(fileLauncher))
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
            }
            else
            {
                // Detect RayPack 6.2 which uses build meta data like this:
                // <build:Item Name="OperatingSystem" Version="6.2.9200.0" /><build:Item Name="Raynet.RaySuite.Common.Appx" Version="6.2.5306.1168" /></build:Metadata>
                if (buildValues.TryGetValue("Raynet.RaySuite.Common.Appx", out var rayPack))
                {
                    buildInfo = new BuildInfo
                    {
                        ProductName = "RayPack " + Version.Parse(rayPack).ToString(2),
                        ProductVersion = rayPack
                    };

                    if (buildValues.TryGetValue("OperatingSystem", out var os))
                    {
                        var win10Version = Windows10Parser.GetOperatingSystemFromNameAndVersion(os);
                        buildInfo.OperatingSystem = win10Version.ToString();
                    }

                    return true;
                }
            }

            return false;
        }

        private static string GetNodeValue(XmlNode node, string nodeName, bool withNamespace = false)
        {
            return GetNode(node, nodeName, withNamespace)?.InnerText;
        }

        private static IEnumerable<XmlNode> GetNodes(XmlNode node, string nodeName)
        {
            if (node == null)
            {
                yield break;
            }

            foreach (var item in node.ChildNodes.OfType<XmlNode>())
            {
                if (item.LocalName == nodeName)
                {
                    yield return item;
                }
            }
        }

        private static XmlNode GetNode(XmlNode node, string nodeName, bool withNamespace = false)
        {
            if (node == null)
            {
                return null;
            }

            if (!withNamespace)
            {
                return node[nodeName];
            }
            else
            {
                for (var i = 0; i < node.ChildNodes.Count; i++)
                {
                    if (node.ChildNodes[i].LocalName == nodeName)
                    {
                        return node.ChildNodes[i];
                    }
                }
            }

            return null;
        }

        private static string GetNodeAttribute(XmlNode node, string attributeName)
        {
            if (node?.Attributes == null)
            {
                return null;
            }

            var attr = node.Attributes[attributeName];
            return attr?.Value;
        }
    }
}