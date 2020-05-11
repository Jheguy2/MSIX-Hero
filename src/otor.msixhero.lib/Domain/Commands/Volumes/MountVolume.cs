﻿using System.Printing;
using otor.msixhero.lib.Domain.Appx.Volume;

namespace otor.msixhero.lib.Domain.Commands.Volumes
{
    public class MountVolume : VoidCommand
    {
        public MountVolume()
        {
        }

        public MountVolume(string volumeName)
        {
            this.Name = volumeName;
        }

        public MountVolume(AppxVolume volume) : this(volume.Name)
        {
        }

        public string Name { get; set; }
    }
}