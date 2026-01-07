using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Reactive;
using Lang.Avalonia;
using MaaFramework.Binding.Buffers;
using MFAToolsPlus.Helper;
using MFAToolsPlus.Helper.Converters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MFAToolsPlus.Extensions;

public static class MFAExtensions
{
    public static string GetFallbackCommand()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "cmd.exe"
            : "/bin/bash";
    }

    public static string FormatWith(this string format, params object[] args)
    {
        return string.Format(format, args);
    }

    public static void AddRange<T>(this ICollection<T>? collection, IEnumerable<T> newItems)
    {
        if (collection == null)
            return;
        if (collection is List<T> objList)
        {
            objList.AddRange(newItems);
        }
        else
        {
            foreach (T newItem in newItems)
                collection.Add(newItem);
        }
    }

    extension(string? key)
    {
        public string ToLocalization()
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            return I18nManager.Instance.GetResource(key) ?? key;
        }

        public string ToLocalizationFormatted(bool transformKey = true, params string[] args)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            var localizedKey = key.ToLocalization();
            var processedArgs = transformKey
                ? Array.ConvertAll(args, a => a.ToLocalization() as object)
                : Array.ConvertAll(args, a => a as object);

            try
            {
                return Regex.Unescape(localizedKey.FormatWith(processedArgs));
            }
            catch
            {
                return localizedKey.FormatWith(processedArgs);
            }
        }
    }
    public static string GetName(this VersionChecker.VersionType type)
    {
        return type.ToString().ToLower();
    }
    
    public static VersionChecker.VersionType ToVersionType(this int version)
    {
        if (version == 0)
            return VersionChecker.VersionType.Alpha;
        if (version == 1) return VersionChecker.VersionType.Beta;
        return VersionChecker.VersionType.Stable;
    }
    
    public static Bitmap? ToBitmap(this MaaImageBuffer buffer)
    {
        if (buffer.IsInvalid || buffer.IsEmpty || !buffer.TryGetEncodedData(out Stream? encodedDataStream)) return null;

        try
        {

            encodedDataStream.Seek(0, SeekOrigin.Begin);
            return new Bitmap(encodedDataStream);

        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"Bitmap 创建失败: {ex.Message}");
            // 确保异常情况下也释放 Stream
            encodedDataStream?.Dispose();
            return null;
        }
    }
    
    public static bool CapturePointer(this IInputElement element, IPointer device)
    {
        device.Capture(element);
        return device.Captured == element;
    }

    public static void ReleasePointerCapture(this IInputElement element, IPointer device)
    {
        if (element != device.Captured)
            return;
        device.Capture((IInputElement) null);
    }

    public static T PeekOrDefault<T>(this ImmutableStack<T> stack)
    {
        return !stack.IsEmpty ? stack.Peek() : default (T);
    }

    public static IDisposable Subscribe<T>(this IObservable<T> observable, Action<T> action)
    {
        return observable.Subscribe((IObserver<T>) new AnonymousObserver<T>((Action<T>) action));
    }
    
        /// <summary>
    /// 生成ADB设备的指纹字符串，用于设备匹配
    /// 指纹由 Name + AdbPath + Index 组成，可以稳定识别同一个模拟器实例
    /// </summary>
    /// <param name="device">ADB设备信息</param>
    /// <returns>设备指纹字符串</returns>
    public static string GenerateDeviceFingerprint(this MaaFramework.Binding.AdbDeviceInfo device)
    {
        var index = DeviceDisplayConverter.GetFirstEmulatorIndex(device.Config);
        return GenerateDeviceFingerprint(device.Name, device.AdbPath, index);
    }

    /// <summary>
    /// 生成ADB设备的指纹字符串
    /// </summary>
    /// <param name="name">设备名称</param>
    /// <param name="adbPath">ADB路径</param>
    /// <param name="index">模拟器索引（-1表示无索引）</param>
    /// <returns>设备指纹字符串</returns>
    public static string GenerateDeviceFingerprint(string name, string adbPath, int index)
    {
        // 规范化AdbPath：只保留文件名部分，忽略路径差异
        var normalizedAdbPath = adbPath;

        // 指纹格式：Name|AdbPath|Index
        return $"{name}|{normalizedAdbPath}|{index}";
    }

    /// <summary>
    /// 比较两个设备是否匹配（基于指纹）
    /// 当任一方 index 为 -1 时，只比较 Name 和 AdbPath
    /// </summary>
    /// <param name="device">当前设备</param>
    /// <param name="savedDevice">保存的设备</param>
    /// <returns>是否匹配</returns>
    public static bool MatchesFingerprint(this MaaFramework.Binding.AdbDeviceInfo device, MaaFramework.Binding.AdbDeviceInfo savedDevice)
    {
        var deviceIndex = DeviceDisplayConverter.GetFirstEmulatorIndex(device.Config);
        var savedIndex = DeviceDisplayConverter.GetFirstEmulatorIndex(savedDevice.Config);

        // 比较 Name 和 AdbPath
        bool nameMatches = device.Name == savedDevice.Name;
        bool adbPathMatches = device.AdbPath == savedDevice.AdbPath;

        // 如果 Name 或 AdbPath 不匹配，直接返回 false
        if (!nameMatches || !adbPathMatches) return false;

        // 如果任一方 index 为 -1，则不比较 index，只要 Name 和 AdbPath 匹配即可
        if (deviceIndex == -1 || savedIndex == -1)
            return true;

        // 两方 index 都有效时，需要 index 也匹配
        return deviceIndex == savedIndex;
    }
}
