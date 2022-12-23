#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using static Jellyfin.Plugin.RefreshSparse.Common.Utils;

namespace Jellyfin.Plugin.RefreshSparse
{
    public class RefreshScheduledTask : BaseRefreshTask, IScheduledTask
    {
        private string[] _badNameList;
        private string[] _seriesBlockList;

        public RefreshScheduledTask(
            ILibraryManager libraryManager,
            IServerConfigurationManager config,
            ILogger<RefreshScheduledTask> logger,
            ILocalizationManager localization,
            IFileSystem fileSystem) : base(libraryManager, config, logger, localization, fileSystem)
        {
            _badNameList = SplitToArray(PluginConfig.BadNames);
            _seriesBlockList = SplitToArray(PluginConfig.SeriesBlockList);
        }

        protected override string ItemTypeName => "episodes";

        public override string Name => Localization.GetLocalizedString("Refresh sparse episodes");

        public override string Description => Localization.GetLocalizedString("Refresh episodes with missing metadata based on requirements configured.");

        protected override bool NeedsRefresh(BaseItem item)
        {
            return (PluginConfig.MissingImage && !item.HasImage(ImageType.Primary))
                || (PluginConfig.MissingOverview && string.IsNullOrWhiteSpace(item.Overview))
                || (PluginConfig.MissingName && string.IsNullOrWhiteSpace(item.Name))
                || (PluginConfig.NameIsDate && IsDate(item.Name))
                || _badNameList.Any(en => item.Name.StartsWith(en, StringComparison.CurrentCultureIgnoreCase))
                || item.ProviderIds.Count < PluginConfig.MinimumProviderIds;
        }

        protected override IEnumerable<Episode> GetItems()
        {
            _badNameList = SplitToArray(PluginConfig.BadNames);
            _seriesBlockList = SplitToArray(PluginConfig.SeriesBlockList);

            // episodes that aired in the past MaxDays days
            // or added to JF in MaxDays
            DateTime? minDate = null;
            var maxDays = PluginConfig.MaxDays;
            if (maxDays > -1)
            {
                minDate = DateTime.UtcNow.Date.AddDays(-(double)maxDays);
            }

            return LibraryManager.GetItemList(
                new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Episode },
                    IsVirtualItem = false,
                    Recursive = true,
                    MinDateCreated = minDate,
                    OrderBy = new[]
                        {
                            (ItemSortBy.SeriesSortName, SortOrder.Ascending),
                            (ItemSortBy.SortName, SortOrder.Ascending)
                        }
                }).Cast<Episode>().Where(i => (maxDays == -1 || i.PremiereDate >= minDate || !i.PremiereDate.HasValue)
                    && MinutesSinceRefresh(i) > PluginConfig.RefreshCooldownMinutes
                    && !_seriesBlockList.Any(sbl => i.SeriesName.Equals(sbl, StringComparison.OrdinalIgnoreCase))
                    && NeedsRefresh(i));
        }

        protected override void LogWhatsMissingInfo(BaseItem item)
        {
            if (PluginConfig.MissingImage && !item.HasImage(ImageType.Primary))
            {
                Logger.LogInformation("    Episode missing primary image");
            }

            if (PluginConfig.MissingOverview && string.IsNullOrWhiteSpace(item.Overview))
            {
                Logger.LogInformation("    Episode missing overview");
            }

            if (PluginConfig.MissingName && string.IsNullOrWhiteSpace(item.Name))
            {
                Logger.LogInformation("    Episode missing name");
            }

            if (PluginConfig.NameIsDate && IsDate(item.Name))
            {
                Logger.LogInformation("    Episode name is a date");
            }

            if (_badNameList.Any(en => item.Name.StartsWith(en, StringComparison.CurrentCultureIgnoreCase)))
            {
                Logger.LogInformation("    Episode name matches bad name list");
            }

            if (item.ProviderIds.Count < PluginConfig.MinimumProviderIds)
            {
                Logger.LogInformation("    Episode only has {X} provider IDs.", item.ProviderIds.Count);
            }
        }

        public override string GetItemName(BaseItem item)
        {
            Episode episode = (Episode)item;
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}x{2:D2}", episode.SeriesName, episode.ParentIndexNumber, episode.IndexNumber);
        }
    }
}
