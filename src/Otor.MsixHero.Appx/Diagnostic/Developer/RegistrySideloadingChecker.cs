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
using System.Diagnostics;
using Microsoft.Win32;
using Otor.MsixHero.Appx.Diagnostic.Developer.Enums;

namespace Otor.MsixHero.Appx.Diagnostic.Developer
{
    public class RegistrySideloadingChecker : ISideloadingChecker
    {
        public WindowsStoreAutoDownload GetStoreAutoDownloadStatus()
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = key.OpenSubKey(@"SOFTWARE\Policies\Microsoft\WindowsStore");

            if (subKey == null)
            {
                return WindowsStoreAutoDownload.Default;
            }

            var allowAllTrustedApps = subKey.GetValue("AutoDownload");

            if (allowAllTrustedApps == null)
            {
                return WindowsStoreAutoDownload.Default;
            }

            var dword = allowAllTrustedApps is int dwordValue1 ? dwordValue1 : 0;

            switch (dword)
            {
                case 2:
                    return WindowsStoreAutoDownload.Never;
                case 4:
                    return WindowsStoreAutoDownload.Always;
                default:
                    return WindowsStoreAutoDownload.Default;
            }
        }

        public bool SetStoreAutoDownloadStatus(WindowsStoreAutoDownload status)
        {
            var newValue = (int)status;
            string cmd;

            switch (newValue)
            {
                case 4:
                case 2:
                    if ((int)this.GetStoreAutoDownloadStatus() == newValue)
                    {
                        return true;
                    }

                    cmd = @"ADD HKLM\SOFTWARE\Policies\Microsoft\WindowsStore /v AutoDownload /t REG_DWORD /f /d " + newValue;
                    break;

                default:
                    if (this.GetStoreAutoDownloadStatus() == WindowsStoreAutoDownload.Default)
                    {
                        return true;
                    }

                    cmd = @"DELETE HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore /f /v AutoDownload";
                    break;
            }

            var psi = new ProcessStartInfo("reg.exe")
            {
                Arguments = cmd,
                UseShellExecute = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
                Verb = "runas",
#if !DEBUG
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
#endif
            };

            try
            {
                var p = Process.Start(psi);
                if (p == null)
                {
                    return false;
                }

                if (!p.WaitForExit(2000))
                {
                    return false;
                }

                if (p.ExitCode != 0)
                {
                    return false;
                }

            }
            catch (Exception)
            {
                return false;
            }

            return this.GetStoreAutoDownloadStatus() == status;
        }

        public bool SetStatus(SideloadingStatus status)
        {
            var allowSideloadDword = status != SideloadingStatus.NotAllowed ? 1 : 0;
            var allowDevModeDword = status == SideloadingStatus.DeveloperMode ? 1 : 0;

            var cmd1 = $@"reg.exe add HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock /t REG_DWORD /f /v AllowAllTrustedApps /d {allowSideloadDword}";
            var cmd2 = $@"reg.exe add HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock /t REG_DWORD /f /v AllowDevelopmentWithoutDevLicense /d {allowDevModeDword}";

            var psi = new ProcessStartInfo("cmd.exe")
            {
                Arguments = "/c \"" + cmd1 + " && " + cmd2 + "\"",
                UseShellExecute = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
                Verb = "runas",
#if !DEBUG
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
#endif
            };

            try
            {
                var p = Process.Start(psi);
                if (p == null)
                {
                    return false;
                }

                if (!p.WaitForExit(2000))
                {
                    return false;
                }

                if (p.ExitCode != 0)
                {
                    return false;
                }

            }
            catch (Exception)
            {
                return false;
            }

            return this.GetStatus() == status;
        }

        public SideloadingStatus GetStatus()
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = key.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");

            if (subKey == null)
            {
                return SideloadingStatus.NotAllowed;
            }

            var allowAllTrustedApps = subKey.GetValue("AllowAllTrustedApps");

            if (allowAllTrustedApps == null)
            {
                return SideloadingStatus.NotAllowed;
            }

            var dword1 = allowAllTrustedApps is int dwordValue1 ? dwordValue1 : 0;

            if (dword1 == 0)
            {
                return SideloadingStatus.NotAllowed;
            }

            var allowDevelopmentWithoutDevLicense = subKey.GetValue("AllowDevelopmentWithoutDevLicense");
            if (allowDevelopmentWithoutDevLicense == null)
            {
                return SideloadingStatus.Sideloading;
            }

            var dword2 = allowDevelopmentWithoutDevLicense is int dwordValue2 ? dwordValue2 : 0;
            if (dword2 == 0)
            {
                return SideloadingStatus.Sideloading;
            }

            return SideloadingStatus.DeveloperMode;
        }
    }
}