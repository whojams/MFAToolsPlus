using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using MaaFramework.Binding;
using MFAToolsPlus.Configuration;
using MFAToolsPlus.Helper;
using MFAToolsPlus.Helper.Other;


namespace MFAToolsPlus.ViewModels.UsersControls.Settings;

public partial class PerformanceUserControlModel : ViewModelBase
{
    private bool _gpuInitCompleted;

    protected override void Initialize()
    {
        _gpuInitCompleted = false;
        GpuOption = GpuOptions[GpuIndex].Other;
        _gpuInitCompleted = true;
        base.Initialize();
    }
    //禁用切换GPU
    [ObservableProperty] public bool _isDirectMLSupported = false;

    [ObservableProperty] private bool _useDirectML = ConfigurationManager.Current.GetValue(ConfigurationKeys.UseDirectML, false);

    public class DirectMLAdapterInfo
    {
        public int AdapterId { get; set; } // 与EnumAdapters1索引一致
        public string AdapterName { get; set; }
        public bool IsDirectMLCompatible { get; set; }
    }

    partial void OnUseDirectMLChanged(bool value) => HandlePropertyChanged(ConfigurationKeys.UseDirectML, value, (v) =>
    {
        if (!IsDirectMLSupported)
            return;
        if (v)
        {
        }
        else
        {
            if (GpuOptions.Count != 2)
            {
                if (GpuIndex == GpuOptions.Count - 1)
                {
                    GpuIndex = 1;
                }
                if (GpuIndex > 0 && GpuIndex < GpuOptions.Count - 1)
                {
                    GpuIndex = 0;
                    MaaProcessor.Instance.SetTasker();
                }
                while (GpuOptions.Count > 2)
                {
                    GpuOptions.RemoveAt(1);
                }

                ConfigurationManager.Current.SetValue(ConfigurationKeys.GPUs, GpuOptions);
            }
        }

    });

    public class GpuDeviceOption
    {
        public static GpuDeviceOption Auto = new(InferenceDevice.Auto);
        public static GpuDeviceOption CPU = new(InferenceDevice.CPU);
        public static GpuDeviceOption GPU0 = new(InferenceDevice.GPU0);
        public static GpuDeviceOption GPU1 = new(InferenceDevice.GPU1);
        public GpuDeviceOption()
        {

        }
        public GpuDeviceOption(InferenceDevice device)
        {
            Device = device;
            IsDirectML = false;
        }
        public GpuDeviceOption(DirectMLAdapterInfo adapter)
        {
            Adapter = adapter;
            IsDirectML = true;
        }
        public InferenceDevice Device;
        public DirectMLAdapterInfo Adapter;
        public bool IsDirectML;
    }

    [ObservableProperty] private AvaloniaList<LocalizationBlock<GpuDeviceOption>> _gpuOptions =
    [
        new("GpuOptionAuto")
        {
            Other = GpuDeviceOption.Auto,
        },
        new("GpuOptionDisable")
        {
            Other = GpuDeviceOption.CPU
        }
    ];
    // ConfigurationManager.Current.GetValue(ConfigurationKeys.GPUs, new AvaloniaList<LocalizationViewModel<GpuDeviceOption>>
    // {
    //     new("GpuOptionAuto")
    //     {
    //         Other = GpuDeviceOption.Auto,
    //     },
    //     new("GpuOptionDisable")
    //     {
    //         Other = GpuDeviceOption.CPU
    //     }
    // }, null, new UniversalEnumConverter<InferenceDevice>());

    partial void OnGpuOptionsChanged(AvaloniaList<LocalizationBlock<GpuDeviceOption>> value) => HandlePropertyChanged(ConfigurationKeys.GPUs, value);

    [ObservableProperty] private int _gpuIndex = ConfigurationManager.Current.GetValue(ConfigurationKeys.GPUOption, 0);
    partial void OnGpuIndexChanged(int value) => HandlePropertyChanged(ConfigurationKeys.GPUOption, value, () =>
    {
        GpuOption = GpuOptions[value].Other;
    });

    [ObservableProperty] private GpuDeviceOption? _gpuOption;

    partial void OnGpuOptionChanged(GpuDeviceOption? value)
    {
        if (!_gpuInitCompleted)
        {
            return;
        }

        if (!Instances.IsResolved<RootViewModel>())
        {
            DispatcherHelper.PostOnMainThread(() =>
            {
                if (_gpuInitCompleted && Instances.IsResolved<RootViewModel>())
                {
                    ChangeGpuOption(MaaProcessor.Instance.MaaTasker?.Resource, value);
                    MaaProcessor.Instance.SetTasker();
                }
            });
            return;
        }

        ChangeGpuOption(MaaProcessor.Instance.MaaTasker?.Resource, value);
        MaaProcessor.Instance.SetTasker();
    }

    public void ChangeGpuOption(MaaResource? resource, GpuDeviceOption? option)
    {
        // LoggerHelper.Info($"MaaResource: {resource != null}");
        // LoggerHelper.Info($"GpuDeviceOption: {option != null}");
        if (option != null && resource != null)
        {
            if (option.IsDirectML)
            {
                var v1 = resource.SetOption_InferenceExecutionProvider(InferenceExecutionProvider.DirectML);
                var v2 = resource.SetOption_InferenceDevice(option.Adapter.AdapterId);
                LoggerHelper.Info($"{"Use DirectML: " + (v1 && v2 ? "succeed" : "failed")}");
            }
            else if (option.Device == InferenceDevice.CPU)
            {
                var v1 = resource.SetOption_InferenceExecutionProvider(InferenceExecutionProvider.CPU);
                var v2 = resource.SetOption_InferenceDevice(option.Device);
                LoggerHelper.Info($"{"Use CPU: " + (v1 && v2 ? "succeed" : "failed")}");
            }
            else
            {
                var v1 = resource.SetOption_InferenceExecutionProvider(InferenceExecutionProvider.Auto);
                var v2 = resource.SetOption_InferenceDevice(option.Device);
                LoggerHelper.Info($"{"Use GPU: " + (v1 && v2 ? "succeed" : "failed")}");
            }
        }
    }
}
