using System.Diagnostics;

namespace NegativeEncoder.Utils;

public static class ProcessExtend
{
    public static void WaitForProcessTreeExit(this Process parent, int timeoutMs = 30000)
    {
        if (parent == null) return;

        try
        {
            if (timeoutMs <= 0)
                parent.WaitForExit();
            else
                parent.WaitForExit(timeoutMs);
        }
        catch
        {
            // process already exited or access denied
        }
    }

    public static void KillProcessTree(this Process parent)
    {
        if (parent == null) return;

        try
        {
            if (parent.HasExited) return;
        }
        catch
        {
            return;
        }

        try
        {
            if (parent.Id == Process.GetCurrentProcess().Id) return;
            parent.Kill(true);
        }
        catch
        {
            // process already exited or no permission
        }
    }
}
