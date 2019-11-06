﻿using System;
using System.Xml.Serialization;
using otor.msixhero.lib.BusinessLayer.State.Enums;

namespace otor.msixhero.lib.BusinessLayer.Commands.Grid
{
    [Serializable]
    public class SetPackageContext : BaseCommand<PackageContext>
    {
        public SetPackageContext()
        {

        }

        public SetPackageContext(PackageContext context, bool force = false)
        {
            this.Context = context;
            this.Force = force;
        }

        [XmlElement]
        public PackageContext Context { get; set; }

        [XmlElement]
        public bool Force { get; set; }
    }
}