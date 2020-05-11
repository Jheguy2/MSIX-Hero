﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using otor.msixhero.lib.BusinessLayer.State;
using otor.msixhero.lib.Domain.Appx.Packages;
using otor.msixhero.lib.Domain.Commands;
using otor.msixhero.lib.Domain.Commands.Packages.Grid;
using otor.msixhero.lib.Infrastructure.Commanding;
using otor.msixhero.lib.Infrastructure.Configuration;
using otor.msixhero.lib.Infrastructure.Interop;
using otor.msixhero.lib.Infrastructure.Ipc;
using otor.msixhero.lib.Infrastructure.Progress;
using Prism.Events;

namespace otor.msixhero.lib.tests
{
    [TestFixture()]
    public class ServerTest
    {
        [Test]
        public void TestCommunication()
        {
            var result = new List<InstalledPackage> {new InstalledPackage() { Name = "ABC" }};

            var server = new Server(
                new ApplicationStateManager(
                    new EventAggregator(),
                    new DummyCommandExecutor(result), 
                    new LocalConfigurationService()));

            var client = new Client(new DummyProcessManager());
            var t2 = client.GetExecuted(new GetPackages(), CancellationToken.None);
            var t1 = server.Start(1);

            Task.WaitAll(t1, t2);
            Assert.AreEqual(1, t2.Result.Count);
            Assert.AreEqual("ABC", t2.Result[0].Name);
        }

        private class DummyCommandExecutor : ICommandBus
        {
            private readonly object result;

            public DummyCommandExecutor(object result)
            {
                this.result = result;
            }

            public void SetStateManager(IWritableApplicationStateManager stateManager)
            {
            }

            public void Execute(VoidCommand action)
            {
            }

            public T GetExecute<T>(CommandWithOutput<T> action)
            {
                return default;
            }

            public Task ExecuteAsync(VoidCommand action, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = default)
            {
                return Task.FromResult(true);
            }

            public Task<T> GetExecuteAsync<T>(CommandWithOutput<T> command, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = default)
            {
                return Task.FromResult(default(T));
            }

            public Task<object> GetExecuteAsync(VoidCommand command, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = default)
            {
                return Task.FromResult(result);
            }
        }


        private class DummyProcessManager : IProcessManager
        {
            public void Dispose()
            {   
            }

            public Task<bool> CheckIfRunning(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(true);
            }

            public async Task Connect(CancellationToken cancellationToken = default)
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
