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
        private IEnumerable<string> _badNameList;

        public RefreshScheduledTask(
            ILibraryManager libraryManager,
            IServerConfigurationManager config,
            ILogger<RefreshScheduledTask> logger,
            ILocalizationManager localization,
            IFileSystem fileSystem) : base(libraryManager, config, logger, localization, fileSystem)
        {
            _badNameList = SplitToArray(PluginConfig.BadNames);
        }

        protected override string ItemTypeName => "episodes";

        public override string Name => Localization.GetLocalizedString("Refresh sparse episodes");

        public override string Description => Localization.GetLocalizedString("Refresh episodes with missing metadata based on requirements configured.");

        protected override IEnumerable<Episode> GetItems()
        {
            _badNameList = SplitToArray(PluginConfig.BadNames);

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
                    && !SeriesBlockList.Any(sbl => i.SeriesName.Equals(sbl, StringComparison.OrdinalIgnoreCase))
                    && NeedsRefresh(i));
        }

        protected override void LogWhatsMissingInfo(BaseItem item)
        {
            if (item.ProviderIds.Count < PluginConfig.MinimumProviderIds)
            {
                LogMissingProviders(item);
            }

            if (MissingOverview(item))
            {
                Logger.LogInformation("    missing overview");
            }

            if (OverviewBadName(item))
            {
                Logger.LogInformation("    overview contains a bad name");
            }

            if (MissingName(item))
            {
                Logger.LogInformation("    missing name");
            }

            if (NameIsDate(item))
            {
                Logger.LogInformation("    name is a date");
            }

            if (BadName(item))
            {
                Logger.LogInformation("    name starts with a bad name");
            }

            if (MissingImage(item, ImageType.Primary, PluginConfig.MissingImage))
            {
                Logger.LogInformation("    missing primary image");
            }
        }

        protected override bool NeedsRefresh(BaseItem item)
        {
            return item.ProviderIds.Count < PluginConfig.MinimumProviderIds
                || MissingOverview(item)
                || MissingName(item)
                || NameIsDate(item)
                || BadName(item)
                || OverviewBadName(item)
                || MissingImage(item, ImageType.Primary, PluginConfig.MissingImage);
        }

        protected override bool GetReplaceAllImages()
        {
            return Plugin.Instance.Configuration.ReplaceAllImages;
        }

        protected override bool GetReplaceAllMetadata()
        {
            return Plugin.Instance.Configuration.ReplaceAllMetadata;
        }

        private bool MissingOverview(BaseItem item)
        {
            return PluginConfig.MissingOverview && string.IsNullOrWhiteSpace(item.Overview);
        }

        private bool MissingName(BaseItem item)
        {
            return PluginConfig.MissingName && string.IsNullOrWhiteSpace(item.Name);
        }

        private bool NameIsDate(BaseItem item)
        {
            return PluginConfig.NameIsDate && IsDate(item.Name);
        }

        private bool BadName(BaseItem item)
        {
            return _badNameList.Any(en => item.Name is not null && item.Name.StartsWith(en, StringComparison.CurrentCultureIgnoreCase));
        }

        private bool OverviewBadName(BaseItem item)
        {
            return PluginConfig.OverviewBadName && _badNameList.Any(en => item.Overview is not null && item.Overview.Contains(en, StringComparison.CurrentCultureIgnoreCase));
        }

        protected override string GetItemName(BaseItem item)
        {
            Episode episode = (Episode)item;
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}x{2:D2}", episode.SeriesName, episode.ParentIndexNumber, episode.IndexNumber);
        }
    }
}
