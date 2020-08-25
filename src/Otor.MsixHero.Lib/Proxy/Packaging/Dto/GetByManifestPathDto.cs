﻿using System;
using System.Xml.Serialization;
using Otor.MsixHero.Appx.Packaging.Installation.Enums;
using Otor.MsixHero.Appx.Packaging.Manifest.Entities;
using Otor.MsixHero.Lib.Domain.Commands;

namespace Otor.MsixHero.Lib.Proxy.Packaging.Dto
{
    [Serializable]
    public class GetByManifestPathDto : ProxyObject<AppxPackage>
    {
        public GetByManifestPathDto()
        {
        }

        public GetByManifestPathDto(string fullName, PackageContext context = PackageContext.CurrentUser)
        {
            this.Context = context;
            this.Source = fullName;
        }
        
        [XmlElement]
        public string Source { get; set; }
        

        [XmlElement]
        public PackageContext Context { get; set; }
    }
}
