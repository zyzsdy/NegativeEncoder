using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NegativeEncoder.Utils;

public static class AppleRuntimeInstaller
{
    private sealed class Package
    {
        public string InstallerUrl { get; set; }
        public string InstallerSha256 { get; set; }
        public string MsiName { get; set; }
        public string MsiSha256 { get; set; }
    }

    // prepareOnly verifies the real download/extraction without changing the system.
    public static async Task<int> RunAsync(string baseDir, bool prepareOnly = false)
    {
        var qaacExecutable = ToolPaths.Resolve(baseDir, "qaac", "qaac64.exe");
        if (!prepareOnly && (await QaacProbe.CheckAsync(qaacExecutable)).IsAvailable)
        {
            Console.WriteLine("qaac 已可用，跳过 Apple 运行库安装。");
            return 0;
        }

        var configuration = Path.Combine(baseDir, "Setup", "apple-runtime.lock.json");
        var package = JsonSerializer.Deserialize<Package>(await File.ReadAllTextAsync(configuration),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        ValidatePackage(package);
        var sevenZip = Path.Combine(baseDir, "Libs", "7z.exe");
        if (!File.Exists(sevenZip)) throw new FileNotFoundException("未找到 7z.exe", sevenZip);

        var cache = Path.Combine(baseDir, "Setup", "AppleRuntime-cache");
        Directory.CreateDirectory(cache);
        var installer = Path.Combine(cache, "iTunes64Setup.exe");
        if (!File.Exists(installer) || !HashMatches(installer, package.InstallerSha256))
        {
            Console.WriteLine("正在从 Apple 官方服务器下载历史安装包（约 264 MB）...");
            var partial = installer + ".partial";
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                using var response = await client.GetAsync(package.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                response.EnsureSuccessStatusCode();
                await using (var file = File.Create(partial))
                    await response.Content.CopyToAsync(file, timeout.Token);
                if (!HashMatches(partial, package.InstallerSha256))
                    throw new InvalidDataException("Apple 下载文件 SHA256 校验失败。");
                File.Move(partial, installer, true);
            }
            finally
            {
                if (File.Exists(partial)) File.Delete(partial);
            }
        }
        VerifySignature(installer);

        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var work = Path.Combine(temporaryRoot, "NegativeEncoder-Apple-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            Console.WriteLine("正在提取并校验 Apple Application Support x64 MSI...");
            using (var extractor = new Process
                   {
                       StartInfo = new ProcessStartInfo(sevenZip)
                       {
                           UseShellExecute = false,
                           CreateNoWindow = true,
                           ArgumentList = { "e", "-y", "-bso0", installer, package.MsiName, "-o" + work }
                       }
                   })
            {
                extractor.Start();
                await extractor.WaitForExitAsync();
                if (extractor.ExitCode != 0) throw new InvalidDataException("Apple MSI 解包失败。");
            }
            var msi = Path.Combine(work, package.MsiName);
            if (!File.Exists(msi) || !HashMatches(msi, package.MsiSha256))
                throw new InvalidDataException("Apple MSI SHA256 校验失败。");
            VerifySignature(msi);
            if (prepareOnly)
            {
                Console.WriteLine("Apple 安装包及 MSI 校验通过；未执行安装。");
                return 0;
            }

            // Keep Apple's interactive license/install UI; elevation belongs to this MSI only.
            Console.WriteLine("即将打开 Apple 安装向导，可能需要管理员权限...");
            using var installation = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe"),
                Arguments = $"/i \"{msi}\" /norestart",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            }) ?? throw new InvalidOperationException("无法启动 Apple 安装向导。");
            await installation.WaitForExitAsync();
            if (installation.ExitCode != 0 && installation.ExitCode != 3010)
                throw new InvalidOperationException($"Apple 安装失败或取消（退出码 {installation.ExitCode}）。");
            if (!(await QaacProbe.CheckAsync(qaacExecutable)).IsAvailable)
            {
                if (installation.ExitCode == 3010)
                {
                    Console.WriteLine("Apple 运行库安装完成，需要重启后再使用 qaac。");
                    return 3010;
                }
                throw new InvalidOperationException("Apple 安装结束，但 qaac 自检仍失败，请检查运行库或重启电脑。");
            }
            Console.WriteLine("Apple 运行库安装完成，qaac 自检通过。");
            return installation.ExitCode;
        }
        finally
        {
            var prefix = temporaryRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(work).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                try { Directory.Delete(work, true); } catch (IOException) { }
            }
        }
    }

    private static void ValidatePackage(Package package)
    {
        if (package == null || !Uri.TryCreate(package.InstallerUrl, UriKind.Absolute, out var url) ||
            url.Scheme != "https" || url.Host != "secure-appldnld.apple.com" ||
            package.MsiName != "AppleApplicationSupport64.msi" ||
            !Regex.IsMatch(package.InstallerSha256 ?? "", "^[a-f0-9]{64}$") ||
            !Regex.IsMatch(package.MsiSha256 ?? "", "^[a-f0-9]{64}$"))
            throw new InvalidDataException("Apple 运行库下载配置无效。");
    }

    internal static bool HashMatches(string path, string expected)
    {
        using var file = File.OpenRead(path);
        return string.Equals(Convert.ToHexString(SHA256.HashData(file)), expected, StringComparison.OrdinalIgnoreCase);
    }

    internal static void VerifySignature(string path)
    {
        var file = new TrustFile
        {
            Size = (uint)Marshal.SizeOf<TrustFile>(), Path = Path.GetFullPath(path)
        };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        Marshal.StructureToPtr(file, pointer, false);
        try
        {
            var data = new TrustData
            {
                Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1, File = pointer
            };
            var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
            var result = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            if (result != 0)
                throw new InvalidDataException($"Apple 文件数字签名验证失败（0x{result:X8}）。");
        }
        finally
        {
            Marshal.DestroyStructure<TrustFile>(pointer);
            Marshal.FreeHGlobal(pointer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Path;
        public IntPtr Handle;
        public IntPtr Subject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size;
        public IntPtr PolicyCallback;
        public IntPtr SipClient;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
}
