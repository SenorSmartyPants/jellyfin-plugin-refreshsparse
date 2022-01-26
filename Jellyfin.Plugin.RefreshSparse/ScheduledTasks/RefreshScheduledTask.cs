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

        private double MinutesSinceRefresh(Episode episode)
        {
            return (DateTime.UtcNow - episode.DateLastRefreshed).TotalMinutes;
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
                            IncludeItemTypes = new[] { "Episode" },
                            IsVirtualItem = false,
                            Recursive = true,
                            MinPremiereDate = minPremiereDate,
                            OrderBy = new[]
                                {
                                    (ItemSortBy.SeriesSortName, SortOrder.Ascending),
                                    (ItemSortBy.SortName, SortOrder.Ascending)
                                }
                        }).Cast<Episode>().ToList();

            var badNameList = BadNamesAsArray(config);

            episodes = episodes.Where(i => MinutesSinceRefresh(i) > config.RefreshCooldownMinutes &&
                ((config.MissingImage && !i.HasImage(ImageType.Primary))
                || (config.MissingOverview && string.IsNullOrWhiteSpace(i.Overview))
                || (config.MissingName && string.IsNullOrWhiteSpace(i.Name))
                || (config.NameIsDate && IsDate(i.Name))
                || badNameList.Any(en => i.Name.StartsWith(en, StringComparison.CurrentCultureIgnoreCase))
                || i.ProviderIds.Count < config.MinimumProviderIds)).ToList();
            // DateLastRefreshed - don't hammer metadata provider, won't be new info

            // when called from item refresh metadata menu, these are always full. Unless scan for only new/updated files
            var metadataRefreshMode = MetadataRefreshMode.FullRefresh;
            var imageRefreshMode = MetadataRefreshMode.FullRefresh;

            var refreshOptions = new MetadataRefreshOptions(new DirectoryService(_fileSystem))
            {
                MetadataRefreshMode = metadataRefreshMode,
                ImageRefreshMode = imageRefreshMode,
                ReplaceAllImages = config.ReplaceAllImages,
                ReplaceAllMetadata = config.ReplaceAllMetadata,
                ForceSave = metadataRefreshMode == MetadataRefreshMode.FullRefresh
                    || imageRefreshMode == MetadataRefreshMode.FullRefresh
                    || config.ReplaceAllImages
                    || config.ReplaceAllMetadata,
                IsAutomated = false
            };

            var numComplete = 0;

            var numEpisodes = episodes.Count;

            if (numEpisodes > 0)
            {
                if (config.Pretend)
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
                        LogInfo(episode);
                        var minutesSinceRefreshed = (DateTime.UtcNow - episode.DateLastRefreshed).TotalMinutes;
                        if (config.RefreshCooldownMinutes > 0 && minutesSinceRefreshed < config.RefreshCooldownMinutes)
                        {
                            _logger.LogInformation("    Skipping refresh: episode was refreshed {X:N0} minutes ago",  minutesSinceRefreshed );
                        }
                        else
                        {
                            if (!config.Pretend)
                            {
                                await episode.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);
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

        private string[] BadNamesAsArray(PluginConfiguration config)
        {
            return config.BadNames.Split("|", StringSplitOptions.RemoveEmptyEntries);
        }

        private void LogInfo(Episode episode)
        {
            var config = Plugin.Instance.Configuration;
            var badNameList = BadNamesAsArray(config);

            _logger.LogInformation("Refreshing {Series} {SeasonNumber}x{EpisodeNumber:D2}", episode.SeriesName, episode.ParentIndexNumber, episode.IndexNumber);
            if (config.MissingImage && !episode.HasImage(ImageType.Primary))
            {
                _logger.LogInformation("    Episode missing primary image");
            }

            if (config.MissingOverview && string.IsNullOrWhiteSpace(episode.Overview))
            {
                _logger.LogInformation("    Episode missing overview");
            }

            if (config.MissingName && string.IsNullOrWhiteSpace(episode.Name))
            {
                _logger.LogInformation("    Episode missing name");
            }

            if (config.NameIsDate && IsDate(episode.Name))
            {
                _logger.LogInformation("    Episode name is a date");
            }

            if (badNameList.Any(en => episode.Name.StartsWith(en, StringComparison.CurrentCultureIgnoreCase)))
            {
                _logger.LogInformation("    Episode name matches bad name list");
            }

            if (episode.ProviderIds.Count < config.MinimumProviderIds)
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
