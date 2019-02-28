using System.Collections.Generic;

namespace Assets
{
    sealed class BossModuleSettings
    {
        public string SiteUrl = @"https://ktane.timwi.de/json/raw";

        public Dictionary<string, string[]> IgnoredModules = new Dictionary<string, string[]>();

        public int Version = 1;
    }
}
