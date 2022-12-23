#pragma warning disable CS1591

using System;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.RefreshSparse.Common
{
    public static class Utils
    {
        public static string[] SplitToArray(string stringList)
        {
            return stringList.Split("|", StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool IsDate(string possibleDate)
        {
            DateTime d;
            return DateTime.TryParse(possibleDate, out d);
        }

        public static double MinutesSinceRefresh(BaseItem item)
        {
            return (DateTime.UtcNow - item.DateLastRefreshed).TotalMinutes;
        }
    }
}
