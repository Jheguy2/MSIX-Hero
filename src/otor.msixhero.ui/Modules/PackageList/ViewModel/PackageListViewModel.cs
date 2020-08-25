﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Otor.MsixHero.Appx.Diagnostic.Registry;
using Otor.MsixHero.Appx.Packaging.Installation;
using Otor.MsixHero.Appx.Packaging.Installation.Entities;
using Otor.MsixHero.Appx.Signing;
using Otor.MsixHero.Infrastructure.Configuration;
using Otor.MsixHero.Infrastructure.Processes.SelfElevation;
using Otor.MsixHero.Infrastructure.Services;
using Otor.MsixHero.Lib.BusinessLayer.State;
using Otor.MsixHero.Lib.Infrastructure;
using Otor.MsixHero.Lib.Infrastructure.Progress;
using Otor.MsixHero.Ui.Commands.RoutedCommand;
using Otor.MsixHero.Ui.Hero;
using Otor.MsixHero.Ui.Hero.Commands.Packages;
using Otor.MsixHero.Ui.Hero.Commands.Volumes;
using Otor.MsixHero.Ui.Hero.Events.Base;
using Otor.MsixHero.Ui.Hero.Executor;
using Otor.MsixHero.Ui.Modules.Common;
using Otor.MsixHero.Ui.Modules.Dialogs.PackageExpert.ViewModel;
using Otor.MsixHero.Ui.Modules.PackageList.Navigation;
using Otor.MsixHero.Ui.Modules.PackageList.ViewModel.Elements;
using Otor.MsixHero.Ui.Themes;
using Otor.MsixHero.Ui.ViewModel;
using Prism;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;

namespace Otor.MsixHero.Ui.Modules.PackageList.ViewModel
{
    public class PackageListViewModel : NotifyPropertyChanged, INavigationAware, IActiveAware, IHeaderViewModel
    {
        private readonly IMsixHeroApplication msixHeroApplication;
        private readonly ISelfElevationProxyProvider<IAppxPackageManager> appxManagerProvider;
        private readonly ISelfElevationProxyProvider<ISigningManager> signingManagerProvider;
        private readonly ISelfElevationProxyProvider<IRegistryManager> registryManagerProvider;
        private readonly IRegionManager regionManager;
        private readonly IInteractionService interactionService;
        private readonly IConfigurationService configurationService;
        private readonly IDialogService dialogService;
        private readonly IBusyManager busyManager;
        private bool isActive;
        private bool firstRun = true;
        private ICommand showSelectionDetails;
        private bool isLoading;
        private int loadingProgress;
        private string loadingMessage;

        public PackageListViewModel(
            ISelfElevationProxyProvider<IAppxPackageManager> appxManagerProvider,
            ISelfElevationProxyProvider<ISigningManager> signingManagerProvider,
            ISelfElevationProxyProvider<IRegistryManager> registryManagerProvider,
            IRegionManager regionManager,
            IMsixHeroApplication msixHeroApplication,
            IInteractionService interactionService,
            IConfigurationService configurationService,
            IDialogService dialogService,
            IBusyManager busyManager)
        {
            this.appxManagerProvider = appxManagerProvider;
            this.signingManagerProvider = signingManagerProvider;
            this.registryManagerProvider = registryManagerProvider;
            this.regionManager = regionManager;
            this.msixHeroApplication = msixHeroApplication;
            this.interactionService = interactionService;
            this.configurationService = configurationService;
            this.dialogService = dialogService;
            this.busyManager = busyManager;
            
            this.AllPackages = new ObservableCollection<InstalledPackageViewModel>();
            this.AllPackagesView = CollectionViewSource.GetDefaultView(this.AllPackages);
            this.AllPackagesView.Filter = this.FilterPackage;
            this.SetSortingAndGrouping();

            this.busyManager.StatusChanged += this.BusyManagerOnStatusChanged;

            this.msixHeroApplication.EventAggregator.GetEvent<UiExecutedEvent<GetPackagesCommand, IList<InstalledPackage>>>().Subscribe(this.OnGetPackagesExecuted, ThreadOption.UIThread);
            this.msixHeroApplication.EventAggregator.GetEvent<UiExecutedEvent<SelectPackagesCommand>>().Subscribe(this.OnSelectPackages);
            this.msixHeroApplication.EventAggregator.GetEvent<UiExecutedEvent<SetPackageFilterCommand>>().Subscribe(this.OnSetPackageFilter, ThreadOption.UIThread);
            this.msixHeroApplication.EventAggregator.GetEvent<UiExecutedEvent<SetPackageSortingCommand>>().Subscribe(this.OnSetPackageSorting, ThreadOption.UIThread);
            this.msixHeroApplication.EventAggregator.GetEvent<UiExecutedEvent<SetPackageGroupingCommand>>().Subscribe(this.OnSetPackageGrouping, ThreadOption.UIThread);
        }

        private void OnSetPackageFilter(UiExecutedPayload<SetPackageFilterCommand> obj)
        {
            this.OnPropertyChanged(nameof(this.SearchKey));
            this.AllPackagesView.Refresh();
        }

        private void OnSelectPackages(UiExecutedPayload<SelectPackagesCommand> obj)
        {
            var selected = this.msixHeroApplication.ApplicationState.Packages.SelectedPackages.Select(p => p.ManifestLocation).ToArray();
            var navigation = new PackageListNavigation(selected);
            switch (selected.Length)
            {
                case 0:
                {
                    this.regionManager.Regions[Constants.RegionPackageSidebar].RequestNavigate(new Uri(Constants.PathPackagesEmptySelection, UriKind.Relative), navigation.ToParameters());
                    break;
                }

                case 1:
                {
                    this.regionManager.Regions[Constants.RegionPackageSidebar].RequestNavigate(new Uri(Constants.PathPackagesSingleSelection, UriKind.Relative), navigation.ToParameters());
                    break;
                }

                default:
                {
                    this.regionManager.Regions[Constants.RegionPackageSidebar].RequestNavigate(new Uri(Constants.PathPackagesMultiSelection, UriKind.Relative), navigation.ToParameters());
                    break;
                }
            }
        }

        private void OnGetPackagesExecuted(UiExecutedPayload<GetPackagesCommand, IList<InstalledPackage>> obj)
        {
            this.AllPackages.Clear();

            foreach (var item in obj.Result)
            {
                var selected = this.msixHeroApplication.ApplicationState.Packages.SelectedPackages.Contains(item);
                var itemViewModel = new InstalledPackageViewModel(this.msixHeroApplication, item, selected);
                this.AllPackages.Add(itemViewModel);
            }
        }

        private bool FilterPackage(object obj)
        {
            var item = (InstalledPackageViewModel) obj;

            switch (item.SignatureKind)
            {
                case SignatureKind.Developer:
                case SignatureKind.Unsigned:
                case SignatureKind.Enterprise:
                    if ((this.msixHeroApplication.ApplicationState.Packages.PackageFilter & PackageFilter.Developer) != PackageFilter.Developer)
                    {
                        item.IsSelected = false;
                        return false;
                    }

                    break;
                case SignatureKind.Store:
                    if ((this.msixHeroApplication.ApplicationState.Packages.PackageFilter & PackageFilter.Store) != PackageFilter.Store)
                    {
                        item.IsSelected = false;
                        return false;
                    }

                    break;
                case SignatureKind.System:
                    if ((this.msixHeroApplication.ApplicationState.Packages.PackageFilter & PackageFilter.System) != PackageFilter.System)
                    {
                        item.IsSelected = false;
                        return false;
                    }

                    break;
            }

            if (item.IsAddon && this.msixHeroApplication.ApplicationState.Packages.AddonFilter == AddonsFilter.OnlyMain)
            {
                item.IsSelected = false;
                return false;
            }

            if (!item.IsAddon && this.msixHeroApplication.ApplicationState.Packages.AddonFilter == AddonsFilter.OnlyAddons)
            {
                item.IsSelected = false;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(this.msixHeroApplication.ApplicationState.Packages.SearchKey) && item.DisplayName.IndexOf(this.msixHeroApplication.ApplicationState.Packages.SearchKey, StringComparison.OrdinalIgnoreCase) == -1
                                                           && item.DisplayPublisherName.IndexOf(this.msixHeroApplication.ApplicationState.Packages.SearchKey, StringComparison.OrdinalIgnoreCase) == -1
                                                           && item.Version.IndexOf(this.msixHeroApplication.ApplicationState.Packages.SearchKey, StringComparison.OrdinalIgnoreCase) == -1
                                                           && item.Architecture.IndexOf(this.msixHeroApplication.ApplicationState.Packages.SearchKey, StringComparison.OrdinalIgnoreCase) == -1)
            {
                item.IsSelected = false;
                return false;
            }

            return true;
        }

        private void BusyManagerOnStatusChanged(object sender, IBusyStatusChange e)
        {
            if (e.Type != OperationType.PackageLoading)
            {
                return;
            }

            this.IsLoading = e.IsBusy;
            this.LoadingMessage = e.Message;
            this.LoadingProgress = e.Progress;
        }

        public bool IsLoading
        {
            get => this.isLoading;
            private set => this.SetField(ref this.isLoading, value);
        }

        public int LoadingProgress
        {
            get => this.loadingProgress;
            private set => this.SetField(ref this.loadingProgress, value);
        }

        public string LoadingMessage
        {
            get => this.loadingMessage;
            private set => this.SetField(ref this.loadingMessage, value);
        }

        public PackageListCommandHandler CommandHandler => new PackageListCommandHandler(
            this.appxManagerProvider, 
            this.signingManagerProvider, 
            this.registryManagerProvider, 
            this.msixHeroApplication, 
            this.interactionService, 
            this.configurationService, 
            this.dialogService, 
            this.busyManager);

        public bool IsActive
        {
            get => this.isActive;
            set
            {
                if (this.isActive == value)
                {
                    return;
                }

                this.isActive = value;
                this.IsActiveChanged?.Invoke(this, new EventArgs());
                
                if (value)
                {
                    if (!this.firstRun)
                    {
                        return;
                    }

                    firstRun = false;

                    this.LoadInitialData();
                }
            }
        }

        private async void LoadInitialData()
        {
            await Task.Delay(200).ConfigureAwait(false);
            var executor = this.msixHeroApplication.CommandExecutor
                .WithBusyManager(this.busyManager, OperationType.PackageLoading)
                .WithErrorHandling(this.interactionService, true);

            await executor.Invoke(this, new GetPackagesCommand(), CancellationToken.None).ConfigureAwait(false);
        }

        public event EventHandler IsActiveChanged;

        public string Header { get; } = "Packages";

        public Geometry Icon { get; } = VectorIcons.TabPackages;

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
            var currentSort = this.msixHeroApplication.ApplicationState.Packages.SortMode;
            var currentSortDescending = this.msixHeroApplication.ApplicationState.Packages.SortDescending;
            var currentGroup = this.msixHeroApplication.ApplicationState.Packages.GroupMode;

            using (this.AllPackagesView.DeferRefresh())
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
                    this.AllPackagesView.GroupDescriptions.Clear();
                }
                else
                {
                    var pgd = this.AllPackagesView.GroupDescriptions.OfType<PropertyGroupDescription>().FirstOrDefault();
                    if (pgd == null || pgd.PropertyName != groupProperty)
                    {
                        this.AllPackagesView.GroupDescriptions.Clear();
                        this.AllPackagesView.GroupDescriptions.Add(new PropertyGroupDescription(groupProperty));
                    }
                }

                // 2) Then sorting
                if (sortProperty == null)
                {
                    this.AllPackagesView.SortDescriptions.Clear();
                }
                else
                {
                    var sd = this.AllPackagesView.SortDescriptions.FirstOrDefault();
                    if (sd.PropertyName != sortProperty || sd.Direction != (currentSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending))
                    {
                        this.AllPackagesView.SortDescriptions.Clear();
                        this.AllPackagesView.SortDescriptions.Add(new SortDescription(sortProperty, currentSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending));
                    }
                }

                if (this.AllPackagesView.GroupDescriptions.Any())
                {
                    var gpn = ((PropertyGroupDescription)this.AllPackagesView.GroupDescriptions[0]).PropertyName;
                    if (this.AllPackagesView.GroupDescriptions.Any() && this.AllPackagesView.SortDescriptions.All(sd => sd.PropertyName != gpn))
                    {
                        this.AllPackagesView.SortDescriptions.Insert(0, new SortDescription(gpn, ListSortDirection.Ascending));
                    }
                }
            }
        }

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

        public string SearchKey
        {
            get => this.msixHeroApplication.ApplicationState.Packages.SearchKey;
            set
            {
                if (this.msixHeroApplication.ApplicationState.Packages.SearchKey == value)
                {
                    return;
                }

                var state = this.msixHeroApplication.ApplicationState.Packages;


                this.msixHeroApplication.CommandExecutor
                    .WithErrorHandling(this.interactionService, false)
                    .Invoke(this, new SetPackageFilterCommand(state.PackageFilter, state.AddonFilter, value));
            }
        }

        public ObservableCollection<InstalledPackageViewModel> AllPackages { get; }

        public ICollectionView AllPackagesView { get; }

        public ICommand ShowSelectionDetails
        {
            get
            {
                return this.showSelectionDetails ??= new DelegateCommand(this.ShowSelectionDetailsExecute);
            }
        }

        private void ShowSelectionDetailsExecute(object obj)
        {
            var selected = this.AllPackages.FirstOrDefault(item => item.IsSelected);
            if (selected == null)
            {
                return;
            }

            this.dialogService.ShowDialog(Constants.PathPackageExpert, new PackageExpertSelection(selected.ManifestLocation).ToDialogParameters(), result => {});
        }
    }
}
