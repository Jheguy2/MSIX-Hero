﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Otor.MsixHero.Infrastructure.Configuration;
using Otor.MsixHero.Infrastructure.Configuration.ResolvableFolder;
using Otor.MsixHero.Infrastructure.Logging;

namespace Otor.MsixHero.Infrastructure.Services
{
    public class LocalConfigurationService : IConfigurationService, IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger();

        private readonly AutoResetEvent lockObject = new AutoResetEvent(true);

        private Configuration.Configuration currentConfiguration;

        void IDisposable.Dispose()
        {
            this.lockObject.Dispose();
        }

        public async Task<Configuration.Configuration> GetCurrentConfigurationAsync(bool preferCached =  true, CancellationToken token = default)
        {
            if (this.currentConfiguration != null && preferCached)
            {
                return this.currentConfiguration;
            }

            var waited = this.lockObject.WaitOne();

            try
            {
                if (this.currentConfiguration != null && preferCached)
                {
                    return this.currentConfiguration;
                }

                var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify), "msix-hero", "config.json");

                if (!File.Exists(file))
                {
                    var cfg = FixConfiguration(new Configuration.Configuration());
                    cfg.Update = new UpdateConfiguration
                    {
                        HideNewVersionInfo = false,
                        // ReSharper disable once PossibleNullReferenceException
                        LastShownVersion = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version.ToString(3)
                    };

                    return cfg;
                }

                var fileContent = await File.ReadAllTextAsync(file, token).ConfigureAwait(false);

                try
                {
                    this.currentConfiguration = FixConfiguration(JsonConvert.DeserializeObject<Configuration.Configuration>(fileContent, new ResolvablePathConverter()));
                }
                catch (Exception e)
                {
                    this.currentConfiguration = FixConfiguration(new Configuration.Configuration());
                    Logger.Warn(e, "Could not read the settings. Default settings will be used.");
                }

                return this.currentConfiguration;
            }
            finally
            {
                if (waited)
                {
                    this.lockObject.Set();
                }
            }
        }

        public Configuration.Configuration GetCurrentConfiguration(bool preferCached = true)
        {
            try
            {
                return this.GetCurrentConfigurationAsync(preferCached, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (AggregateException e)
            {
                throw e.GetBaseException();
            }
        }

        public async Task SetCurrentConfigurationAsync(Configuration.Configuration configuration, CancellationToken cancellationToken = default)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var waited = this.lockObject.WaitOne();
            try
            {
                var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify), "msix-hero", "config.json");

                var jsonString = JsonConvert.SerializeObject(configuration, new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter>
                    {
                        new ResolvablePathConverter()
                    },
                    DefaultValueHandling = DefaultValueHandling.Include,
                    DateParseHandling = DateParseHandling.DateTime,
                    TypeNameHandling = TypeNameHandling.None,
                    Formatting = Formatting.Indented,
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });

                var dirInfo = new FileInfo(file).Directory;
                if (dirInfo != null && !dirInfo.Exists)
                {
                    dirInfo.Create();
                }

                await File.WriteAllTextAsync(file, jsonString, cancellationToken).ConfigureAwait(false);
                this.currentConfiguration = configuration;
            }
            finally
            {
                if (waited)
                {
                    this.lockObject.Set();
                }
            }
        }

        public void SetCurrentConfiguration(Configuration.Configuration configuration)
        {
            try
            {
                this.SetCurrentConfigurationAsync(configuration, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (AggregateException e)
            {
                throw e.GetBaseException();
            }
        }

        private static Configuration.Configuration GetDefault()
        {
            var result = new Configuration.Configuration();
            result.List.Sidebar.Visible = true;
            result.List.Tools.Add(new ToolListConfiguration { Name = "Registry editor", Path = "regedit.exe", AsAdmin = true });
            result.List.Tools.Add(new ToolListConfiguration { Name = "Notepad", Path = "notepad.exe" });
            result.List.Tools.Add(new ToolListConfiguration { Name = "Command Prompt", Path = "cmd.exe" });
            result.List.Tools.Add(new ToolListConfiguration { Name = "PowerShell Console", Path = "powershell.exe" });
            return result;
        }

        private static Configuration.Configuration FixConfiguration(Configuration.Configuration result)
        {
            var defaults = GetDefault();

            if (result == null)
            {
                result = new Configuration.Configuration();
            }

            if (result.Signing == null)
            {
                result.Signing = defaults.Signing;
            }

            if (result.Signing.DefaultOutFolder == null)
            {
                result.Signing.DefaultOutFolder = defaults.Signing.DefaultOutFolder;
            }

            if (result.Packer == null)
            {
                result.Packer = defaults.Packer;
            }

            if (result.List == null)
            {
                result.List = defaults.List;
            }

            if (result.List.Tools == null || !result.List.Tools.Any())
            {
                result.List.Tools = defaults.List.Tools;
            }

            if (result.List.Sidebar == null)
            {
                result.List.Sidebar = defaults.List.Sidebar;
            }

            if (result.List.Filter == null)
            {
                result.List.Filter = defaults.List.Filter;
            }

            if (result.List.Sorting == null)
            {
                result.List.Sorting = defaults.List.Sorting;
            }

            if (result.List.Group == null)
            {
                result.List.Group = defaults.List.Group;
            }

            if (result.AppInstaller == null)
            {
                result.AppInstaller = defaults.AppInstaller;
            }

            return result;
        }
    }
}
