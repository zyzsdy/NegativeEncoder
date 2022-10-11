using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NegativeEncoder.Utils;

public static class OpenBrowserViewLink
{
    public static void OpenUrl(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
        else
            throw new NotImplementedException($"未知的操作系统，请自行打开URL: {url}");
    }
}