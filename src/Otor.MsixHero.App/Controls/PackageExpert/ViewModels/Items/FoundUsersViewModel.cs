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

using System.Collections.Generic;
using Otor.MsixHero.App.Mvvm;
using Otor.MsixHero.Appx.Users;
using Otor.MsixHero.Lib.Domain.State;

namespace Otor.MsixHero.App.Controls.PackageExpert.ViewModels.Items
{
    public class FoundUsersViewModel : NotifyPropertyChanged
    {
        public FoundUsersViewModel(List<User> listOfInstalls, ElevationStatus status)
        {
            this.InstalledBy = listOfInstalls;
            this.IsElevated = status == ElevationStatus.OK;
        }

        public bool IsElevated { get; }

        public List<User> InstalledBy { get; }
    }
}
