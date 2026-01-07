using CommunityToolkit.Mvvm.ComponentModel;
using MFAToolsPlus.Helper;
using Newtonsoft.Json;

namespace MFAToolsPlus.Extensions.MaaFW;

public partial class MaaInterface : ObservableObject
{
    public partial class MaaResourceController : ObservableObject
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("label")]
        public string? Label { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("icon")]
        public string? Icon { get; set; }

        [JsonProperty("display_short_side")]
        public long? DisplayShortSide { get; set; }

        [JsonProperty("display_long_side")]
        public long? DisplayLongSide { get; set; }

        [JsonProperty("display_raw")]
        public bool? DisplayRaw { get; set; }

        [JsonProperty("adb")]
        public MaaResourceControllerAdb? Adb { get; set; }
        [JsonProperty("win32")]
        public MaaResourceControllerWin32? Win32 { get; set; }
        [JsonProperty("playcover")]
        public MaaResourceControllerPlayCover? PlayCover { get; set; }

        /// <summary>显示名称（用于 UI 绑定）</summary>
        [ObservableProperty] [JsonIgnore] private string _displayName = string.Empty;

        [ObservableProperty] [JsonIgnore] private bool _hasDescription;

        [ObservableProperty] [JsonIgnore] private string _displayDescription = string.Empty;

        /// <summary>解析后的图标路径（用于 UI 绑定）</summary>
        [ObservableProperty] [JsonIgnore] private string? _resolvedIcon;

        /// <summary>是否有图标</summary>
        [ObservableProperty] [JsonIgnore] private bool _hasIcon;

        /// <summary>控制器类型枚举值</summary>
        [JsonIgnore]
        public MaaControllerTypes ControllerType => Type.ToMaaControllerTypes();

        /// <summary>
        /// 初始化显示名称并注册语言变化监听
        /// </summary>
        public void InitializeDisplayName()
        {
            UpdateDisplayName();
            LanguageHelper.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, LanguageHelper.LanguageEventArgs e)
        {
            UpdateDisplayName();
        }

        private void UpdateDisplayName()
        {
            // 如果有Label，使用Label的国际化；否则使用Name或默认的控制器类型名称
            if (!string.IsNullOrWhiteSpace(Label))
            {
                DisplayName = Label.ToLocalization();
            }
            else if (!string.IsNullOrWhiteSpace(Name))
            {
                DisplayName = Name.ToLocalization();
            }
            else
            {
                // 使用默认的控制器类型名称
                DisplayName = ControllerType.ToResourceKey().ToLocalization();
            }
        }
        
    }
    public class MaaResourceControllerAdb
    {
        [JsonProperty("input")]
        public long? Input { get; set; }
        [JsonProperty("screencap")]
        public long? ScreenCap { get; set; }
        [JsonProperty("config")]
        public object? Adb { get; set; }
    }

    public class MaaResourceControllerPlayCover
    {
        [JsonProperty("uuid")]
        public string? Uuid { get; set; }
    }

    public class MaaResourceControllerWin32
    {
        [JsonProperty("class_regex")]
        public string? ClassRegex { get; set; }
        [JsonProperty("window_regex")]
        public string? WindowRegex { get; set; }
        [JsonProperty("input")]
        public object? Input { get; set; }
        [JsonProperty("mouse")]
        public object? Mouse { get; set; }
        [JsonProperty("keyboard")]
        public object? Keyboard { get; set; }
        [JsonProperty("screencap")]
        public object? ScreenCap { get; set; }
    }

}
