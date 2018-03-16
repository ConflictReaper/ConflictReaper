using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConflictReaperClient
{
    public enum WatchDogMessageType
    {
        Lock,
        UnLock
    }

    public class WatchDogMessage
    {
        public WatchDogMessageType Type { get; set; }
        public string User { get; set; }
        private string path;
        public string Path
        {
            get
            {
                return path;
            }
            set
            {
                if (value.StartsWith("/"))
                {
                    path = value;
                }
                else if (value.StartsWith(LocalRoot))
                {
                    path = value.Substring(LocalRoot.Length).Replace('\\', '/');
                }
                else
                {
                    throw new WatchDogMessageException("File path should start with '/' or should be in the dropbox folder.");
                }
            }
        }
        public int Time { get; set; }
        public int Flow { get; set; }
        public string LocalRoot { get; set; }

        public WatchDogMessage(string basepath)
        {
            LocalRoot = basepath;
        }

        public WatchDogMessage(WatchDogMessageType type, string user, string path, string basepath)
        {
            LocalRoot = basepath;
            Type = type;
            User = user;
            Path = path;
        }

        public WatchDogMessage(WatchDogMessageType type, string user, string path, int time, int flow, string basepath)
        {
            LocalRoot = basepath;
            Type = type;
            User = user;
            Path = path;
            Time = time;
            Flow = flow;
        }

        public static WatchDogMessage FromString(string value, string basepath)
        {
            WatchDogMessage message = new WatchDogMessage(basepath);
            string[] param = value.Split(',');
            if (param.Length != 3 && param.Length != 5)
            {
                throw new WatchDogMessageException("Error Message Params Count.");
            }
            message.User = param[1];
            message.Path = param[2];
            if (param[0].Equals("LOCK"))
            {
                message.Type = WatchDogMessageType.Lock;
            }
            else if (param[0].Equals("UNLOCK"))
            {
                message.Type = WatchDogMessageType.UnLock;
                try
                {
                    message.Time = int.Parse(param[3]);
                }
                catch (Exception)
                {
                    throw new WatchDogMessageException("Prase Time Failed.");
                }
                try
                {
                    message.Flow = int.Parse(param[4]);
                }
                catch (Exception)
                {
                    throw new WatchDogMessageException("Prase Flow Failed.");
                }
            }
            else
            {
                throw new WatchDogMessageException("Unknown Message Type.");
            }
            return message;
        }

        public override string ToString()
        {
            string result = "";
            if (Type == WatchDogMessageType.Lock)
            {
                result += "LOCK,";
                result += User + ",";
                result += Path;
            }
            else
            {
                result += "UNLOCK,";
                result += User + ",";
                result += Path + ",";
                result += Time + ",";
                result += Flow;
            }
            return result;
        }

        public string getLocalPath()
        {
            return LocalRoot + path.Replace('/', '\\');
        }
    }

    public class WatchDogMessageException : Exception
    {
        private string message;
        
        public WatchDogMessageException(string Message) : base()
        {
            message = Message;
        }

        public override string ToString()
        {
            return message;
        }
    }
}
