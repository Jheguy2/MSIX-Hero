﻿using System;
using System.Xml.Serialization;
using otor.msixhero.lib.Domain.Appx.Packages;

namespace otor.msixhero.lib.Domain.Commands.Developer
{
    [Serializable]
    public class RunToolInPackage : BaseCommand
    {
        public RunToolInPackage(string packagePackageFamilyName, string packageName, string toolPath, string arguments = null)
        {
            this.PackageFamilyName = packagePackageFamilyName;
            this.AppId = packageName;
            this.ToolPath = toolPath;
            this.Arguments = arguments;
        }

        public RunToolInPackage(Package package, string toolPath, string arguments = null) : this(package.PackageFamilyName, package.Name, toolPath, arguments)
        {
        }

        public RunToolInPackage()
        {
        }

        [XmlElement]
        public string PackageFamilyName { get; set; }

        [XmlElement]
        public string AppId { get; set; }
        
        [XmlElement]
        public string ToolPath { get; set; }
        
        [XmlElement]
        public string Arguments { get; set; }
    }
}