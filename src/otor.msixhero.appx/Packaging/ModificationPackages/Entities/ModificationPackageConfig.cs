﻿using Otor.MsixHero.Appx.Packaging.Manifest.Enums;

namespace Otor.MsixHero.Appx.Packaging.ModificationPackages.Entities
{
    public class ModificationPackageConfig
    {
        public string ParentName { get; set; }

        public string ParentPublisher { get; set; }

        public string Name { get; set; }
        
        public string DisplayName { get; set; }

        public string DisplayPublisher { get; set; }

        public string Publisher { get; set; }

        public string Version { get; set; }

        public AppxPackageArchitecture Architecture { get; set; }
    }
}