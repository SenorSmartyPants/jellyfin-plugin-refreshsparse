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
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

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

        public string Category => _localization.GetLocalizedString("TasksLibraryCategory");

        public string Key => "RefreshSparse";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        protected abstract bool NeedsRefresh(BaseItem item);

        protected abstract IEnumerable<BaseItem> GetItems();

        protected abstract void LogWhatsMissingInfo(BaseItem item);

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _pluginConfig = Plugin.Instance.Configuration;

            // when called from item refresh metadata menu, these are always full. Unless scan for only new/updated files
            var metadataRefreshMode = MetadataRefreshMode.FullRefresh;
            var imageRefreshMode = MetadataRefreshMode.FullRefresh;

            // TODO: replace all for each type series/episode

            var refreshOptions = new MetadataRefreshOptions(new DirectoryService(_fileSystem))
            {
                MetadataRefreshMode = metadataRefreshMode,
                ImageRefreshMode = imageRefreshMode,
                ReplaceAllImages = _pluginConfig.ReplaceAllImages,
                ReplaceAllMetadata = _pluginConfig.ReplaceAllMetadata,
                ForceSave = metadataRefreshMode == MetadataRefreshMode.FullRefresh
                    || imageRefreshMode == MetadataRefreshMode.FullRefresh
                    || _pluginConfig.ReplaceAllImages
                    || _pluginConfig.ReplaceAllMetadata,
                IsAutomated = false
            };

            var items = GetItems();

            var numComplete = 0;
            var numItems = items.Count();
            if (numItems > 0)
            {
                if (_pluginConfig.Pretend)
                {
                    _logger.LogInformation("Pretending to refresh {X} {Item}", numItems, ItemTypeName);
                }
                else
                {
                    _logger.LogInformation("Will refresh {X} {Item}", numItems, ItemTypeName);
                }

                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        _logger.LogInformation("Refreshing {Name} ", GetItemName(item));
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

        public virtual string GetItemName(BaseItem item)
        {
            return item.Name;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // no default schedule
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}
