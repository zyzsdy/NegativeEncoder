using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Text;

namespace NegativeEncoder.Utils
{
    public static class ProcessExtend
    {
        public static void KillProcessTree(this Process parent)
        {
            var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + parent.Id);
            var moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                Process childProcess = Process.GetProcessById(Convert.ToInt32(mo["ProcessID"]));
                childProcess.KillProcessTree();
            }
            try
            {
                if (parent.Id != Process.GetCurrentProcess().Id) parent.Kill();//结束当前进程
            }
            catch
            {
                /* process already exited */
            }
        }
    }
}
