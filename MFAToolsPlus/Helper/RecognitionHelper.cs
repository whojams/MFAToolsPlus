using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MFAToolsPlus.Extensions;
using MFAToolsPlus.Extensions.MaaFW.Custom;
using Newtonsoft.Json;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.IO;

namespace MFAToolsPlus.Helper;

public static class RecognitionHelper
{
    public sealed class RecognitionQuery
    {
        [JsonProperty("all")]
        public List<RecognitionResult>? All { get; set; }

        [JsonProperty("best")]
        public RecognitionResult? Best { get; set; }

        [JsonProperty("filtered")]
        public List<RecognitionResult>? Filtered { get; set; }
    }

    public sealed class RecognitionResult
    {
        [JsonProperty("box")]
        public List<int>? Box { get; set; }

        [JsonProperty("score")]
        public double? Score { get; set; }

        [JsonProperty("text")]
        public string? Text { get; set; }
    }

    public static string BuildAppendOcrPayload(int x, int y, int width, int height)
    {
        var payload = new
        {
            AppendOCR = new
            {
                recognition = "OCR",
                roi = new[]
                {
                    x,
                    y,
                    width,
                    height
                }
            }
        };

        return JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });
    }
    public static string BuildAppendMFAOcr(int x, int y, int width, int height)
    {
        var payload = new
        {
            AppendOCR = new
            {
                recognition = "Custom",
                custom_recognition = "MFAOCRRecognition",
                roi = new[]
                {
                    x,
                    y,
                    width,
                    height
                }
            }
        };

        return JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });
    }
    public static string ReadTextFromMaaContext(IMaaContext context, IMaaImageBuffer image, int x, int y, int width, int height)
    {
        var detail = context.RunRecognition("AppendOCR", image, BuildAppendMFAOcr(x, y, width, height));
        if (detail == null)
        {
            return string.Empty;
        }

        var query = JsonConvert.DeserializeObject<RecognitionQuery>(detail.Detail);
        return query?.Best?.Text ?? string.Empty;
    }

    public static string ReadTextFromMaaTasker(MaaTasker tasker, Bitmap bitmap, int x, int y, int width, int height)
    {
        MFAOCRRecognition.Bitmap = bitmap;
        MFAOCRRecognition.Output = null;
        var pipeline = BuildAppendMFAOcr(x, y, width, height);

        var job = tasker.AppendTask("AppendOCR", pipeline).Wait();

        if (job == MaaJobStatus.Succeeded)
        {
            var result = MFAOCRRecognition.Output ?? string.Empty;
            MFAOCRRecognition.Output = null;
            MFAOCRRecognition.Bitmap = null;
            return result;
        }

        MFAOCRRecognition.Output = null;
        MFAOCRRecognition.Bitmap = null;
        return string.Empty;
    }

    public static void RunClickTest(MaaTasker tasker, int x, int y, int w, int h, int offset_x = 0, int offset_y = 0, int offset_w = 0, int offset_h = 0)
    {
        var payload = new
        {
            action = "Click",
            target = new[]
            {
                x,
                y,
                w,
                h
            },
            target_offset = new[]
            {
                offset_x,
                offset_y,
                offset_w,
                offset_h
            }
        };
        var pipeline = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });
        TaskManager.RunTask(() =>
        {
            Instances.ToolsViewModel.IsRunning = true;
            using var rect = new MaaRectBuffer();
            var job = tasker.AppendAction("Click", pipeline, rect, "{}");
            var status = job.Wait();
            Instances.ToolsViewModel.IsRunning = false;
            if (status != MaaJobStatus.Succeeded)
            {
                ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.TaskFailed.ToLocalization());
                return;
            }
        }, "Click Test");
    }

    public static void RunSwipeTest(MaaTasker tasker, int sx, int sy, int ex, int ey, int time = 200)
    {
        var payload = new
        {
            action = "Swipe",
            begin = new[]
            {
                sx,
                sy
            },
            end = new[]
            {
                ex,
                ey
            },
            duration = time
        };
        var pipeline = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });
        TaskManager.RunTask(() =>
        {
            Instances.ToolsViewModel.IsRunning = true;
            using var rect = new MaaRectBuffer();
            var job = tasker.AppendAction("Swipe", pipeline, rect, "{}");
            var status = job.Wait();
            Instances.ToolsViewModel.IsRunning = false;
            if (status != MaaJobStatus.Succeeded)
            {
                ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.TaskFailed.ToLocalization());
                return;
            }
        }, "Swipe Test");
    }

    public static void RunKeyClickTest(MaaTasker tasker, List<int> keys)
    {
        var payload = new
        {
            action = "ClickKey",
            key = keys
        };
        var pipeline = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });
        TaskManager.RunTask(() =>
        {
            Instances.ToolsViewModel.IsRunning = true;
            using var rect = new MaaRectBuffer();
            var job = tasker.AppendAction("ClickKey", pipeline, rect, "{}");
            var status = job.Wait();
            Instances.ToolsViewModel.IsRunning = false;
            if (status != MaaJobStatus.Succeeded)
            {
                ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.TaskFailed.ToLocalization());
                return;
            }
        }, "KeyClick Test");
    }

    public static void RunColorMatch(MaaTasker tasker, int x, int y, int w, int h, int color_method, List<int> up, List<int> low, int color_count, bool color_connected = false)
    {
        var tempBitmap = Instances.ToolsViewModel.LiveViewImage ?? Instances.ToolsViewModel.LiveViewDisplayImage;
        if (tempBitmap == null)
        {
            ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewNoScreenshot.ToLocalization());
            return;
        }

        var payload = new
        {
            recognition = "ColorMatch",
            method = color_method,
            upper = up,
            lower = low,
            count = color_count,
            connected = color_connected,
            roi = new[]
            {
                x,
                y,
                w,
                h
            }
        };
        var pipeline = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });


        TaskManager.RunTask(() =>
        {
            Instances.ToolsViewModel.IsRunning = true;
            using var buffer = new MaaImageBuffer();
            buffer.TrySetEncodedData(BitmapToBytes(tempBitmap));
            var job = tasker.AppendRecognition("ColorMatch", pipeline, buffer);
            var status = job.Wait();
            if (status != MaaJobStatus.Succeeded)
            {
                Instances.ToolsViewModel.IsRunning = false;
                return;
            }
            tasker.GetTaskDetail(job.Id, out var enter, out var nodeIdList, out var _);
            tasker.GetNodeDetail(nodeIdList[0], out var nodeName, out var recognitionId, out var actionId, out var actionCompleted);

            var imageListBuffer = new MaaImageListBuffer();

            using var hitBox = new MaaRectBuffer();
            tasker.GetRecognitionDetail(recognitionId, out string node,
                out var algorithm,
                out var hit,
                hitBox,
                out var detailJson,
                null, imageListBuffer);
            if (imageListBuffer.IsEmpty)
            {
                ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewNoHit.ToLocalization());
                Instances.ToolsViewModel.IsRunning = false;
                return;
            }
            Instances.ToolsViewModel.IsRunning = false;
            DispatcherHelper.PostOnMainThread(() =>
            {
                var imageBrowser = new SukiImageBrowser();
                imageBrowser.SetImage(imageListBuffer[0].ToBitmap());
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    imageBrowser.Show(desktop.MainWindow);
                }
                else
                {
                    imageBrowser.Show();
                }
                imageListBuffer.Dispose();
            });
        }, "TemplateMatch");
    }

    public static void RunOcrMatch(MaaTasker tasker, int x, int y, int w, int h, string text, double recognition_threshold = 0.3, bool rec = false)
    {
        var payload = new
        {
            recognition = "OCR",
            expected = text,
            threshold = recognition_threshold,
            only_rec = rec,
            roi = new[]
            {
                x,
                y,
                w,
                h
            }
        };
        var pipeline = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });
        var tempBitmap = Instances.ToolsViewModel.LiveViewImage ?? Instances.ToolsViewModel.LiveViewDisplayImage;
        if (tempBitmap == null)
        {
            ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewNoScreenshot.ToLocalization());
            return;
        }
        TaskManager.RunTask(() =>
        {
            Instances.ToolsViewModel.IsRunning = true;
            var job = tasker.AppendRecognition("OCR", pipeline, BitmapToMaaImageBuffer(tempBitmap));
            var status = job.Wait();
            if (status != MaaJobStatus.Succeeded)
            {
                ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.TaskFailed.ToLocalization());
                Instances.ToolsViewModel.IsRunning = false;
                return;
            }

            tasker.GetTaskDetail(job.Id, out var enter, out var nodeIdList, out var _);
            tasker.GetNodeDetail(nodeIdList[0], out var nodeName, out var recognitionId, out var actionId, out var actionCompleted);
            var imageListBuffer = new MaaImageListBuffer();

            using var hitBox = new MaaRectBuffer();
            tasker.GetRecognitionDetail(recognitionId, out string node,
                out var algorithm,
                out var hit,
                hitBox,
                out var detailJson,
                null, imageListBuffer);

            if (imageListBuffer.IsEmpty)
            {
                ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewNoHit.ToLocalization());
                Instances.ToolsViewModel.IsRunning = false;
                return;
            }
            Instances.ToolsViewModel.IsRunning = false;
            DispatcherHelper.PostOnMainThread(() =>
            {
                var imageBrowser = new SukiImageBrowser();
                imageBrowser.SetImage(imageListBuffer[0].ToBitmap());
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    imageBrowser.Show(desktop.MainWindow);
                }
                else
                {
                    imageBrowser.Show();
                }
                imageListBuffer.Dispose();
            });
        }, "OcrMatch");
    }

    public static void RunTemplateMatch(MaaTasker tasker, int x, int y, int w, int h, Bitmap? bitmap, bool mask = false, double recognition_threshold = 0.7, int method_nodes = 5)
    {
        if (bitmap == null)
        {
            ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewNoScreenshot.ToLocalization());
            return;
        }

        var tempBitmap = Instances.ToolsViewModel.LiveViewImage ?? Instances.ToolsViewModel.LiveViewDisplayImage;
        if (tempBitmap == null)
        {
            ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewNoScreenshot.ToLocalization());
            return;
        }

        var payload = new
        {
            recognition = "TemplateMatch",
            template = "template.png",
            threshold = recognition_threshold,
            green_mask = mask,
            method = method_nodes,
            roi = new[]
            {
                x,
                y,
                w,
                h
            }
        };
        var pipeline = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });


        TaskManager.RunTask(() =>
        {
            Instances.ToolsViewModel.IsRunning = true;
            using var buffer = new MaaImageBuffer();
            buffer.TrySetEncodedData(BitmapToBytes(tempBitmap));
            using var template = new MaaImageBuffer();
            template.TrySetEncodedData(BitmapToBytes(bitmap));
            tasker.Resource.OverrideImage("template.png", template);
            var job = tasker.AppendRecognition("TemplateMatch", pipeline, buffer);
            var status = job.Wait();
            if (status != MaaJobStatus.Succeeded)
            {
                Instances.ToolsViewModel.IsRunning = false;
                return;
            }
            tasker.GetTaskDetail(job.Id, out var enter, out var nodeIdList, out var _);
            tasker.GetNodeDetail(nodeIdList[0], out var nodeName, out var recognitionId, out var actionId, out var actionCompleted);

            var imageListBuffer = new MaaImageListBuffer();

            using var hitBox = new MaaRectBuffer();
            tasker.GetRecognitionDetail(recognitionId, out string node,
                out var algorithm,
                out var hit,
                hitBox,
                out var detailJson,
                null, imageListBuffer);
            if (imageListBuffer.IsEmpty)
            {
                ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewNoHit.ToLocalization());
                Instances.ToolsViewModel.IsRunning = false;
                return;
            }
            Instances.ToolsViewModel.IsRunning = false;
            DispatcherHelper.PostOnMainThread(() =>
            {
                var imageBrowser = new SukiImageBrowser();
                imageBrowser.SetImage(imageListBuffer[0].ToBitmap());
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    imageBrowser.Show(desktop.MainWindow);
                }
                else
                {
                    imageBrowser.Show();
                }
                imageListBuffer.Dispose();
            });
        }, "TemplateMatch");
    }

    public static byte[] BitmapToBytes(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        return ms.ToArray();
    }

    public static IMaaImageBuffer BitmapToMaaImageBuffer(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        var bytes = ms.ToArray();
        var buffer = new MaaImageBuffer();
        buffer.TrySetEncodedData(bytes);
        return buffer;
    }
}
