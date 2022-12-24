#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RefreshSparse.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using static Jellyfin.Plugin.RefreshSparse.Common.Utils;

namespace Jellyfin.Plugin.RefreshSparse
{
    public abstract class BaseRefreshTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IServerConfigurationManager _config;
        private readonly ILogger _logger;
        private readonly ILocalizationManager _localization;
        private readonly IFileSystem _fileSystem;
        private PluginConfiguration _pluginConfig;

        protected BaseRefreshTask(
            ILibraryManager libraryManager,
            IServerConfigurationManager config,
            ILogger logger,
            ILocalizationManager localization,
            IFileSystem fileSystem)
        {
            _libraryManager = libraryManager;
            _config = config;
            _logger = logger;
            _localization = localization;
            _fileSystem = fileSystem;

            _pluginConfig = Plugin.Instance.Configuration;
        }

        protected ILibraryManager LibraryManager => _libraryManager;

        protected ILocalizationManager Localization => _localization;

        protected ILogger Logger => _logger;

        protected PluginConfiguration PluginConfig => _pluginConfig;

        protected abstract string ItemTypeName
        {
            get;
        }

        public abstract string Name
        {
            get;
        }

        public abstract string Description
        {
            get;
        }

        public string Category => _localization.GetLocalizedString("Sparse Items");

        public string Key => "RefreshSparse";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        protected IEnumerable<string> SeriesBlockList => SplitToArray(_pluginConfig.SeriesBlockList);

        protected abstract bool NeedsRefresh(BaseItem item);

        protected abstract IEnumerable<BaseItem> GetItems();

        protected abstract void LogWhatsMissingInfo(BaseItem item);

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _pluginConfig = Plugin.Instance.Configuration;

            var refreshOptions = GetMetadataRefreshOptions();

            var items = GetItems();

            var numComplete = 0;
            var numItems = items.Count();
            if (numItems > 0)
            {
                _logger.LogInformation("{Prefix} refresh {X} {Item}", _pluginConfig.Pretend ? "Pretending to" : "Will", numItems, ItemTypeName);

                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        _logger.LogInformation("Refreshing {Name} ", GetItemName(item));
                        _logger.LogInformation("    hasn't been refreshed in {0:0} days", DaysSinceRefresh(item));
                        LogWhatsMissingInfo(item);
                        if (!_pluginConfig.Pretend)
                        {
                            await item.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);
                            // check if episode will pass or not after refresh
                            if (NeedsRefresh(item))
                            {
                                _logger.LogInformation("{Name} will need another refresh", GetItemName(item));
                                LogWhatsMissingInfo(item);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (System.Exception)
                    {
                        throw;
                    }

                    // Update progress
                    numComplete++;
                    double percent = numComplete;
                    percent /= numItems;

                    progress.Report(100 * percent);
                }

                progress.Report(100);

                _logger.LogInformation("Sparse {Item} refresh completed", ItemTypeName);
            }
        }

        private MetadataRefreshOptions GetMetadataRefreshOptions()
        {
            // when called from item refresh metadata menu, these are always FullRefresh. Unless scan for only new/updated files
            var refreshOptions = new MetadataRefreshOptions(new DirectoryService(_fileSystem))
            {
                MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                ForceSave = true
            };

            refreshOptions.ReplaceAllImages = GetReplaceAllImages();
            refreshOptions.ReplaceAllMetadata = GetReplaceAllMetadata();

            return refreshOptions;
        }

        protected abstract bool GetReplaceAllImages();

        protected abstract bool GetReplaceAllMetadata();

        protected virtual string GetItemName(BaseItem item)
        {
            return item.Name;
        }

        protected bool MissingImage(BaseItem item, ImageType imageType, bool performCheck)
        {
            return performCheck && !item.HasImage(imageType);
        }

        protected void LogMissingProviders(BaseItem item)
        {
            Logger.LogInformation(
                "    only has {X} provider IDs. {List}",
                item.ProviderIds.Count,
                item.ProviderIds.Count > 0 ? string.Join(',', item.ProviderIds.Keys) : string.Empty);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // no default schedule
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}
