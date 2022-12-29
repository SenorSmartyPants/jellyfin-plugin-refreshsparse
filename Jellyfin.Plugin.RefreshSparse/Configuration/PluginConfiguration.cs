#pragma warning disable CS1591

using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RefreshSparse.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            // set default options here
            MaxDays = 14;
            MinimumProviderIds = 0;
            MissingImage = true;
            MissingName = true;
            MissingOverview = true;
            NameIsDate = false;
            BadNames = string.Empty;
            OverviewBadName = false;
            SeriesBlockList = string.Empty;
            Pretend = true;
            RefreshCooldownMinutes = 60;
            // all is being overwritten currently in 10.8 regardless of these settings.
            ReplaceAllImages = false;
            ReplaceAllMetadata = false;

            // series defaults
            SeriesCooldownDays = 30;
            SeriesStatusDays = 180;
            SeriesMinimumProviderIds = 0;
            SeriesOverview = true;
            SeriesPrimary = true;
            SeriesArt = false;
            SeriesBanner = false;
            SeriesLogo = false;
            SeriesThumb = false;
            SeriesBackdrop = true;
            SeriesReplaceAllImages = false;
            SeriesReplaceAllMetadata = false;

            // season defaults
            SeasonCooldownDays = 30;
            SeasonMinimumProviderIds = 0;
            SeasonOverview = true;
            SeasonName = false;
            SeasonPrimary = true;
            SeasonBanner = false;
            SeasonThumb = false;
            SeasonBackdrop = false;
            SeasonReplaceAllImages = false;
            SeasonReplaceAllMetadata = false;
        }

        public int MaxDays { get; set; }

        public int MinimumProviderIds { get; set; }

        public bool MissingImage { get; set; }

        public bool MissingName { get; set; }

        public bool MissingOverview { get; set; }

        public bool NameIsDate { get; set; }

        public string BadNames { get; set; }

        public bool OverviewBadName { get; set; }

        public bool Pretend { get; set; }

        public int RefreshCooldownMinutes { get; set; }

        public bool ReplaceAllImages { get; set; }

        public bool ReplaceAllMetadata { get; set; }

        public string SeriesBlockList { get; set; }

        // Series settings
        public int SeriesCooldownDays { get; set; }

        public int SeriesStatusDays { get; set; }

        public int SeriesMinimumProviderIds { get; set; }

        public bool SeriesOverview { get; set; }

        public bool SeriesPrimary { get; set; }

        public bool SeriesArt { get; set; }

        public bool SeriesBanner { get; set; }

        public bool SeriesLogo { get; set; }

        public bool SeriesThumb { get; set; }

        public bool SeriesBackdrop { get; set; }

        public bool SeriesReplaceAllImages { get; set; }

        public bool SeriesReplaceAllMetadata { get; set; }

        // Season settings
        public int SeasonCooldownDays { get; set; }

        public int SeasonMinimumProviderIds { get; set; }

        public bool SeasonOverview { get; set; }

        public bool SeasonName { get; set; }

        public bool SeasonPrimary { get; set; }

        public bool SeasonBanner { get; set; }

        public bool SeasonThumb { get; set; }

        public bool SeasonBackdrop { get; set; }

        public bool SeasonReplaceAllImages { get; set; }

        public bool SeasonReplaceAllMetadata { get; set; }
    }
}
