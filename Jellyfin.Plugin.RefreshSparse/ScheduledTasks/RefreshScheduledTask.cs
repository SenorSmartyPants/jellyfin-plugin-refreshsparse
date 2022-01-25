#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
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

        private bool IsDate(string possibleDate)
        {
            DateTime d;
            return DateTime.TryParse(possibleDate, out d);
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var config = Plugin.Instance.Configuration;

            // episodes that aired in the past MaxDays days
            DateTime? minPremiereDate = null;
            var maxDays = config.MaxDays;
            if (maxDays > -1 )
            {
                minPremiereDate = DateTime.UtcNow.Date.AddDays(-(double)maxDays);
            }

            List<Episode> episodes =
                _libraryManager.GetItemList(
                        new InternalItemsQuery
                        {
                            IncludeItemTypes = new[] { BaseItemKind.Episode },
                            IsVirtualItem = false,
                            Recursive = true,
                            MinPremiereDate = minPremiereDate
                        }).Cast<Episode>().ToList();

            var badNameList = config.BadNames.Split("|", StringSplitOptions.RemoveEmptyEntries);

            episodes = episodes.Where(i => (config.MissingImage && !i.HasImage(ImageType.Primary))
                || (config.MissingOverview && string.IsNullOrWhiteSpace(i.Overview))
                || (config.MissingName && string.IsNullOrWhiteSpace(i.Name))
                || (config.NameIsDate && IsDate(i.Name))
                || badNameList.Any(en => i.Name.StartsWith(en, StringComparison.CurrentCultureIgnoreCase))
                || i.ProviderIds.Count < config.MinimumProviderIds).ToList();
            // DateLastRefreshed - don't hammer metadata provider, won't be new info

            // just get missing, all is being overwritten currently in 10.8 regardless of these settings.
            var replaceAllMetadata = false;
            var replaceAllImages = false;

            // when called from menu, these are always full. Unless scan for new/updated files
            var metadataRefreshMode = MetadataRefreshMode.FullRefresh;
            var imageRefreshMode = MetadataRefreshMode.FullRefresh;

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

            var numComplete = 0;

            var numEpisodes = episodes.Count;

            if (numEpisodes > 0)
            {
                _logger.LogInformation("Will refresh {X} episodes", numEpisodes);

                foreach (Episode episode in episodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        _logger.LogInformation("Refreshing {Series} {SeasonNumber}x{EpisodeNumber}", episode.SeriesName, episode.ParentIndexNumber, episode.IndexNumber);
                        await episode.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);
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
                    percent /= numEpisodes;

                    progress.Report(100 * percent);
                }

                progress.Report(100);

                _logger.LogInformation("Sparse episode refresh completed");
            }
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // no default schedule
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}
