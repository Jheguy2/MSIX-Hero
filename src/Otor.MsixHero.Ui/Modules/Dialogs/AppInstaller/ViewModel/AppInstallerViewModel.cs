﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Otor.MsixHero.AppInstaller;
using Otor.MsixHero.AppInstaller.Entities;
using Otor.MsixHero.Infrastructure.Progress;
using Otor.MsixHero.Infrastructure.Services;
using Otor.MsixHero.Ui.Commands.RoutedCommand;
using Otor.MsixHero.Ui.Controls.ChangeableDialog.ViewModel;
using Otor.MsixHero.Ui.Domain;
using Otor.MsixHero.Ui.Modules.Dialogs.Common.PackageSelector.ViewModel;
using Prism.Services.Dialogs;

namespace Otor.MsixHero.Ui.Modules.Dialogs.AppInstaller.ViewModel
{
    public class AppInstallerViewModel : ChangeableDialogViewModel, IDialogAware
    {
        private readonly AppInstallerBuilder appInstallerBuilder;
        private readonly IInteractionService interactionService;
        private readonly IConfigurationService configurationService;
        private ICommand openSuccessLink;
        private ICommand reset;
        private ICommand open;
        private string previousPath;

        public AppInstallerViewModel(
            IInteractionService interactionService,
            IConfigurationService configurationService) : base("Create .appinstaller", interactionService)
        {
            this.appInstallerBuilder = new AppInstallerBuilder();
            this.interactionService = interactionService;
            this.configurationService = configurationService;

            this.AppInstallerUpdateCheckingMethod = new ChangeableProperty<AppInstallerUpdateCheckingMethod>(Otor.MsixHero.AppInstaller.Entities.AppInstallerUpdateCheckingMethod.LaunchAndBackground);
            this.AllowDowngrades = new ChangeableProperty<bool>();
            this.BlockLaunching = new ChangeableProperty<bool>();
            this.Version = new ValidatedChangeableProperty<string>("Version", "1.0.0.0", this.ValidateVersion);
            this.ShowPrompt = new ChangeableProperty<bool>();
            this.MainPackageUri = new ValidatedChangeableProperty<string>("Main package URL", true, ValidatorFactory.ValidateUri(true));
            this.AppInstallerUri = new ValidatedChangeableProperty<string>("App installer URL", true, ValidatorFactory.ValidateUri(true));
            this.Hours = new ValidatedChangeableProperty<string>("Hours between updates", "24", this.ValidateHours);

            this.TabPackage = new PackageSelectorViewModel(
                interactionService,
                PackageSelectorDisplayMode.AllowAllPackageTypes | 
                PackageSelectorDisplayMode.ShowTypeSelector | 
                PackageSelectorDisplayMode.AllowManifests | 
                PackageSelectorDisplayMode.ShowActualName | 
                PackageSelectorDisplayMode.RequireFullIdentity |
                PackageSelectorDisplayMode.AllowBrowsing | 
                PackageSelectorDisplayMode.AllowChanging)
            {
                CustomPrompt = "What will be targeted by this .appinstaller?"
            };

            this.TabOptions = new ChangeableContainer(
                this.AppInstallerUpdateCheckingMethod,
                this.AllowDowngrades,
                this.BlockLaunching,
                this.ShowPrompt,
                this.Hours);

            this.TabProperties = new ChangeableContainer(
                this.Version, 
                this.MainPackageUri, 
                this.AppInstallerUri);
            
            this.AddChildren(
                this.TabPackage,
                this.TabProperties,
                this.TabOptions);

            this.TabPackage.InputPath.ValueChanged += this.InputPathOnValueChanged;
            this.AppInstallerUpdateCheckingMethod.ValueChanged += this.AppInstallerUpdateCheckingMethodValueChanged;
            this.AllowDowngrades.ValueChanged += this.OnBooleanChanged;
            this.BlockLaunching.ValueChanged += this.OnBooleanChanged;
            this.ShowPrompt.ValueChanged += this.OnBooleanChanged;
        }

        public ChangeableContainer TabProperties { get; }

        public ChangeableContainer TabOptions { get; }
        public bool ShowLaunchOptions =>
            this.AppInstallerUpdateCheckingMethod.CurrentValue == Otor.MsixHero.AppInstaller.Entities.AppInstallerUpdateCheckingMethod.LaunchAndBackground ||
            this.AppInstallerUpdateCheckingMethod.CurrentValue == Otor.MsixHero.AppInstaller.Entities.AppInstallerUpdateCheckingMethod.Launch;

        public ChangeableProperty<AppInstallerUpdateCheckingMethod> AppInstallerUpdateCheckingMethod { get; }

        public ValidatedChangeableProperty<string> Hours { get; }

        public ChangeableProperty<bool> BlockLaunching { get; }

        public ChangeableProperty<bool> ShowPrompt { get; }

        public ValidatedChangeableProperty<string> Version { get; }

        public ChangeableProperty<bool> AllowDowngrades { get; }
        
        public ValidatedChangeableProperty<string> MainPackageUri { get; }

        public ValidatedChangeableProperty<string> AppInstallerUri { get; }

        public string CompatibleWindows
        {
            get
            {
                var minWin10 = this.appInstallerBuilder.GetMinimumSupportedWindowsVersion(this.GetCurrentAppInstallerConfig());
                return $"Windows 10 {minWin10.Item1}";
            }
        }
        
        public ICommand OpenSuccessLinkCommand
        {
            get { return this.openSuccessLink ??= new DelegateCommand(this.OpenSuccessLinkExecuted); }
        }

        public ICommand ResetCommand
        {
            get { return this.reset ??= new DelegateCommand(this.ResetExecuted); }
        }

        public ICommand OpenCommand
        {
            get { return this.open ??= new DelegateCommand(this.OpenExecuted); }
        }

        public PackageSelectorViewModel TabPackage { get; }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.TryGetValue("file", out string sourceFile))
            {
                var ext = Path.GetExtension(sourceFile) ?? string.Empty;

                switch (ext.ToLowerInvariant())
                {
                    case ".appinstaller":
                        this.OpenCommand.Execute(sourceFile);
                        break;
                    default:
                        this.TabPackage.InputPath.CurrentValue = sourceFile;
                        this.TabPackage.AllowChangingSourcePackage = false;
                        this.TabPackage.ShowPackageTypeSelector = false;
                        break;
                }
            }
        }

        protected override async Task<bool> Save(CancellationToken cancellationToken, IProgress<ProgressData> progress)
        {
            if (!this.IsValid)
            {
                return false;
            }

            if (!this.interactionService.SaveFile(this.previousPath, "Appinstaller files|*.appinstaller|All files|*.*", out var selected))
            {
                return false;
            }

            this.previousPath = selected;
            var appInstaller = this.GetCurrentAppInstallerConfig();
            await this.appInstallerBuilder.Create(appInstaller, selected, cancellationToken, progress).ConfigureAwait(false);
            return true;
        }

        private AppInstallerConfig GetCurrentAppInstallerConfig()
        {
            var builder = new AppInstallerBuilder
            {
                Version = this.Version.CurrentValue,
                MainPackageType = this.TabPackage.PackageType.CurrentValue,
                MainPackageName = this.TabPackage.Name.CurrentValue,
                MainPackageArchitecture = this.TabPackage.Architecture.CurrentValue,
                MainPackagePublisher = this.TabPackage.Publisher.CurrentValue,
                MainPackageVersion = this.TabPackage.Version.CurrentValue,
                HoursBetweenUpdateChecks = int.Parse(this.Hours.CurrentValue),
                CheckForUpdates = this.AppInstallerUpdateCheckingMethod.CurrentValue,
                ShowPrompt = this.ShowPrompt.CurrentValue,
                UpdateBlocksActivation = this.BlockLaunching.CurrentValue,
                AllowDowngrades = this.AllowDowngrades.CurrentValue,
                RedirectUri = string.IsNullOrEmpty(this.AppInstallerUri.CurrentValue) ? null : new Uri(this.AppInstallerUri.CurrentValue),
                MainPackageUri = string.IsNullOrEmpty(this.MainPackageUri.CurrentValue) ? null : new Uri(this.MainPackageUri.CurrentValue),
            };

            var appInstaller = builder.Build();
            return appInstaller;
        }

        private void ResetExecuted(object parameter)
        {
            this.TabPackage.Reset();
            this.State.IsSaved = false;
        }

        private void OpenExecuted(object obj)
        {
            string selected = obj as string;

            if (selected != null)
            {
                if (!File.Exists(selected))
                {
                    selected = null;
                }
                else
                {
                    previousPath = selected;
                }
            }

            if (selected == null)
            {
                if (this.previousPath != null)
                {
                    if (!this.interactionService.SelectFile(this.previousPath, "Appinstaller files|*.appinstaller|All files|*.*", out selected))
                    {
                        return;
                    }
                }
                else if (!this.interactionService.SelectFile("Appinstaller files|*.appinstaller|All files|*.*", out selected))
                {
                    return;
                }
            }
            
            this.previousPath = selected;

            AppInstallerConfig.FromFile(selected).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    this.interactionService.ShowError("The selected file is not a valid Appinstaller.", t.Exception.GetBaseException(), InteractionResult.OK);
                    return;
                }

                if (t.IsCanceled)
                {
                    return;
                }

                if (!t.IsCompleted)
                {
                    return;
                }

                var builder = new AppInstallerBuilder(t.Result);

                this.AllowDowngrades.CurrentValue = builder.AllowDowngrades;
                this.AppInstallerUpdateCheckingMethod.CurrentValue = builder.CheckForUpdates;
                this.AppInstallerUri.CurrentValue = builder.RedirectUri.ToString();
                this.BlockLaunching.CurrentValue = builder.UpdateBlocksActivation;
                this.Hours.CurrentValue = builder.HoursBetweenUpdateChecks.ToString();
                this.MainPackageUri.CurrentValue = builder.MainPackageUri.ToString();
                this.Version.CurrentValue = builder.Version;
                this.ShowPrompt.CurrentValue = builder.ShowPrompt;

                this.TabPackage.Name.CurrentValue = builder.MainPackageName;
                this.TabPackage.Version.CurrentValue = builder.MainPackageVersion;
                this.TabPackage.Publisher.CurrentValue = builder.MainPackagePublisher;
                this.TabPackage.PackageType.CurrentValue = builder.MainPackageType;
                this.TabPackage.Architecture.CurrentValue = builder.MainPackageArchitecture;

                this.AllowDowngrades.CurrentValue = builder.AllowDowngrades;

                this.OnPropertyChanged(nameof(ShowLaunchOptions));
                this.OnPropertyChanged(nameof(CompatibleWindows));
            }, 
            CancellationToken.None, 
            TaskContinuationOptions.AttachedToParent | TaskContinuationOptions.ExecuteSynchronously, 
            TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OpenSuccessLinkExecuted(object parameter)
        {
            Process.Start("explorer.exe", "/select," + previousPath);
        }
        
        private void InputPathOnValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty((string)e.NewValue))
            {
                var isManifest = string.Equals(Path.GetFileName((string)e.NewValue), "appxmanifest.xml", StringComparison.OrdinalIgnoreCase);
                
                if (isManifest)
                {
                    return;
                }
            }
            
            if (string.IsNullOrEmpty(this.MainPackageUri.CurrentValue) && !string.IsNullOrEmpty(e.NewValue as string))
            {
                var newFilePath = new FileInfo((string)e.NewValue);
                var configValue = this.configurationService.GetCurrentConfiguration().AppInstaller?.DefaultRemoteLocationPackages;
                if (string.IsNullOrEmpty(configValue))
                {
                    configValue = "http://server-name/";
                }

                this.MainPackageUri.CurrentValue = $"{configValue.TrimEnd('/')}/{newFilePath.Name}";
            }
            
            if (string.IsNullOrEmpty(this.AppInstallerUri.CurrentValue) && !string.IsNullOrEmpty(e.NewValue as string))
            {
                var newFilePath = new FileInfo((string)e.NewValue);
                var configValue = this.configurationService.GetCurrentConfiguration().AppInstaller?.DefaultRemoteLocationPackages;
                if (string.IsNullOrEmpty(configValue))
                {
                    configValue = "http://server-name/";
                }

                var newName = Path.ChangeExtension(newFilePath.Name, ".appinstaller");
                this.AppInstallerUri.CurrentValue = $"{configValue.TrimEnd('/')}/{newName}";
            }
        }

        private void AppInstallerUpdateCheckingMethodValueChanged(object sender, ValueChangedEventArgs e)
        {
            this.OnPropertyChanged(nameof(ShowLaunchOptions));
            this.OnPropertyChanged(nameof(CompatibleWindows));
        }
        
        private string ValidateHours(string newValue)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                return "This value cannot be empty.";
            }

            if (!int.TryParse(newValue, out var value))
            {
                return $"The value '{newValue}' is not a valid number.";
            }

            if (value < 0 || value > 255)
            {
                return $"The value '{newValue}' lies outside of valid range 0-255.";
            }

            return null;
        }

        private void OnBooleanChanged(object sender, ValueChangedEventArgs e)
        {
            this.OnPropertyChanged(nameof(CompatibleWindows));
        }
        private string ValidateVersion(string newValue)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                return "The version may not be empty.";
            }

            if (!System.Version.TryParse(newValue, out _))
            {
                return $"'{newValue}' is not a valid version.";
            }

            return null;
        }
    }
}