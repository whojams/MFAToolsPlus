using System.Collections.Generic;
using System.Runtime.InteropServices;

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
}
