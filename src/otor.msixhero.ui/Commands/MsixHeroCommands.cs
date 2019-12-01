﻿using System.Windows.Input;

namespace otor.msixhero.ui.Commands
{
    public static class MsixHeroCommands
    {
        static MsixHeroCommands()
        {
            OpenExplorer = new RoutedUICommand { Text = "Open install location", InputGestures = { new KeyGesture(Key.E, ModifierKeys.Control) } };
            OpenExplorerUser = new RoutedUICommand { Text = "Open user data folder", InputGestures = { new KeyGesture(Key.U, ModifierKeys.Control) } };
            OpenManifest = new RoutedUICommand { Text = "Open manifest", InputGestures = { new KeyGesture(Key.M, ModifierKeys.Control )}};
            OpenConfigJson = new RoutedUICommand { Text = "Open config.json", InputGestures = { new KeyGesture(Key.J, ModifierKeys.Control )}};
            RunPackage = new RoutedUICommand { Text = "Run app", InputGestures = { new KeyGesture(Key.Enter, ModifierKeys.Control) } };
            RunTool = new RoutedUICommand { Text = "Run tool in package context" };
            OpenPowerShell = new RoutedUICommand { Text = "Open PowerShell console" };
            MountRegistry = new RoutedUICommand { Text = "Mount registry" };
            UnmountRegistry = new RoutedUICommand { Text = "Unmount registry" };
            CreateSelfSign = new RoutedUICommand { Text = "Create new self-signed certificate "};
            OpenLogs = new RoutedUICommand { Text = "Show event viewer", InputGestures = { new KeyGesture(Key.E, ModifierKeys.Control) } };
            RemovePackage = new RoutedUICommand { Text = "Remove package" };
            AddPackage = new RoutedUICommand { Text = "Add package", InputGestures = { new KeyGesture(Key.Insert)}};
            OpenAppsFeatures = new RoutedUICommand { Text = "Open Apps and Features" };
            OpenDevSettings = new RoutedUICommand { Text = "Open Developer Settings" };
            OpenResign = new RoutedUICommand { Text = "Sign MSIX package(s)" };
            InstallCertificate = new RoutedUICommand { Text = "Install certificate from a .CER file" };
            ExtractCertifiacte = new RoutedUICommand { Text = "Extract certificate from an .MSIX file" };
        }

        public static RoutedUICommand OpenExplorer { get; }
        
        public static RoutedUICommand OpenResign { get; }

        public static RoutedUICommand OpenExplorerUser { get; }

        public static RoutedUICommand CreateSelfSign { get; }

        public static RoutedUICommand ExtractCertifiacte { get; }

        public static RoutedUICommand InstallCertificate { get; }
        
        public static RoutedUICommand OpenManifest { get; }

        public static RoutedUICommand OpenConfigJson { get; }

        public static RoutedUICommand RunTool { get; }

        public static RoutedUICommand RunPackage { get; }

        public static RoutedUICommand AddPackage { get; }

        public static RoutedUICommand OpenPowerShell { get; }

        public static RoutedUICommand OpenAppsFeatures { get; }

        public static RoutedUICommand OpenDevSettings { get; }

        public static RoutedUICommand MountRegistry { get; }

        public static RoutedUICommand UnmountRegistry { get; }

        public static RoutedUICommand OpenLogs { get; }

        public static RoutedUICommand RemovePackage { get; }
    }
}
