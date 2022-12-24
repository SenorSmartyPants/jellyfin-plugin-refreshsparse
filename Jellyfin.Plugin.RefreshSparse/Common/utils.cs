#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.RefreshSparse.Common
{
    public static class Utils
    {
        public static IEnumerable<string> SplitToArray(string stringList)
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

        public static double DaysSinceRefresh(BaseItem item)
        {
            return (DateTime.UtcNow - item.DateLastRefreshed).TotalDays;
        }
    }
}
