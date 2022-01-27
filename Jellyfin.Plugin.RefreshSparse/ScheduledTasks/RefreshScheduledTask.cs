#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.RefreshSparse.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
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
        private readonly PluginConfiguration _pluginConfig;
        private readonly string[] _badNameList;

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

            _pluginConfig = Plugin.Instance.Configuration;
            _badNameList = BadNamesAsArray();
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

        private double MinutesSinceRefresh(Episode episode)
        {
            return (DateTime.UtcNow - episode.DateLastRefreshed).TotalMinutes;
        }

        private bool NeedsRefresh(Episode episode)
        {
            return (_pluginConfig.MissingImage && !episode.HasImage(ImageType.Primary))
                || (_pluginConfig.MissingOverview && string.IsNullOrWhiteSpace(episode.Overview))
                || (_pluginConfig.MissingName && string.IsNullOrWhiteSpace(episode.Name))
                || (_pluginConfig.NameIsDate && IsDate(episode.Name))
                || _badNameList.Any(en => episode.Name.StartsWith(en, StringComparison.CurrentCultureIgnoreCase))
                || episode.ProviderIds.Count < _pluginConfig.MinimumProviderIds;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            // when called from item refresh metadata menu, these are always full. Unless scan for only new/updated files
            var metadataRefreshMode = MetadataRefreshMode.FullRefresh;
            var imageRefreshMode = MetadataRefreshMode.FullRefresh;

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

            // episodes that aired in the past MaxDays days
            DateTime? minPremiereDate = null;
            var maxDays = _pluginConfig.MaxDays;
            if (maxDays > -1 )
            {
                minPremiereDate = DateTime.UtcNow.Date.AddDays(-(double)maxDays);
            }

            List<Episode> episodes =
                _libraryManager.GetItemList(
                        new InternalItemsQuery
                        {
                            IncludeItemTypes = new[] { "Episode" },
                            IsVirtualItem = false,
                            Recursive = true,
                            MinPremiereDate = minPremiereDate,
                            OrderBy = new[]
                                {
                                    (ItemSortBy.SeriesSortName, SortOrder.Ascending),
                                    (ItemSortBy.SortName, SortOrder.Ascending)
                                }
                        }).Cast<Episode>().Where(i => MinutesSinceRefresh(i) > _pluginConfig.RefreshCooldownMinutes && NeedsRefresh(i)).ToList();

            var numComplete = 0;
            var numEpisodes = episodes.Count;
            if (numEpisodes > 0)
            {
                if (_pluginConfig.Pretend)
                {
                    _logger.LogInformation("Pretending to refresh {X} episodes", numEpisodes);
                }
                else
                {
                    _logger.LogInformation("Will refresh {X} episodes", numEpisodes);
                }

                foreach (Episode episode in episodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        _logger.LogInformation("Refreshing {Series} {SeasonNumber}x{EpisodeNumber:D2}", episode.SeriesName, episode.ParentIndexNumber, episode.IndexNumber);
                        LogWhatsMissingInfo(episode);
                        var minutesSinceRefreshed = (DateTime.UtcNow - episode.DateLastRefreshed).TotalMinutes;
                        if (!_pluginConfig.Pretend)
                        {
                            await episode.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);
                            // check if episode will pass or not after refresh
                            if (NeedsRefresh(episode))
                            {
                                _logger.LogInformation("{Series} {SeasonNumber}x{EpisodeNumber:D2} will need another refresh", episode.SeriesName, episode.ParentIndexNumber, episode.IndexNumber);
                                LogWhatsMissingInfo(episode);
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
                    percent /= numEpisodes;

                    progress.Report(100 * percent);
                }

                progress.Report(100);

                _logger.LogInformation("Sparse episode refresh completed");
            }
        }

        private string[] BadNamesAsArray()
        {
            return _pluginConfig.BadNames.Split("|", StringSplitOptions.RemoveEmptyEntries);
        }

        private void LogWhatsMissingInfo(Episode episode)
        {
            if (_pluginConfig.MissingImage && !episode.HasImage(ImageType.Primary))
            {
                _logger.LogInformation("    Episode missing primary image");
            }

            if (_pluginConfig.MissingOverview && string.IsNullOrWhiteSpace(episode.Overview))
            {
                _logger.LogInformation("    Episode missing overview");
            }

            if (_pluginConfig.MissingName && string.IsNullOrWhiteSpace(episode.Name))
            {
                _logger.LogInformation("    Episode missing name");
            }

            if (_pluginConfig.NameIsDate && IsDate(episode.Name))
            {
                _logger.LogInformation("    Episode name is a date");
            }

            if (_badNameList.Any(en => episode.Name.StartsWith(en, StringComparison.CurrentCultureIgnoreCase)))
            {
                _logger.LogInformation("    Episode name matches bad name list");
            }

            if (episode.ProviderIds.Count < _pluginConfig.MinimumProviderIds)
            {
                _logger.LogInformation("    Episode only has {X} provider IDs.", episode.ProviderIds.Count);
            }
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // no default schedule
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}
