#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RefreshSparse
{
    public class RefreshScheduledTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IServerConfigurationManager _config;
        private readonly ILogger<RefreshScheduledTask> _logger;
        private readonly ILocalizationManager _localization;
        private readonly IFileSystem _fileSystem;

        public RefreshScheduledTask(
            ILibraryManager libraryManager,
            IServerConfigurationManager config,
            ILogger<RefreshScheduledTask> logger,
            ILocalizationManager localization,
            IFileSystem fileSystem)
        {
            _libraryManager = libraryManager;
            _config = config;
            _logger = logger;
            _localization = localization;
            _fileSystem = fileSystem;
        }

        public string Name => _localization.GetLocalizedString("Refresh sparse episodes");

        public string Description => _localization.GetLocalizedString("Refresh episodes with missing metadata based on requirements configured.");

        public string Category => _localization.GetLocalizedString("TasksLibraryCategory");

        public string Key => "RefreshSparse";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            // episodes that aired in the past 14 days
            var minPremiereDate = DateTime.UtcNow.Date.AddDays(-14);

            var mediaItems =
                _libraryManager.GetItemList(
                        new InternalItemsQuery
                        {
                            IncludeItemTypes = new[] { "Episode" },
                            IsVirtualItem = false,
                            Recursive = true,
                            MinPremiereDate = minPremiereDate,
                        }).ToList();

            // just get missing
            var replaceAllMetadata = false;
            var replaceAllImages = false;

            // when called from menu, these are always full. Unless scan for new/updated files
            var metadataRefreshMode = MetadataRefreshMode.FullRefresh;
            var imageRefreshMode = MetadataRefreshMode.FullRefresh;

            // TODO more query options
            var refreshOptions = new MetadataRefreshOptions(new DirectoryService(_fileSystem))
            {
                MetadataRefreshMode = metadataRefreshMode,
                ImageRefreshMode = imageRefreshMode,
                ReplaceAllImages = replaceAllImages,
                ReplaceAllMetadata = replaceAllMetadata,
                ForceSave = metadataRefreshMode == MetadataRefreshMode.FullRefresh
                    || imageRefreshMode == MetadataRefreshMode.FullRefresh
                    || replaceAllImages
                    || replaceAllMetadata,
                IsAutomated = false
            };

            // Refresh all in list
            foreach (var item in mediaItems)
            {
                item.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);
            }

            return Task.FromResult(true);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // no default schedule
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}
