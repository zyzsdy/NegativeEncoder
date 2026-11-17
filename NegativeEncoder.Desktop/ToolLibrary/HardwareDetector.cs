using System.Text.Json;

namespace NegativeEncoder.Desktop.ToolLibrary;

public sealed class HardwareDetector(ProcessRunner runner)
{
    public async Task<HardwareReport> DetectAsync(CancellationToken token)
    {
        try
        {
            string description;
            if (OperatingSystem.IsMacOS())
            {
                var result = await runner.RunAsync("/usr/sbin/system_profiler", ["SPDisplaysDataType", "-json"], token, timeoutSeconds: 25);
                result.EnsureSuccess("GPU 检测");
                using var json = JsonDocument.Parse(result.Output);
                description = string.Join("、", json.RootElement.GetProperty("SPDisplaysDataType").EnumerateArray()
                    .Select(d => d.TryGetProperty("sppci_model", out var model) ? model.GetString() : d.GetProperty("_name").GetString()));
            }
            else if (OperatingSystem.IsWindows())
            {
                var ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                var result = await runner.RunAsync(ps, ["-NoProfile", "-NonInteractive", "-Command", "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; Get-CimInstance Win32_VideoController | ForEach-Object {$_.Name}"], token, timeoutSeconds: 25);
                result.EnsureSuccess("GPU 检测");
                description = result.Output.Trim();
            }
            else
            {
                var devices = new List<string>();
                foreach (var directory in Directory.EnumerateDirectories("/sys/bus/pci/devices"))
                {
                    var cls = await File.ReadAllTextAsync(Path.Combine(directory, "class"), token);
                    if (!cls.Trim().StartsWith("0x03", StringComparison.Ordinal)) continue;
                    var vendor = (await File.ReadAllTextAsync(Path.Combine(directory, "vendor"), token)).Trim();
                    devices.Add(vendor switch { "0x10de" => "NVIDIA", "0x8086" => "Intel", "0x1002" => "AMD", _ => $"未知 GPU 厂商 {vendor}" });
                }
                description = string.Join("、", devices);
            }
            var vendors = new List<string>();
            if (description.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) vendors.Add("NVIDIA");
            if (description.Contains("Intel", StringComparison.OrdinalIgnoreCase)) vendors.Add("Intel");
            if (description.Contains("AMD", StringComparison.OrdinalIgnoreCase) || description.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) vendors.Add("AMD");
            if (OperatingSystem.IsMacOS() && System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64) vendors.Add("Apple");
            return new(string.IsNullOrWhiteSpace(description) ? "未检测到显卡" : description, vendors.ToArray(), null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return new("硬件检测失败", [], ex.ToString()); }
    }
}
