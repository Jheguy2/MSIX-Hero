﻿// MSIX Hero
// Copyright (C) 2021 Marcin Otorowski
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// Full notice:
// https://github.com/marcinotorowski/msix-hero/blob/develop/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Otor.MsixHero.App.Helpers
{
    /// <summary>
    /// A builder for dialog filters in Windows style.
    /// </summary>
    public class DialogFilterBuilder
    {
        private readonly HashSet<string> filters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        public DialogFilterBuilder(params string[] extensions)
        {
            this.AddFilters(extensions);
        }
        
        public void AddFilters(params string[] extensions)
        {
            foreach (var ext in extensions)
            {
                // ReSharper disable once StringLiteralTypo
                if (string.Equals(ext, "appxmanifest.xml", StringComparison.OrdinalIgnoreCase))
                {
                    // ReSharper disable once StringLiteralTypo
                    this.filters.Add("appxmanifest.xml");
                    continue;
                }
                
                this.filters.Add(ext);
            }
        }
        
        public string BuildFilter(bool includeAllSupported = true, bool includeAll = true)
        {
            var filter = string.Join('|', new[]
            {
                this.BuildPackagesFilter(),
                this.BuildBundles(),
                this.BuildManifestFilter(),
                this.BuildWinGet(),
                this.BuildAppInstaller(),
                this.BuildCertificateFiles(),
                this.BuildRegistry(),
                this.BuildLegacyInstallers(),
                this.BuildOtherFiles()
            }.Where(s => !string.IsNullOrEmpty(s)));
            
            if (!includeAll && !includeAllSupported)
            {
                return filter;
            }    
            
            var sections = filter.Split('|');
            if (sections.Length == 0)
            {
                return "All files|*.*";
            }

            var allExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 1; i < sections.Length; i += 2)
            {
                var extensions = sections[i].Split(';');
                foreach (var extension in extensions)
                {
                    allExtensions.Add(extension);
                }
            }
            
            if (includeAllSupported && allExtensions.Count > 1 && sections.Length / 2 > 1)
            {
                // if there is just one category then it makes no sense to show all supported files extra
                filter = "All supported files|" + string.Join(';', allExtensions.OrderBy(e => e)) + "|" + filter;
            }

            if (includeAll && !allExtensions.Contains("*.*"))
            {
                if (filter.Length > 0)
                {
                    filter += "|All files|*.*";
                }
                else
                {
                    filter = "All files|*.*";
                }
            }

            return filter;
        }

        private string BuildLegacyInstallers()
        {
            var msi = this.filters.Contains("*.msi");
            var exe = this.filters.Contains("*.exe");
            
            if (msi)
            {
                if (exe)
                {
                    return "Windows Installer|*.msi|Executable files|*.exe";
                }

                return "Windows Installer|*.msi";
            }

            if (exe)
            {
                return "Executable files|*.exe";
            }

            return null;
        }
        
        private string BuildOtherFiles()
        {
            var otherExtensions = this.filters.Select(filter => 
            {
                switch (filter.ToLowerInvariant())
                {
                    // ReSharper disable once StringLiteralTypo
                    case "appxmanifest.xml":
                    case "*.msix":
                    case "*.appx":
                    case "*.yaml":
                    // ReSharper disable once StringLiteralTypo
                    case "*.appinstaller":
                    case "*.reg":
                    case "*.cer":
                    case "*.exe":
                    case "*.msi":
                    case "*.pfx":
                    // ReSharper disable once StringLiteralTypo
                    case "*.appxbundle":
                    // ReSharper disable once StringLiteralTypo
                    case "*.msixbundle":
                        return null;
                    default:
                        return filter;
                }
            }).Where(ext => ext != null).OrderBy(ext => ext);
            
            return string.Join('|', otherExtensions.Select(e => $"{e.TrimStart('*').TrimStart('.').ToUpperInvariant()} files|*.{e}"));
        }

        private string BuildPackagesFilter()
        {
            var msix = this.filters.Contains("*.msix");
            var appx = this.filters.Contains("*.appx");
            
            var package = msix || appx;
            if (!package)
            {
                return string.Empty;
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.Append("Packages|");
            if (msix)
            {
                stringBuilder.Append("*.msix;");
            }

            if (appx)
            {
                stringBuilder.Append("*.appx;");
            }

            return stringBuilder.ToString().TrimEnd(';');
        }

        private string BuildManifestFilter()
        {
            // ReSharper disable once StringLiteralTypo
            return this.filters.Contains("AppxManifest.xml") ? "Manifest files|appxmanifest.xml" : null;
        }

        private string BuildBundles()
        {
            var msixBundle = this.filters.Contains("*.MsixBundle");
            var appxBundle = this.filters.Contains("*.AppxBundle");
            
            var bundle = msixBundle || appxBundle;
            if (!bundle)
            {
                return string.Empty;
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.Append("Bundles|");
            
            if (msixBundle)
            {
                // ReSharper disable once StringLiteralTypo
                stringBuilder.Append("*.msixbundle;");
            }

            if (appxBundle)
            {
                // ReSharper disable once StringLiteralTypo
                stringBuilder.Append("*.appxbundle;");
            }

            return stringBuilder.ToString().TrimEnd(';');
        }

        private string BuildWinGet()
        {
            // ReSharper disable once StringLiteralTypo
            return this.filters.Contains("*.yaml") ? "Winget manifests|*.yaml" : null;
        }

        private string BuildCertificateFiles()
        {
            var pfx = this.filters.Contains("*.pfx");
            var cer = this.filters.Contains("*.cer");

            if (!pfx && !cer)
            {
                return null;
            }

            var stringBuilder = new StringBuilder("Certificates|");

            if (pfx)
            {
                stringBuilder.Append("*.pfx;");
            }

            if (cer)
            {
                stringBuilder.Append("*.cer;");
            }

            return stringBuilder.ToString().TrimEnd(';');
        }

        private string BuildRegistry()
        {
            return this.filters.Contains("*.reg") ? "Windows Registry Files|*.reg" : null;
        }
        
        private string BuildAppInstaller()
        {
            // ReSharper disable once StringLiteralTypo
            return this.filters.Contains("*.AppInstaller") ? "App installer|*.appinstaller" : null;
        }
    }
}