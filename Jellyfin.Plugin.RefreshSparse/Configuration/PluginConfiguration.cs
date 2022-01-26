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
            Pretend = true;
            RefreshCooldownMinutes = 60;
            // all is being overwritten currently in 10.8 regardless of these settings.
            ReplaceAllImages = false;
            ReplaceAllMetadata = false;
        }

        public int MaxDays { get; set; }

        public int MinimumProviderIds { get; set; }

        public bool MissingImage { get; set; }

        public bool MissingName { get; set; }

        public bool MissingOverview { get; set; }

        public bool NameIsDate { get; set; }

        public string BadNames { get; set; }

        public bool Pretend { get; set; }

        public int RefreshCooldownMinutes { get; set; }

        public bool ReplaceAllImages { get; set; }

        public bool ReplaceAllMetadata { get; set; }
    }
}
