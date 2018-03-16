using EasyHook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConflictReaperClient
{
    public class HookInjector
    {
        private Int32 TargetPID { get; set; }
        private string ChannelName;

        public HookInjector(Int32 PID, string ChannelName)
        {
            TargetPID = PID;
            this.ChannelName = ChannelName;
        }

        public void Inject()
        {
            try
            {
                RemoteHooking.Inject(
                        TargetPID,
                        "FileOpenMonitor.dll",
                        "FileOpenMonitor.dll",
                        ChannelName);
            }
            catch (Exception ExtInfo)
            {
                System.Diagnostics.Debug.WriteLine("There was an error while connecting to target {1}:\r\n{0}", ExtInfo.ToString(), TargetPID);
            }
        }
    }
}
