// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using ApplicationLanguages = Microsoft.Windows.Globalization.ApplicationLanguages;

    public class LanguageItem
    {
        private string _id;

        public string ID
        {
            get => _id;
            set
            {
                _id = value;
                Name = string.IsNullOrEmpty(value)
                    ? new ResourceLoader().GetString("/Settings/AdvancedPage_LanguagePreferenceSettings_SystemDefaultText")
                    : new CultureInfo(value).NativeName;
            }
        }

        public string Name { get; private set; }
    }

    public static class LanguageUtility
    {
        public static readonly string CurrentLanguageID = ApplicationLanguages.PrimaryLanguageOverride;

        public static IReadOnlyCollection<LanguageItem> GetSupportedLanguageItems()
        {
            var supportedLanguageList = new List<LanguageItem>() { new LanguageItem() { ID = string.Empty } };

            if (App.IsPackaged)
            {
                supportedLanguageList.AddRange(ApplicationLanguages.ManifestLanguages
                    .Select(languageId => new LanguageItem() { ID = languageId }));
            }
            else
            {
                // Fallback language list for unpackaged environment
                string[] unpackagedLanguages = new[]
                {
                    "ar-YE", "bg-BG", "cs-CZ", "de-CH", "de-DE", "en-US", "es-ES", "fi-FI",
                    "fr-FR", "hi-IN", "hr-HR", "hu-HU", "it-IT", "ja-JP", "ka-GE", "ko-KR",
                    "nl-NL", "or-IN", "pl-PL", "pt-BR", "pt-PT", "ru-RU", "sr-Latn", "sr-cyrl",
                    "tr-TR", "uk-UA", "vi-VN", "zh-CN", "zh-TW"
                };
                supportedLanguageList.AddRange(unpackagedLanguages
                    .Select(languageId => new LanguageItem() { ID = languageId }));
            }

            return supportedLanguageList;
        }
    }
}