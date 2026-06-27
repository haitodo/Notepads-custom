// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads
{
    using System;
    using System.Collections.Concurrent;

    public class ResourceLoader
    {
        private static readonly string PriPath = Microsoft.Windows.ApplicationModel.Resources.ResourceLoader.GetDefaultResourceFilePath();
        private static readonly ConcurrentDictionary<string, Microsoft.Windows.ApplicationModel.Resources.ResourceLoader> Loaders = new();

        private readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader _defaultLoader;

        public ResourceLoader()
        {
            _defaultLoader = GetLoader("Resources");
        }

        public ResourceLoader(string name)
        {
            _defaultLoader = GetLoader(name);
        }

        private static Microsoft.Windows.ApplicationModel.Resources.ResourceLoader GetLoader(string name)
        {
            return Loaders.GetOrAdd(name, mapName => new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader(PriPath, mapName));
        }

        public string GetString(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return string.Empty;

            try
            {
                if (resourceId.StartsWith("/"))
                {
                    // UWP-style resource path parsing
                    // e.g. "/Settings/AdvancedPage_LanguagePreferenceSettings_SystemDefaultText"
                    var parts = resourceId.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var mapName = parts[0];
                        var remainingKey = string.Join("/", parts, 1, parts.Length - 1);
                        return GetLoader(mapName).GetString(remainingKey);
                    }
                }

                return _defaultLoader.GetString(resourceId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResourceLoader failed to get string for key '{resourceId}': {ex}");
                return string.Empty;
            }
        }
    }
}
