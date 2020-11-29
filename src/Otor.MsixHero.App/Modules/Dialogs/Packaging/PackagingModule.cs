﻿using Otor.MsixHero.App.Modules.Dialogs.Packaging.ModificationPackage.View;
using Otor.MsixHero.App.Modules.Dialogs.Packaging.ModificationPackage.ViewModel;
using Otor.MsixHero.App.Modules.Dialogs.Packaging.Pack.View;
using Otor.MsixHero.App.Modules.Dialogs.Packaging.Pack.ViewModel;
using Otor.MsixHero.App.Modules.Dialogs.Packaging.Unpack.View;
using Otor.MsixHero.App.Modules.Dialogs.Packaging.Unpack.ViewModel;
using Prism.Ioc;
using Prism.Modularity;

namespace Otor.MsixHero.App.Modules.Dialogs.Packaging
{
    public class PackagingModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<ModificationPackageView, ModificationPackageViewModel>(NavigationPaths.DialogPaths.PackagingModificationPackage);
            containerRegistry.RegisterForNavigation<PackView, PackViewModel>(NavigationPaths.DialogPaths.PackagingPack);
            containerRegistry.RegisterForNavigation<UnpackView, UnpackViewModel>(NavigationPaths.DialogPaths.PackagingUnpack);
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
        }
    }
}