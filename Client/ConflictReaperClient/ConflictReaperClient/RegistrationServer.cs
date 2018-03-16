using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace ConflictReaperClient
{
    class RegistrationServer
    {
        private static string host = "localhost:8080/ConflictReaper";
        //private static string host = "58.205.208.74:2013/ConflictReaper";
        private static Thread registerThread;
        private static WebSocket registerClient;
        private static volatile bool stopRegisterThread = false;

        public static void register(string email, int port)
        {
            string url = "ws://" + host + "/websocket";
            registerClient = new WebSocket(url);
            registerClient.SetCookie(new WebSocketSharp.Net.Cookie("user", email));
            registerClient.Connect();
            registerClient.Send("IP=" + getLocalIp() + ",Port=" + port);
            /*registerClient.OnClose += (s, e) =>
            {
                stopRegisterThread = true;
                registerThread.Join();
            };
            registerClient.OnError += (s, e) =>
            {
                stopRegisterThread = true;
                registerThread.Join();
            };
            registerThread = new Thread(new ThreadStart(() =>
            {
                registerClient.Connect();
                registerClient.Send("IP=" + getLocalIp() + ",Port=" + port);
                while (!stopRegisterThread)
                {

                }
            }));
            registerThread.IsBackground = true;
            registerThread.Start();*/
        }

        public static string getLocalIp()
        {
            System.Net.Sockets.TcpClient c = new System.Net.Sockets.TcpClient();
            c.Connect("58.205.208.74", 2013);
            string ip = ((System.Net.IPEndPoint)c.Client.LocalEndPoint).Address.ToString();
            c.Close();
            return ip;
        }

        public static Dictionary<string, string> getAddressMap(List<SharedUser> users)
        {
            string _user = "";
            foreach (SharedUser user in users)
            {
                _user += user.Email + ",";
            }
            _user = _user.Substring(0, _user.Length - 1);

            string url = "http://" + host + "/getaddressmap";
            WebRequest request = WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(_user);
            }

            try
            {
                Dictionary<string, string> IAddressMap = new Dictionary<string, string>();
                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    var streamReader = new StreamReader(response.GetResponseStream());
                    string result = streamReader.ReadToEnd();
                    if (!result.Equals(""))
                    {
                        string[] maps = result.Split(',');
                        foreach (string map in maps)
                        {
                            string[] param = map.Split('|');
                            IAddressMap.Add(param[0], param[1]);
                        }
                    }
                    return IAddressMap;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void close()
        {
            if (registerClient != null)
                registerClient.Close();
        }
    }
}
