﻿using System;
using System.Threading;
using System.Threading.Tasks;
using otor.msixhero.lib.BusinessLayer.State;
using otor.msixhero.lib.Domain.Commands.Packages.Grid;
using otor.msixhero.lib.Domain.Events.PackageList;
using otor.msixhero.lib.Infrastructure;
using otor.msixhero.lib.Infrastructure.Progress;

namespace otor.msixhero.lib.BusinessLayer.Executors.Client
{
    public class SetPackageSidebarVisibilityClientExecutor : CommandExecutor
    {
        private readonly SetPackageSidebarVisibility action;

        public SetPackageSidebarVisibilityClientExecutor(SetPackageSidebarVisibility action, IWritableApplicationStateManager stateManager) : base(action, stateManager)
        {
            this.action = action;
        }

        public override Task Execute(IInteractionService interactionService, CancellationToken cancellationToken = default, IProgress<ProgressData> progressReporter = default)
        {
            if (this.action.IsVisible == this.StateManager.CurrentState.Packages.ShowSidebar)
            {
                return Task.FromResult(false);
            }

            this.StateManager.CurrentState.Packages.ShowSidebar = action.IsVisible;
            this.StateManager.EventAggregator.GetEvent<PackagesSidebarVisibilityChanged>().Publish(action.IsVisible);
            return Task.FromResult(true);
        }
    }
}