﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using Otor.MsixHero.App.Helpers;
using Otor.MsixHero.App.Hero;
using Otor.MsixHero.App.Hero.Commands.Packages;
using Otor.MsixHero.App.Hero.Events.Base;
using Otor.MsixHero.App.Hero.Executor;
using Otor.MsixHero.App.Modules.PackageManagement.ViewModels.Items;
using Otor.MsixHero.App.Mvvm;
using Otor.MsixHero.Appx.Diagnostic.RunningDetector;
using Otor.MsixHero.Appx.Packaging.Installation;
using Otor.MsixHero.Appx.Packaging.Installation.Entities;
using Otor.MsixHero.Infrastructure.Configuration;
using Otor.MsixHero.Infrastructure.Services;
using Otor.MsixHero.Lib.Infrastructure.Progress;
using Prism;
using Prism.Events;
using Prism.Regions;

namespace Otor.MsixHero.App.Modules.PackageManagement.ViewModels
{
    public class PackagesListViewModel : NotifyPropertyChanged, INavigationAware, IActiveAware
    {
        private readonly IBusyManager busyManager;
        private readonly IMsixHeroApplication application;
        private readonly IInteractionService interactionService;
        private readonly ReaderWriterLockSlim packagesSync = new ReaderWriterLockSlim();
        private bool firstRun = true;
        private bool isActive;

        public PackagesListViewModel(
            IBusyManager busyManager,
            IMsixHeroApplication application,
            IInteractionService interactionService)
        {
            this.busyManager = busyManager;
            this.application = application;
            this.interactionService = interactionService;
            this.Items = new ObservableCollection<InstalledPackageViewModel>();
            this.ItemsCollection = CollectionViewSource.GetDefaultView(this.Items);
            this.ItemsCollection.Filter = row => this.IsPackageVisible((InstalledPackageViewModel)row);

            // reloading packages
            this.application.EventAggregator.GetEvent<UiExecutingEvent<GetPackagesCommand>>().Subscribe(this.OnGetPackagesExecuting);
            this.application.EventAggregator.GetEvent<UiExecutedEvent<GetPackagesCommand, IList<InstalledPackage>>>().Subscribe(this.OnGetPackagesExecuted, ThreadOption.UIThread);
            this.application.EventAggregator.GetEvent<UiFailedEvent<GetPackagesCommand>>().Subscribe(this.OnGetPackagesFailed);
            this.application.EventAggregator.GetEvent<UiCancelledEvent<GetPackagesCommand>>().Subscribe(this.OnGetPackagesCancelled);

            // filtering
            this.application.EventAggregator.GetEvent<UiExecutedEvent<SetPackageFilterCommand>>().Subscribe(this.OnSetPackageFilterCommand, ThreadOption.UIThread);

            // sorting ang grouping
            this.application.EventAggregator.GetEvent<UiExecutedEvent<SetPackageSortingCommand>>().Subscribe(this.OnSetPackageSorting, ThreadOption.UIThread);
            this.application.EventAggregator.GetEvent<UiExecutedEvent<SetPackageGroupingCommand>>().Subscribe(this.OnSetPackageGrouping, ThreadOption.UIThread);

            // is running indicator
            this.application.EventAggregator.GetEvent<PubSubEvent<ActivePackageFullNames>>().Subscribe(this.OnActivePackageIndication);
            this.application.EventAggregator.GetEvent<PubSubEvent<ActivePackageFullNames>>().Subscribe(this.OnActivePackageIndicationFinished, ThreadOption.UIThread);

            this.busyManager.StatusChanged += BusyManagerOnStatusChanged;
            this.SetSortingAndGrouping();
        }

        public string SearchKey
        {
            get => this.application.ApplicationState.Packages.SearchKey;
            set => this.application.CommandExecutor.Invoke(this, new SetPackageFilterCommand(this.application.CommandExecutor.ApplicationState.Packages.PackageFilter, value));
        }

        public bool IsActive
        {
            get => this.isActive;
            set
            {
                if (!this.SetField(ref this.isActive, value))
                {
                    return;
                }

                this.IsActiveChanged?.Invoke(this, new EventArgs());

                if (firstRun)
                {
                    this.application
                        .CommandExecutor
                        .WithBusyManager(this.busyManager, OperationType.PackageLoading)
                        .WithErrorHandling(this.interactionService, true)
                        .Invoke(this, new GetPackagesCommand(PackageFindMode.Auto));
                }

                this.firstRun = false;
            }
        }

        public event EventHandler IsActiveChanged;

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }

        public ProgressProperty Progress { get; } = new ProgressProperty();

        public ICollectionView ItemsCollection { get; }

        public ObservableCollection<InstalledPackageViewModel> Items { get; }

        private void BusyManagerOnStatusChanged(object sender, IBusyStatusChange e)
        {
            if (e.Type != OperationType.PackageLoading && e.Type != OperationType.Other)
            {
                return;
            }

            this.Progress.IsLoading = e.IsBusy;
            this.Progress.Message = e.Message;
            this.Progress.Progress = e.Progress;
        }

        private void OnGetPackagesExecuting(UiExecutingPayload<GetPackagesCommand> eventPayload)
        {
        }

        private void OnGetPackagesCancelled(UiCancelledPayload<GetPackagesCommand> eventPayload)
        {
        }

        private void OnGetPackagesFailed(UiFailedPayload<GetPackagesCommand> eventPayload)
        {
        }

        private void OnSetPackageFilterCommand(UiExecutedPayload<SetPackageFilterCommand> obj)
        {
            this.OnPropertyChanged(nameof(SearchKey));
            this.ItemsCollection.Refresh();
        }

        private void OnGetPackagesExecuted(UiExecutedPayload<GetPackagesCommand, IList<InstalledPackage>> eventPayload)
        {
            this.Items.Clear();

            foreach (var item in eventPayload.Result)
            {
                this.Items.Add(new InstalledPackageViewModel(item));
            }
        }

        private bool IsPackageVisible(InstalledPackageViewModel item)
        {
            var signatureFlags = this.application.ApplicationState.Packages.PackageFilter & PackageFilter.AllSources;
            if (signatureFlags != 0 && signatureFlags != PackageFilter.AllSources)
            {
                switch (item.SignatureKind)
                {
                    case SignatureKind.Developer:
                    case SignatureKind.Unsigned:
                    case SignatureKind.Enterprise:
                        if ((signatureFlags & PackageFilter.Developer) == 0)
                        {
                            return false;
                        }

                        break;
                    case SignatureKind.Store:
                        if ((signatureFlags & PackageFilter.Store) == 0)
                        {
                            return false;
                        }

                        break;
                    case SignatureKind.System:
                        if ((signatureFlags & PackageFilter.System) == 0)
                        {
                            return false;
                        }

                        break;
                }
            }

            var addonFlags = this.application.ApplicationState.Packages.PackageFilter & PackageFilter.MainAppsAndAddOns;
            if (addonFlags != 0 && addonFlags != PackageFilter.MainAppsAndAddOns)
            {
                if (item.IsAddon)
                {
                    if ((addonFlags & PackageFilter.Addons) == 0)
                    {
                        return false;
                    }
                }
                else
                {
                    if ((addonFlags & PackageFilter.MainApps) == 0)
                    {
                        return false;
                    }
                }
            }

            var archFilter = this.application.ApplicationState.Packages.PackageFilter & PackageFilter.AllArchitectures;
            if (archFilter != PackageFilter.AllArchitectures && archFilter != 0)
            {
                switch (item.Model.Architecture?.ToLowerInvariant())
                {
                    case "x86":
                        {
                            if ((archFilter & PackageFilter.x86) == 0)
                            {
                                return false;
                            }

                            break;
                        }
                    case "x64":
                        {
                            if ((archFilter & PackageFilter.x64) == 0)
                            {
                                return false;
                            }

                            break;
                        }
                    case "arm":
                        {
                            if ((archFilter & PackageFilter.Arm) == 0)
                            {
                                return false;
                            }

                            break;
                        }
                    case "arm64":
                        {
                            if ((archFilter & PackageFilter.Arm64) == 0)
                            {
                                return false;
                            }

                            break;
                        }
                    case "neutral":
                        {
                            if ((archFilter & PackageFilter.Neutral) == 0)
                            {
                                return false;
                            }

                            break;
                        }
                }
            }

            var isRunningFilter = this.application.ApplicationState.Packages.PackageFilter & PackageFilter.InstalledAndRunning;
            if (isRunningFilter == PackageFilter.Running)
            {
                if (!item.IsRunning)
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(this.application.ApplicationState.Packages.SearchKey) && item.DisplayName.IndexOf(this.application.ApplicationState.Packages.SearchKey, StringComparison.OrdinalIgnoreCase) == -1
                   && item.DisplayPublisherName.IndexOf(this.application.ApplicationState.Packages.SearchKey, StringComparison.OrdinalIgnoreCase) == -1
                   && item.Version.IndexOf(this.application.ApplicationState.Packages.SearchKey, StringComparison.OrdinalIgnoreCase) == -1
                   && item.Architecture.IndexOf(this.application.ApplicationState.Packages.SearchKey, StringComparison.OrdinalIgnoreCase) == -1)
            {
                return false;
            }

            return true;
        }

        private void OnActivePackageIndication(ActivePackageFullNames obj)
        {
            try
            {
                this.packagesSync.EnterReadLock();

                foreach (var item in this.Items)
                {
                    item.IsRunning = obj.Running.Contains(item.ProductId);
                }
            }
            finally
            {
                this.packagesSync.ExitReadLock();
            }
        }

        private void OnActivePackageIndicationFinished(ActivePackageFullNames obj)
        {
            this.ItemsCollection.Refresh();
        }

        private void OnSetPackageGrouping(UiExecutedPayload<SetPackageGroupingCommand> obj)
        {
            this.SetSortingAndGrouping();
        }

        private void OnSetPackageSorting(UiExecutedPayload<SetPackageSortingCommand> obj)
        {
            this.SetSortingAndGrouping();
        }

        private void SetSortingAndGrouping()
        {
            var currentSort = this.application.ApplicationState.Packages.SortMode;
            var currentSortDescending = this.application.ApplicationState.Packages.SortDescending;
            var currentGroup = this.application.ApplicationState.Packages.GroupMode;

            using (this.ItemsCollection.DeferRefresh())
            {
                string sortProperty;
                string groupProperty;

                switch (currentSort)
                {
                    case PackageSort.Name:
                        sortProperty = nameof(InstalledPackageViewModel.DisplayName);
                        break;
                    case PackageSort.Publisher:
                        sortProperty = nameof(InstalledPackageViewModel.DisplayPublisherName);
                        break;
                    case PackageSort.Architecture:
                        sortProperty = nameof(InstalledPackageViewModel.Architecture);
                        break;
                    case PackageSort.InstallDate:
                        sortProperty = nameof(InstalledPackageViewModel.InstallDate);
                        break;
                    case PackageSort.Type:
                        sortProperty = nameof(InstalledPackageViewModel.Type);
                        break;
                    case PackageSort.Version:
                        sortProperty = nameof(InstalledPackageViewModel.Version);
                        break;
                    case PackageSort.PackageType:
                        sortProperty = nameof(InstalledPackageViewModel.DisplayPackageType);
                        break;
                    default:
                        sortProperty = null;
                        break;
                }

                switch (currentGroup)
                {
                    case PackageGroup.None:
                        groupProperty = null;
                        break;
                    case PackageGroup.Publisher:
                        groupProperty = nameof(InstalledPackageViewModel.DisplayPublisherName);
                        break;
                    case PackageGroup.Architecture:
                        groupProperty = nameof(InstalledPackageViewModel.Architecture);
                        break;
                    case PackageGroup.Type:
                        groupProperty = nameof(InstalledPackageViewModel.Type);
                        break;
                    default:
                        return;
                }

                // 1) First grouping
                if (groupProperty == null)
                {
                    this.ItemsCollection.GroupDescriptions.Clear();
                }
                else
                {
                    var pgd = this.ItemsCollection.GroupDescriptions.OfType<PropertyGroupDescription>().FirstOrDefault();
                    if (pgd == null || pgd.PropertyName != groupProperty)
                    {
                        this.ItemsCollection.GroupDescriptions.Clear();
                        this.ItemsCollection.GroupDescriptions.Add(new PropertyGroupDescription(groupProperty));
                    }
                }

                // 2) Then sorting
                if (sortProperty == null)
                {
                    this.ItemsCollection.SortDescriptions.Clear();
                }
                else
                {
                    var sd = this.ItemsCollection.SortDescriptions.FirstOrDefault();
                    if (sd.PropertyName != sortProperty || sd.Direction != (currentSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending))
                    {
                        this.ItemsCollection.SortDescriptions.Clear();
                        this.ItemsCollection.SortDescriptions.Add(new SortDescription(sortProperty, currentSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending));
                    }
                }

                if (this.ItemsCollection.GroupDescriptions.Any())
                {
                    var gpn = ((PropertyGroupDescription)this.ItemsCollection.GroupDescriptions[0]).PropertyName;
                    if (this.ItemsCollection.GroupDescriptions.Any() && this.ItemsCollection.SortDescriptions.All(sd => sd.PropertyName != gpn))
                    {
                        this.ItemsCollection.SortDescriptions.Insert(0, new SortDescription(gpn, ListSortDirection.Ascending));
                    }
                }
            }
        }
    }
}
