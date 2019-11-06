﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using otor.msixhero.lib.BusinessLayer.Commands.Developer;
using otor.msixhero.lib.BusinessLayer.Infrastructure;
using otor.msixhero.lib.BusinessLayer.Infrastructure.Implementation;
using otor.msixhero.lib.BusinessLayer.Models.Logs;
using otor.msixhero.lib.Ipc;
using otor.msixhero.lib.Managers;
using otor.msixhero.lib.Services;

namespace otor.msixhero.lib.BusinessLayer.Reducers
{
    public class GetLogsReducer : SelfElevationReducer<ApplicationState, List<Log>>
    {
        private readonly GetLogs command;
        private readonly IAppxPackageManager packageManager;
        private readonly IClientCommandRemoting clientCommandRemoting;

        public GetLogsReducer(GetLogs command, IApplicationStateManager<ApplicationState> stateManager, IAppxPackageManager packageManager, IClientCommandRemoting clientCommandRemoting) : base(command, stateManager, clientCommandRemoting)
        {
            this.command = command;
            this.packageManager = packageManager;
            this.clientCommandRemoting = clientCommandRemoting;
        }

        public override async Task<List<Log>> GetReduced(IInteractionService interactionService, CancellationToken cancellationToken = default)
        {
            if (this.command.RequiresElevation)
            {
                return await this.clientCommandRemoting.GetClientInstance().GetExecuted(this.command, cancellationToken).ConfigureAwait(false);
            }

            return new List<Log>(await this.packageManager.GetLogs(this.command.MaxCount).ConfigureAwait(false));
        }
    }
}