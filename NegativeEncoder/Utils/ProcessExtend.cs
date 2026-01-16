using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;

namespace NegativeEncoder.Utils;

public static class ProcessExtend
{
    public static void WaitForProcessTreeExit(this Process parent, int pollIntervalMs = 500)
    {
        if (parent == null) return;

        try
        {
            parent.WaitForExit();
        }
        catch
        {
            return;
        }

        while (true)
        {
            var descendants = GetDescendantProcesses(parent.Id);
            if (descendants.Count == 0) break;

            foreach (var child in descendants)
            {
                try
                {
                    child.WaitForExit(pollIntervalMs);
                }
                catch
                {
                    // ignore child process exit failures
                }
            }

            Thread.Sleep(pollIntervalMs);
        }
    }

    public static void KillProcessTree(this Process parent)
    {
        var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + parent.Id);
        var moc = searcher.Get();
        foreach (ManagementObject mo in moc)
        {
            var childProcess = Process.GetProcessById(Convert.ToInt32(mo["ProcessID"]));
            childProcess.KillProcessTree();
        }

        try
        {
            if (parent.Id != Process.GetCurrentProcess().Id) parent.Kill(); //结束当前进程
        }
        catch
        {
            /* process already exited */
        }
    }

    private static List<Process> GetDescendantProcesses(int parentId)
    {
        var results = new List<Process>();
        var pendingParents = new Queue<int>();
        pendingParents.Enqueue(parentId);

        while (pendingParents.Count > 0)
        {
            var currentParentId = pendingParents.Dequeue();
            var searcher = new ManagementObjectSearcher(
                "Select ProcessID From Win32_Process Where ParentProcessID=" + currentParentId);
            var moc = searcher.Get();

            foreach (ManagementObject mo in moc)
            {
                if (!int.TryParse(mo["ProcessID"].ToString(), out var childId)) continue;

                try
                {
                    var childProcess = Process.GetProcessById(childId);
                    results.Add(childProcess);
                    pendingParents.Enqueue(childId);
                }
                catch
                {
                    // child may have already exited
                }
            }
        }

        return results;
    }
}