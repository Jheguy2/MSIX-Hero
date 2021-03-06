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
using System.Threading;
using System.Threading.Tasks;
using Otor.MsixHero.App.Hero.Commands;
using Otor.MsixHero.App.Hero.Commands.Dashboard;
using Otor.MsixHero.App.Hero.Commands.EventViewer;
using Otor.MsixHero.App.Hero.Commands.Logs;
using Otor.MsixHero.App.Hero.Commands.Packages;
using Otor.MsixHero.App.Hero.Commands.Volumes;
using Otor.MsixHero.App.Hero.State;
using Otor.MsixHero.App.Modules;
using Otor.MsixHero.Appx.Diagnostic.Logging;
using Otor.MsixHero.Appx.Diagnostic.Logging.Entities;
using Otor.MsixHero.Appx.Diagnostic.RunningDetector;
using Otor.MsixHero.Appx.Packaging.Installation;
using Otor.MsixHero.Appx.Packaging.Installation.Entities;
using Otor.MsixHero.Appx.Packaging.Installation.Enums;
using Otor.MsixHero.Appx.Volumes;
using Otor.MsixHero.Appx.Volumes.Entities;
using Otor.MsixHero.Infrastructure.Configuration;
using Otor.MsixHero.Infrastructure.Helpers;
using Otor.MsixHero.Infrastructure.Processes.SelfElevation;
using Otor.MsixHero.Infrastructure.Processes.SelfElevation.Enums;
using Otor.MsixHero.Infrastructure.Progress;
using Otor.MsixHero.Infrastructure.Services;
using Prism.Events;
using Prism.Modularity;
using Prism.Regions;

namespace Otor.MsixHero.App.Hero.Executor
{
    public class MsixHeroCommandExecutor : BaseMsixHeroCommandExecutor, IObserver<ActivePackageFullNames>
    {
        private readonly IModuleManager moduleManager;
        private readonly IRegionManager regionManager;
        private readonly IEventAggregator eventAggregator;
        private readonly IConfigurationService configurationService;
        private readonly ISelfElevationProxyProvider<IAppxPackageManager> packageManagerProvider;
        private readonly ISelfElevationProxyProvider<IAppxLogManager> logManagerProvider;
        private readonly ISelfElevationProxyProvider<IAppxVolumeManager> volumeManagerProvider;
        private readonly IRunningAppsDetector detector;

        public MsixHeroCommandExecutor(
            IModuleManager moduleManager,
            IRegionManager regionManager,
            IEventAggregator eventAggregator,
            IConfigurationService configurationService,
            ISelfElevationProxyProvider<IAppxPackageManager> packageManagerProvider,
            ISelfElevationProxyProvider<IAppxLogManager> logManagerProvider,
            ISelfElevationProxyProvider<IAppxVolumeManager> volumeManagerProvider,
            IRunningAppsDetector detector) : base(eventAggregator)
        {
            this.moduleManager = moduleManager;
            this.regionManager = regionManager;
            this.eventAggregator = eventAggregator;
            this.configurationService = configurationService;
            this.packageManagerProvider = packageManagerProvider;
            this.logManagerProvider = logManagerProvider;
            this.volumeManagerProvider = volumeManagerProvider;
            this.detector = detector;

            detector.Subscribe(this);

            this.Handlers[typeof(SetToolFilterCommand)] = (command, token, progress) => this.SetToolFilter((SetToolFilterCommand)command);
            
            this.Handlers[typeof(SetEventViewerFilterCommand)] = (command, token, progress) => this.SetEventViewerFilter((SetEventViewerFilterCommand)command);
            this.Handlers[typeof(SetEventViewerSortingCommand)] = (command, token, progress) => this.SetEventViewerSorting((SetEventViewerSortingCommand)command);

            this.Handlers[typeof(GetVolumesCommand)] = (command, token, progress) => this.GetVolumes((GetVolumesCommand)command, token, progress);
            this.Handlers[typeof(SelectVolumesCommand)] = (command, token, progress) => this.SelectVolumes((SelectVolumesCommand)command);
            this.Handlers[typeof(SetVolumeFilterCommand)] = (command, token, progress) => this.SetVolumeFilter((SetVolumeFilterCommand)command);
            
            this.Handlers[typeof(GetPackagesCommand)] = (command, token, progress) => this.GetPackages((GetPackagesCommand)command, token, progress);
            this.Handlers[typeof(StopPackageCommand)] = (command, token, progress) => this.StopPackage((StopPackageCommand)command, token);
            this.Handlers[typeof(CheckForUpdatesCommand)] = (command, token, progress) => this.CheckForUpdates((CheckForUpdatesCommand)command, token);
            this.Handlers[typeof(SelectPackagesCommand)] = (command, token, progress) => this.SelectPackages((SelectPackagesCommand)command);

            this.Handlers[typeof(GetLogsCommand)] = (command, token, progress) => this.GetLogs((GetLogsCommand)command, token, progress);
            this.Handlers[typeof(SelectLogCommand)] = (command, token, progress) => this.SelectLog((SelectLogCommand)command);
            this.Handlers[typeof(OpenEventViewerCommand)] = (command, token, progress) => this.OpenEventViewer((OpenEventViewerCommand)command, token, progress);

            this.Handlers[typeof(SetPackageFilterCommand)] = (command, token, progress) => this.SetPackageFilter((SetPackageFilterCommand)command);
            this.Handlers[typeof(SetCurrentModeCommand)] = (command, token, progress) => this.SetCurrentMode((SetCurrentModeCommand)command);
            this.Handlers[typeof(SetPackageSortingCommand)] = (command, token, progress) => this.SetPackageSorting((SetPackageSortingCommand)command);
            this.Handlers[typeof(SetPackageGroupingCommand)] = (command, token, progress) => this.SetPackageGrouping((SetPackageGroupingCommand)command);
            this.Handlers[typeof(SetPackageSidebarVisibilityCommand)] = (command, token, progress) => this.SetPackageSidebarVisibility((SetPackageSidebarVisibilityCommand)command);
        }

        // ReSharper disable once UnusedParameter.Local
        private async Task<IList<Log>> GetLogs(GetLogsCommand command, CancellationToken cancellationToken, IProgress<ProgressData> progressData)
        {
            var manager = await this.logManagerProvider.GetProxyFor(SelfElevationLevel.HighestAvailable, cancellationToken).ConfigureAwait(false);
            return await manager.GetLogs(command.Count, cancellationToken, progressData).ConfigureAwait(false);
        }

        private async Task OpenEventViewer(OpenEventViewerCommand command, CancellationToken cancellationToken, IProgress<ProgressData> progressData)
        {
            var manager = await this.logManagerProvider.GetProxyFor(SelfElevationLevel.AsAdministrator, cancellationToken).ConfigureAwait(false);
            await manager.OpenEventViewer(command.Type, cancellationToken, progressData).ConfigureAwait(false);
        }

        private async Task<PackageGroup> SetPackageGrouping(SetPackageGroupingCommand command)
        {
            this.ApplicationState.Packages.GroupMode = command.GroupMode;

            var cleanConfig = await this.configurationService.GetCurrentConfigurationAsync(false).ConfigureAwait(false);
            cleanConfig.Packages ??= new PackagesConfiguration();
            cleanConfig.Packages.Group ??= new PackagesGroupConfiguration();
            cleanConfig.Packages.Group.GroupMode = this.ApplicationState.Packages.GroupMode;
            await this.configurationService.SetCurrentConfigurationAsync(cleanConfig).ConfigureAwait(false);
            return this.ApplicationState.Packages.GroupMode;
        }

        private async Task SetPackageSidebarVisibility(SetPackageSidebarVisibilityCommand command)
        {
            this.ApplicationState.Packages.ShowSidebar = command.IsVisible;

            var cleanConfig = await this.configurationService.GetCurrentConfigurationAsync(false).ConfigureAwait(false);
            cleanConfig.Packages ??= new PackagesConfiguration();
            cleanConfig.Packages.Sidebar ??= new SidebarListConfiguration();
            cleanConfig.Packages.Sidebar.Visible = this.ApplicationState.Packages.ShowSidebar;
            await this.configurationService.SetCurrentConfigurationAsync(cleanConfig).ConfigureAwait(false);
        }

        private async Task SetPackageSorting(SetPackageSortingCommand command)
        {
            this.ApplicationState.Packages.SortMode = command.SortMode;

            if (command.Descending.HasValue)
            {
                this.ApplicationState.Packages.SortDescending = command.Descending.Value;
            }
            else
            {
                this.ApplicationState.Packages.SortDescending = !this.ApplicationState.Packages.SortDescending;
            }

            var cleanConfig = await this.configurationService.GetCurrentConfigurationAsync(false).ConfigureAwait(false);
            cleanConfig.Packages ??= new PackagesConfiguration();
            cleanConfig.Packages.Sorting ??= new PackagesSortConfiguration();
            cleanConfig.Packages.Sorting.SortingMode = this.ApplicationState.Packages.SortMode;
            cleanConfig.Packages.Sorting.Descending = this.ApplicationState.Packages.SortDescending;
            await this.configurationService.SetCurrentConfigurationAsync(cleanConfig).ConfigureAwait(false);
        }

        private Task SetCurrentMode(SetCurrentModeCommand command)
        {
            this.ApplicationState.CurrentMode = command.NewMode;

            switch (command.NewMode)
            {
                case ApplicationMode.Packages:
                    this.moduleManager.LoadModule(ModuleNames.PackageManagement);
                    this.regionManager.Regions[RegionNames.Main].RequestNavigate(NavigationPaths.PackageManagement);
                    this.regionManager.Regions[RegionNames.Search].RequestNavigate(NavigationPaths.PackageManagementPaths.Search);
                    break;
                case ApplicationMode.Dashboard:
                    this.moduleManager.LoadModule(ModuleNames.Dashboard);
                    this.regionManager.Regions[RegionNames.Main].RequestNavigate(NavigationPaths.Dashboard);
                    this.regionManager.Regions[RegionNames.Search].RequestNavigate(NavigationPaths.DashboardPaths.Search);
                    break;
                case ApplicationMode.WhatsNew:
                    this.moduleManager.LoadModule(ModuleNames.WhatsNew);
                    this.regionManager.Regions[RegionNames.Main].RequestNavigate(NavigationPaths.WhatsNew);
                    this.regionManager.Regions[RegionNames.Search].RequestNavigate(NavigationPaths.Empty);
                    break;
                case ApplicationMode.VolumeManager:
                    this.moduleManager.LoadModule(ModuleNames.VolumeManagement);
                    this.regionManager.Regions[RegionNames.Main].RequestNavigate(NavigationPaths.VolumeManagement);
                    this.regionManager.Regions[RegionNames.Search].RequestNavigate(NavigationPaths.VolumeManagementPaths.Search);
                    break;
                case ApplicationMode.SystemStatus:
                    this.moduleManager.LoadModule(ModuleNames.SystemStatus);
                    this.regionManager.Regions[RegionNames.Main].RequestNavigate(NavigationPaths.SystemStatus);
                    this.regionManager.Regions[RegionNames.Search].RequestNavigate(NavigationPaths.Empty);
                    break;
                case ApplicationMode.EventViewer:
                    this.moduleManager.LoadModule(ModuleNames.EventViewer);
                    this.regionManager.Regions[RegionNames.Main].RequestNavigate(NavigationPaths.EventViewer);
                    this.regionManager.Regions[RegionNames.Search].RequestNavigate(NavigationPaths.EventViewerPaths.Search);
                    break;
            }

            return Task.FromResult(true);
        }

        private async Task SetPackageFilter(SetPackageFilterCommand command)
        {
            this.ApplicationState.Packages.Filter = command.Filter;
            this.ApplicationState.Packages.SearchKey = command.SearchKey;

            var cleanConfig = await this.configurationService.GetCurrentConfigurationAsync(false).ConfigureAwait(false);
            cleanConfig.Packages ??= new PackagesConfiguration();
            cleanConfig.Packages.Filter ??= new PackagesFilterConfiguration();
            cleanConfig.Packages.Filter.Filter = command.Filter;
            await this.configurationService.SetCurrentConfigurationAsync(cleanConfig).ConfigureAwait(false);
        }

        private Task SetVolumeFilter(SetVolumeFilterCommand command)
        {
            this.ApplicationState.Volumes.SearchKey = command.SearchKey;
            return Task.FromResult(true);
        }

        private Task SetToolFilter(SetToolFilterCommand command)
        {
            this.ApplicationState.Dashboard.SearchKey = command.SearchKey;
            return Task.FromResult(true);
        }

        private async Task SetEventViewerSorting(SetEventViewerSortingCommand command)
        {
            this.ApplicationState.EventViewer.SortMode = command.SortMode;

            if (command.Descending.HasValue)
            {
                this.ApplicationState.EventViewer.SortDescending = command.Descending.Value;
            }
            else
            {
                this.ApplicationState.EventViewer.SortDescending = !this.ApplicationState.EventViewer.SortDescending;
            }

            var cleanConfig = await this.configurationService.GetCurrentConfigurationAsync(false).ConfigureAwait(false);
            cleanConfig.Events ??= new EventsConfiguration();
            cleanConfig.Events.Sorting ??= new EventsSortConfiguration();
            cleanConfig.Events.Sorting.SortingMode = this.ApplicationState.EventViewer.SortMode;
            cleanConfig.Events.Sorting.Descending = this.ApplicationState.EventViewer.SortDescending;
            await this.configurationService.SetCurrentConfigurationAsync(cleanConfig).ConfigureAwait(false);
        }

        private async Task SetEventViewerFilter(SetEventViewerFilterCommand command)
        {
            this.ApplicationState.EventViewer.SearchKey = command.SearchKey;
            this.ApplicationState.EventViewer.Filter = command.Filter;

            var cleanConfig = await this.configurationService.GetCurrentConfigurationAsync(false).ConfigureAwait(false);
            cleanConfig.Events ??= new EventsConfiguration();
            cleanConfig.Events.Filter ??= new EventsFilterConfiguration();
            cleanConfig.Events.Filter.Filter = command.Filter;
            
            await this.configurationService.SetCurrentConfigurationAsync(cleanConfig).ConfigureAwait(false);
        }

        private Task<IList<AppxVolume>> SelectVolumes(SelectVolumesCommand command)
        {
            IList<AppxVolume> selected;
            if (!command.SelectedVolumePaths.Any())
            {
                selected = new List<AppxVolume>();
            }
            else if (command.SelectedVolumePaths.Count == 1)
            {
                var singleSelection = this.ApplicationState.Volumes.AllVolumes.FirstOrDefault(a => string.Equals(a.PackageStorePath, command.SelectedVolumePaths[0], StringComparison.OrdinalIgnoreCase));
                if (singleSelection != null)
                {
                    selected = new List<AppxVolume> { singleSelection };
                }
                else
                {
                    selected = new List<AppxVolume>();
                }
            }
            else
            {
                selected = this.ApplicationState.Volumes.AllVolumes.Where(a => command.SelectedVolumePaths.Contains(a.PackageStorePath)).ToList();
            }

            this.ApplicationState.Volumes.SelectedVolumes.Clear();
            this.ApplicationState.Volumes.SelectedVolumes.AddRange(selected);

            return Task.FromResult(selected);
        }

        private async Task StopPackage(StopPackageCommand command, CancellationToken cancellationToken)
        {
            var manager = await this.packageManagerProvider.GetProxyFor(SelfElevationLevel.HighestAvailable, cancellationToken).ConfigureAwait(false);
            await manager.Stop(command.Package.PackageId, cancellationToken).ConfigureAwait(false);
        }

        private async Task<AppInstallerUpdateAvailabilityResult> CheckForUpdates(CheckForUpdatesCommand command, CancellationToken cancellationToken)
        {
            var manager = await this.packageManagerProvider.GetProxyFor(SelfElevationLevel.AsInvoker, cancellationToken).ConfigureAwait(false);
            return await manager.CheckForUpdates(command.PackageFullName, cancellationToken).ConfigureAwait(false);
        }

        private Task<Log> SelectLog(SelectLogCommand command)
        {
            this.ApplicationState.EventViewer.SelectedLog = command.SelectedLog;
            return Task.FromResult(command.SelectedLog);
        }

        private Task<IList<InstalledPackage>> SelectPackages(SelectPackagesCommand command)
        {
            IList<InstalledPackage> selected;
            if (!command.SelectedManifestPaths.Any())
            {
                selected = new List<InstalledPackage>();
            }
            else if (command.SelectedManifestPaths.Count == 1)
            {
                try
                {
                    this.packageListSynchronizer.EnterReadLock();
                    var singleSelection = this.ApplicationState.Packages.AllPackages.FirstOrDefault(a => string.Equals(a.ManifestLocation, command.SelectedManifestPaths[0], StringComparison.OrdinalIgnoreCase));
                    selected = singleSelection != null ? new List<InstalledPackage> { singleSelection } : new List<InstalledPackage>();
                }
                finally
                {
                    this.packageListSynchronizer.ExitReadLock();
                }
            }
            else
            {
                try
                {
                    this.packageListSynchronizer.EnterReadLock();
                    selected = this.ApplicationState.Packages.AllPackages.Where(a => command.SelectedManifestPaths.Contains(a.ManifestLocation)).ToList();
                }
                finally
                {
                    this.packageListSynchronizer.ExitReadLock();
                }
            }

            this.ApplicationState.Packages.SelectedPackages.Clear();
            this.ApplicationState.Packages.SelectedPackages.AddRange(selected);
            
            return Task.FromResult(selected);
        }

        private readonly ReaderWriterLockSlim packageListSynchronizer = new ReaderWriterLockSlim();

        private async Task<IList<InstalledPackage>> GetPackages(GetPackagesCommand command, CancellationToken cancellationToken, IProgress<ProgressData> progressData)
        {
            this.detector.StartListening();

            var level = SelfElevationLevel.HighestAvailable;

            if (command.FindMode == PackageFindMode.AllUsers)
            {
                level = SelfElevationLevel.AsAdministrator;
            }

            var manager = await this.packageManagerProvider.GetProxyFor(level, cancellationToken).ConfigureAwait(false);

            PackageFindMode mode;
            if (command.FindMode.HasValue)
            {
                mode = command.FindMode.Value;
                if (mode == PackageFindMode.Auto)
                {
                    var isAdmin = await UserHelper.IsAdministratorAsync(cancellationToken);
                    mode = isAdmin ? PackageFindMode.AllUsers : PackageFindMode.CurrentUser;
                }
            }
            else
            {
                switch (this.ApplicationState.Packages.Mode)
                {
                    case PackageContext.CurrentUser:
                        mode = PackageFindMode.CurrentUser;
                        break;
                    case PackageContext.AllUsers:
                        mode = PackageFindMode.AllUsers;
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            var selected = this.ApplicationState.Packages.SelectedPackages.Select(p => p.ManifestLocation).ToArray();
            var results = await manager.GetInstalledPackages(mode, cancellationToken, progressData).ConfigureAwait(false);

            // this.packageListSynchronizer.EnterWriteLock();
            this.ApplicationState.Packages.AllPackages.Clear();
            this.ApplicationState.Packages.AllPackages.AddRange(results);
            
            foreach (var item in this.detector.GetCurrentlyRunning(this.ApplicationState.Packages.AllPackages))
            {
                item.IsRunning = true;
            }

            switch (mode)
            {
                case PackageFindMode.CurrentUser:
                    this.ApplicationState.Packages.Mode = PackageContext.CurrentUser;
                    break;
                case PackageFindMode.AllUsers:
                    this.ApplicationState.Packages.Mode = PackageContext.AllUsers;
                    break;
            }

            switch (mode)
            {
                case PackageFindMode.CurrentUser:
                    this.ApplicationState.Packages.Mode = PackageContext.CurrentUser;
                    break;
                case PackageFindMode.AllUsers:
                    this.ApplicationState.Packages.Mode = PackageContext.AllUsers;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            await this.Invoke(this, new SelectPackagesCommand(selected), cancellationToken).ConfigureAwait(false);

            return results;
        }

        // ReSharper disable once UnusedParameter.Local
        private async Task<IList<AppxVolume>> GetVolumes(GetVolumesCommand command, CancellationToken cancellationToken, IProgress<ProgressData> progressData)
        {
            var manager = await this.volumeManagerProvider.GetProxyFor(SelfElevationLevel.AsInvoker, cancellationToken).ConfigureAwait(false);

            var selected = this.ApplicationState.Volumes.SelectedVolumes.Select(p => p.PackageStorePath).ToArray();

            var results = await manager.GetAll(cancellationToken, progressData).ConfigureAwait(false);
            var defaultVol = await manager.GetDefault(cancellationToken).ConfigureAwait(false);

            var findMe = defaultVol == null ? null : results.FirstOrDefault(d => d.PackageStorePath == defaultVol.PackageStorePath);
            if (findMe != null)
            {
                findMe.IsDefault = true;
            }

            this.ApplicationState.Volumes.AllVolumes.Clear();
            this.ApplicationState.Volumes.AllVolumes.AddRange(results);
            
            if (selected.Any())
            {
                await this.Invoke(this, new SelectVolumesCommand(selected), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await this.Invoke(this, new SelectVolumesCommand(new string[0]), cancellationToken).ConfigureAwait(false);
            }

            return results;
        }

        void IObserver<ActivePackageFullNames>.OnCompleted()
        {
        }

        void IObserver<ActivePackageFullNames>.OnError(Exception error)
        {
        }

        void IObserver<ActivePackageFullNames>.OnNext(ActivePackageFullNames value)
        {
            this.ApplicationState.Packages.ActivePackageNames = value.Running;
            this.eventAggregator.GetEvent<PubSubEvent<ActivePackageFullNames>>().Publish(value);
        }
    }
}