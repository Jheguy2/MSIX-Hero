﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using otor.msixhero.lib.BusinessLayer.Managers.Packages;
using otor.msixhero.lib.BusinessLayer.State;
using otor.msixhero.lib.Domain.Appx.Packages;
using otor.msixhero.lib.Domain.Commands.Packages.Manager;
using otor.msixhero.lib.Infrastructure;
using otor.msixhero.lib.Infrastructure.Progress;
using otor.msixhero.lib.Infrastructure.SelfElevation;

namespace otor.msixhero.lib.BusinessLayer.Executors.Server
{
    public class RemovePackageServerExecutor : CommandExecutor
    {
        private readonly RemovePackages action;
        private readonly ISelfElevationManagerFactory<IAppxPackageManager> packageManagerFactory;

        public RemovePackageServerExecutor(RemovePackages action, ISelfElevationManagerFactory<IAppxPackageManager> packageManagerFactory, IWritableApplicationStateManager stateManager) : base(action, stateManager)
        {
            this.action = action;
            this.packageManagerFactory = packageManagerFactory;
        }

        public override async Task Execute(IInteractionService interactionService, CancellationToken cancellationToken = default, IProgress<ProgressData> progressData = default)
        {
            if (this.action.Packages == null || !this.action.Packages.Any())
            {
                return;
            }

            var manager = await this.packageManagerFactory.Get(this.action.Context == PackageContext.AllUsers ? SelfElevationLevel.AsAdministrator : SelfElevationLevel.HighestAvailable, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await manager.Remove(this.action.Packages, cancellationToken: cancellationToken, progress: progressData).ConfigureAwait(false);
        }
    }
}
