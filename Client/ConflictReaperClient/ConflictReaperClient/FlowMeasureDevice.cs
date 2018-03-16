using PacketDotNet;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConflictReaperClient
{
    class FlowMeasureDevice
    {
        private ICaptureDevice SendDevice;
        private ICaptureDevice RecvDevice;
        private List<int> ports = new List<int>();

        public string localIP;

        public int NetSendBytes { get; private set; }
        public int NetRecvBytes { get; private set; }

        public bool isUsed { get; private set; }

        public FlowMeasureDevice(string ip)
        {
            localIP = ip;
            int currentDevice;

            var Devices = CaptureDeviceList.New();
            for (currentDevice = 0; currentDevice < Devices.Count; currentDevice++)
            {
                if (Devices[currentDevice].ToString().IndexOf(ip) != -1)
                    break;
            }

            SendDevice = (ICaptureDevice)CaptureDeviceList.New()[currentDevice];
            SendDevice.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrivalSend);
            SendDevice.Open(DeviceMode.Promiscuous, 1000);
            SendDevice.Filter = "src host " + ip;

            RecvDevice = (ICaptureDevice)CaptureDeviceList.New()[currentDevice];
            RecvDevice.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrivalRecv);
            RecvDevice.Open(DeviceMode.Promiscuous, 1000);
            RecvDevice.Filter = "dst host " + ip;

            NetSendBytes = 0;
            NetRecvBytes = 0;
            isUsed = false;
        }

        public void getPorts(int[] pids)
        {
            Process pro = new Process();
            pro.StartInfo.FileName = "cmd.exe";
            pro.StartInfo.UseShellExecute = false;
            pro.StartInfo.RedirectStandardInput = true;
            pro.StartInfo.RedirectStandardOutput = true;
            pro.StartInfo.RedirectStandardError = true;
            pro.StartInfo.CreateNoWindow = true;
            pro.Start();
            pro.StandardInput.WriteLine("netstat -ano");
            pro.StandardInput.WriteLine("exit");
            Regex reg = new Regex("\\s+", RegexOptions.Compiled);
            string line = null;
            ports.Clear();
            while ((line = pro.StandardOutput.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.StartsWith("TCP", StringComparison.OrdinalIgnoreCase))
                {
                    line = reg.Replace(line, ",");
                    string[] arr = line.Split(',');
                    if (pids.Contains(int.Parse(arr[4])))
                    {
                        string[] soc = arr[1].Split(':');
                        if (soc[0].Equals(localIP))
                            ports.Add(int.Parse(soc[1]));
                    }
                }
            }
            pro.Close();
            ports = ports.Distinct().ToList();
        }

        public void Start()
        {
            isUsed = true;
            SendDevice.StartCapture();
            RecvDevice.StartCapture();
        }

        public void Stop()
        {
            SendDevice.StopCapture();
            RecvDevice.StopCapture();

            NetSendBytes = 0;
            NetRecvBytes = 0;
            isUsed = false;
        }

        public void Refresh()
        {
            NetSendBytes = 0;
            NetRecvBytes = 0;
        }

        public void Close()
        {
            SendDevice.Close();
            RecvDevice.Close();
        }

        private void device_OnPacketArrivalSend(object sender, CaptureEventArgs e)
        {
            try
            {
                var packet = Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
                var src = packet.ToString();
                var port = int.Parse(src.Substring(src.IndexOf("SourcePort=") + 11, src.IndexOf(',', src.IndexOf("SourcePort=")) - src.IndexOf("SourcePort=") - 11));
                if (ports.Contains(port))
                {
                    var len = e.Packet.Data.Length;
                    NetSendBytes += len;
                }
            }
            catch
            {

            }
        }

        private void device_OnPacketArrivalRecv(object sender, CaptureEventArgs e)
        {
            try
            {
                var packet = Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
                var src = packet.ToString();
                if (ports.Contains(int.Parse(src.Substring(src.IndexOf("DestinationPort=") + 16, src.IndexOfAny(new char[] { ',', ']' }, src.IndexOf("DestinationPort=")) - src.IndexOf("DestinationPort=") - 16))))
                {
                    var len = e.Packet.Data.Length;
                    NetRecvBytes += len;
                }
            }
            catch
            {

            }
        }
    }
}
