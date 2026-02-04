#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
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
    public class RefreshSeriesTask : BaseRefreshTask, IScheduledTask
    {
        public RefreshSeriesTask(
            ILibraryManager libraryManager,
            IServerConfigurationManager config,
            ILogger<RefreshScheduledTask> logger,
            ILocalizationManager localization,
            IFileSystem fileSystem) : base(libraryManager, config, logger, localization, fileSystem)
        {
        }

        protected override string ItemTypeName => "series";

        public override string Name => Localization.GetLocalizedString("Refresh sparse series");

        public override string Description => Localization.GetLocalizedString("Refresh series with missing metadata based on requirements configured.");

        protected override IEnumerable<Series> GetItems()
        {
            return LibraryManager.GetItemList(
                new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true,
                    OrderBy = new[]
                        {
                            (ItemSortBy.SortName, SortOrder.Ascending)
                        }
                }).Cast<Series>().Where(i => DaysSinceRefresh(i) > PluginConfig.SeriesCooldownDays
                    && !SeriesBlockList.Any(sbl => i.Name.Equals(sbl, StringComparison.OrdinalIgnoreCase))
                    && NeedsRefresh(i));
        }

        protected override void LogWhatsMissingInfo(BaseItem item)
        {
            if (item.ProviderIds.Count < PluginConfig.SeriesMinimumProviderIds)
            {
                LogMissingProviders(item);
            }

            if (MissingOverview(item))
            {
                Logger.LogInformation("    missing overview");
            }

            if (MaybeEnded((Series)item))
            {
                Logger.LogInformation("    Continuing Series hasn't been refreshed in at least {0} days", PluginConfig.SeriesStatusDays);
            }

            if (MissingImage(item, ImageType.Primary, PluginConfig.SeriesPrimary))
            {
                Logger.LogInformation("    missing primary image");
            }

            if (MissingImage(item, ImageType.Art, PluginConfig.SeriesArt))
            {
                Logger.LogInformation("    missing clearart image");
            }

            if (MissingImage(item, ImageType.Banner, PluginConfig.SeriesBanner))
            {
                Logger.LogInformation("    missing banner image");
            }

            if (MissingImage(item, ImageType.Logo, PluginConfig.SeriesLogo))
            {
                Logger.LogInformation("    missing logo image");
            }

            if (MissingImage(item, ImageType.Thumb, PluginConfig.SeriesThumb))
            {
                Logger.LogInformation("    missing thumb image");
            }

            if (MissingImage(item, ImageType.Backdrop, PluginConfig.SeriesBackdrop))
            {
                Logger.LogInformation("    missing backdrop image");
            }
        }

        protected override bool NeedsRefresh(BaseItem item)
        {
            return item.ProviderIds.Count < PluginConfig.SeriesMinimumProviderIds
                || MissingOverview(item)
                || MaybeEnded((Series)item)
                || MissingImage(item, ImageType.Primary, PluginConfig.SeriesPrimary)
                || MissingImage(item, ImageType.Art, PluginConfig.SeriesArt)
                || MissingImage(item, ImageType.Banner, PluginConfig.SeriesBanner)
                || MissingImage(item, ImageType.Logo, PluginConfig.SeriesLogo)
                || MissingImage(item, ImageType.Thumb, PluginConfig.SeriesThumb)
                || MissingImage(item, ImageType.Backdrop, PluginConfig.SeriesBackdrop);
        }

        protected override bool GetReplaceAllImages()
        {
            return Plugin.Instance.Configuration.SeriesReplaceAllImages;
        }

        protected override bool GetReplaceAllMetadata()
        {
            return Plugin.Instance.Configuration.SeriesReplaceAllMetadata;
        }

        private bool MaybeEnded(Series series)
        {
            return PluginConfig.SeriesStatusDays != -1 && (series.Status != SeriesStatus.Ended && DaysSinceRefresh(series) > PluginConfig.SeriesStatusDays);
        }

        private bool MissingOverview(BaseItem item)
        {
            return PluginConfig.SeriesOverview && string.IsNullOrWhiteSpace(item.Overview);
        }
    }
}
