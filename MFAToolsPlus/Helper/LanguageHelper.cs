using Lang.Avalonia;
using MFAToolsPlus.Configuration;
using MFAToolsPlus.Helper.Other;
using MFAToolsPlus.Localization;
using Newtonsoft.Json;
using SukiUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MFAToolsPlus.Helper;

public static class LanguageHelper
{
    public static event EventHandler<LanguageEventArgs>? LanguageChanged;

    public static readonly List<SupportedLanguage> SupportedLanguages =
    [
        new("zh-CN", "简体中文"),
        new("en-US", "English"),
    ];

    public static Dictionary<string, CultureInfo> Cultures { get; } = new();

    public static SupportedLanguage GetLanguage(string key)
    {
        return SupportedLanguages.FirstOrDefault(lang => lang.Key == key, SupportedLanguages[0]);
    }

    public static void ChangeLanguage(SupportedLanguage language)
    {
        // 设置应用程序的文化
        I18nManager.Instance.Culture = Cultures.TryGetValue(language.Key, out var culture)
            ? culture
            : Cultures[language.Key] = new CultureInfo(language.Key);
        _currentLanguage = language.Key;
        CultureInfo.CurrentCulture = I18nManager.Instance.Culture ?? CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = I18nManager.Instance.Culture ?? CultureInfo.InvariantCulture;
        SukiTheme.GetInstance().Locale = I18nManager.Instance.Culture;
        LanguageChanged?.Invoke(null, new LanguageEventArgs(language));
    }

    public static void ChangeLanguage(string language)
    {
        I18nManager.Instance.Culture = Cultures.TryGetValue(language, out var culture)
            ? culture
            : Cultures[language] = new CultureInfo(language);
        _currentLanguage = language;
        CultureInfo.CurrentCulture = I18nManager.Instance.Culture ?? CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = I18nManager.Instance.Culture ?? CultureInfo.InvariantCulture;
        SukiTheme.GetInstance().Locale = I18nManager.Instance.Culture;
        LanguageChanged?.Invoke(null, new LanguageEventArgs(GetLanguage(language)));
    }

    // 存储语言的字典
    private static readonly Dictionary<string, Dictionary<string, string>> Langs = new();
    private static string _currentLanguage = ConfigurationManager.Current.GetValue(ConfigurationKeys.CurrentLanguage, LanguageHelper.SupportedLanguages[0].Key, ["zh-CN", "zh-Hant", "en-US"]);
    public static string CurrentLanguage => _currentLanguage;
    public static void Initialize()
    {
        LoggerHelper.Info("Initializing LanguageManager...");
        var plugin = new MFAResxLangPlugin();
        var defaultCulture = CultureInfo.CurrentUICulture;
        I18nManager.Instance.Register(
            plugin, // 格式插件
            defaultCulture: defaultCulture, // 默认语言
            error: out var error // 错误信息（可选）
        );
        if (!plugin.IsLoaded)
        {
            plugin.Load(defaultCulture);
        }
    }


    private static bool IsSimplifiedChinese(string langCode)
    {
        string[] simplifiedPrefixes =
        [
            "zh-hans",
            "zh-cn",
            "zh-sg"
        ];
        foreach (string prefix in simplifiedPrefixes)
        {
            if (langCode.Replace("_", "-").StartsWith(prefix))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTraditionalChinese(string langCode)
    {
        string[] traditionalPrefixes =
        [
            "zh-hant",
            "zh-tw",
            "zh-hk",
            "zh-mo"
        ];
        foreach (string prefix in traditionalPrefixes)
        {
            if (langCode.Replace("_", "-").StartsWith(prefix))
            {
                return true;
            }
        }
        return false;
    }

    public class LanguageEventArgs(SupportedLanguage language) : EventArgs
    {
        public SupportedLanguage Value { get; set; } = language;
    }
}
