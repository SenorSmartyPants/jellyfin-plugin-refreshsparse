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
        }

        public int MaxDays { get; set; }

        public int MinimumProviderIds { get; set; }

        public bool MissingImage { get; set; }

        public bool MissingName { get; set; }

        public bool MissingOverview { get; set; }

        public bool NameIsDate { get; set; }

        public string BadNames { get; set; }

        public bool Pretend { get; set; }
    }
}
