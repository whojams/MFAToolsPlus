using Avalonia.Media.Imaging;
using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MFAToolsPlus.Extensions.MaaFW.Custom;
using Newtonsoft.Json;
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

    public static void RunOcr()
    {
        
    }
    public static byte[] BitmapToBytes(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        return ms.ToArray();
    }
}
