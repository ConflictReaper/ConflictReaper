using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConflictReaperClient
{
    class FlowMeasureDevicePool : IDisposable
    {
        private string localIP;
        private List<FlowMeasureDevice> devices = new List<FlowMeasureDevice>();

        public FlowMeasureDevicePool()
        {
            System.Net.Sockets.TcpClient c = new System.Net.Sockets.TcpClient();
            c.Connect("58.205.208.74", 2013);
            localIP = ((System.Net.IPEndPoint)c.Client.LocalEndPoint).Address.ToString();
            c.Close();

            for (int i = 0; i < 20; i++)
            {
                devices.Add(new FlowMeasureDevice(localIP));
            }
        }

        public FlowMeasureDevice getDevice()
        {
            FlowMeasureDevice dev = null;
            for (int i = 0; i < devices.Count; i++)
            {
                if (!devices[i].isUsed)
                    dev = devices[i];
            }
            if (dev == null)
            {
                dev = new FlowMeasureDevice(localIP);
                devices.Add(dev);
            }
            return dev;
        }

        public void Dispose()
        {
            for (int i = 0; i < devices.Count; i++)
            {
                devices[i].Stop();
                devices[i].Close();
            }
            devices.Clear();
        }
    }
}
