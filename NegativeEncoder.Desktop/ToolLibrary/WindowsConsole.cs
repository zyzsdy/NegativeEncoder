using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace NegativeEncoder.Desktop.ToolLibrary;

[SupportedOSPlatform("windows")]
internal static class WindowsConsole
{
    // ProcessStartInfo cannot request CREATE_NEW_CONSOLE. The shell needs both its own
    // console and a private Unicode environment, including PATH values longer than cmd's limit.
    public static void Start(ProcessStartInfo info)
    {
        const uint createNewConsole = 0x10, createUnicodeEnvironment = 0x400;
        var environment = string.Join('\0', info.Environment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key}={pair.Value}")) + "\0\0";
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        var commandLine = new StringBuilder($"\"{info.FileName}\"");
        if (!CreateProcessW(info.FileName, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                createNewConsole | createUnicodeEnvironment, environment, null, ref startup, out var process))
            throw new IOException($"无法创建 Shell 控制台：{info.FileName}", new Win32Exception(Marshal.GetLastWin32Error()));

        CloseHandle(process.Thread);
        CloseHandle(process.Process);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public ushort ShowWindow, Reserved2Size;
        public IntPtr Reserved2, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public uint ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool CreateProcessW(string application, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint flags, string environment, string? directory,
        ref StartupInfo startup, out ProcessInformation process);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
