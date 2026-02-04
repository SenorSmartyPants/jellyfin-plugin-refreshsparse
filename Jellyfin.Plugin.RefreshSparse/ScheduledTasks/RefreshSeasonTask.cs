#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Globalization;
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
    public class RefreshSeasonTask : BaseRefreshTask, IScheduledTask
    {
        public RefreshSeasonTask(
            ILibraryManager libraryManager,
            IServerConfigurationManager config,
            ILogger<RefreshScheduledTask> logger,
            ILocalizationManager localization,
            IFileSystem fileSystem) : base(libraryManager, config, logger, localization, fileSystem)
        {
        }

        protected override string ItemTypeName => "season";

        public override string Name => Localization.GetLocalizedString("Refresh sparse seasons");

        public override string Description => Localization.GetLocalizedString("Refresh seasons with missing metadata based on requirements configured.");

        protected override IEnumerable<Season> GetItems()
        {
            return LibraryManager.GetItemList(
                new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Season },
                    IsVirtualItem = false,
                    Recursive = true,
                    OrderBy = new[]
                        {
                            (ItemSortBy.SeriesSortName, SortOrder.Ascending),
                            (ItemSortBy.SortName, SortOrder.Ascending)
                        }
                }).Cast<Season>().Where(i => DaysSinceRefresh(i) > PluginConfig.SeasonCooldownDays
                    && !SeriesBlockList.Any(sbl => i.SeriesName.Equals(sbl, StringComparison.OrdinalIgnoreCase))
                    && NeedsRefresh(i));
        }

        protected override void LogWhatsMissingInfo(BaseItem item)
        {
            if (item.ProviderIds.Count < PluginConfig.SeasonMinimumProviderIds)
            {
                LogMissingProviders(item);
            }

            if (MissingOverview(item))
            {
                Logger.LogInformation("    missing overview");
            }

            if (MissingName(item))
            {
                Logger.LogInformation("    missing name");
            }

            if (MissingImage(item, ImageType.Primary, PluginConfig.SeasonPrimary))
            {
                Logger.LogInformation("    missing primary image");
            }

            if (MissingImage(item, ImageType.Banner, PluginConfig.SeasonBanner))
            {
                Logger.LogInformation("    missing banner image");
            }

            if (MissingImage(item, ImageType.Thumb, PluginConfig.SeasonThumb))
            {
                Logger.LogInformation("    missing thumb image");
            }

            if (MissingImage(item, ImageType.Backdrop, PluginConfig.SeasonBackdrop))
            {
                Logger.LogInformation("    missing backdrop image");
            }
        }

        protected override bool NeedsRefresh(BaseItem item)
        {
            return item.ProviderIds.Count < PluginConfig.SeasonMinimumProviderIds
                || MissingOverview(item)
                || MissingName(item)
                || MissingImage(item, ImageType.Primary, PluginConfig.SeasonPrimary)
                || MissingImage(item, ImageType.Banner, PluginConfig.SeasonBanner)
                || MissingImage(item, ImageType.Thumb, PluginConfig.SeasonThumb)
                || MissingImage(item, ImageType.Backdrop, PluginConfig.SeasonBackdrop);
        }

        protected override bool GetReplaceAllImages()
        {
            return Plugin.Instance.Configuration.SeasonReplaceAllImages;
        }

        protected override bool GetReplaceAllMetadata()
        {
            return Plugin.Instance.Configuration.SeasonReplaceAllMetadata;
        }

        private bool MissingOverview(BaseItem item)
        {
            return PluginConfig.SeasonOverview && string.IsNullOrWhiteSpace(item.Overview);
        }

        private bool MissingName(BaseItem item)
        {
            return PluginConfig.SeasonName
                && (string.IsNullOrWhiteSpace(item.Name)
                || item.Name.StartsWith("Season", StringComparison.InvariantCulture)
                || item.Name.StartsWith("Specials", StringComparison.InvariantCulture));
        }

        protected override string GetItemName(BaseItem item)
        {
            Season season = (Season)item;
            return string.Format(CultureInfo.InvariantCulture, "{0} - S{1:00}. {2}", season.SeriesName, season.IndexNumber, season.Name);
        }
    }
}
