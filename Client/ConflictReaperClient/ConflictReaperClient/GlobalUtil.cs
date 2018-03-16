using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConflictReaperClient
{
    public class FileStatus
    {
        public static int Open = 0x00000001;
        public static int Edit = 0x00000002;
        public static int EditByOther = 0x00000004;
        public static int Uncheck = 0x00000008;
    }

    public class Files
    {
        private class File
        {
            public string path;
            public int    status;
            public string editor;
            public List<int> processes;

            public File(string path)
            {
                this.path = path;
                status = 0x00000000;
                editor = null;
                processes = new List<int>();
            }
        }

        private Dictionary<string, File> files = new Dictionary<string, File>();

        public void Add(string path)
        {
            lock (files)
            {
                if (!files.Keys.Contains(path))
                {
                    files.Add(path, new File(path));
                }
            }
        }

        public void Remove(string path)
        {
            lock (files)
            {
                if (files.Keys.Contains(path))
                {
                    files.Remove(path);
                }
            }
        }

        public bool isOpening(string path)
        {
            lock (files)
            {
                if (!files.Keys.Contains(path))
                    return false;
                return (files[path].status & FileStatus.Open) > 0;
            }
        }

        public bool isEditing(string path)
        {
            lock (files)
            {
                if (!files.Keys.Contains(path))
                    return false;
                return (files[path].status & FileStatus.Edit) > 0;
            }
        }

        public bool isLocking(string path)
        {
            lock (files)
            {
                if (!files.Keys.Contains(path))
                    return false;
                return (files[path].status & FileStatus.EditByOther) > 0;
            }
        }

        public bool isUncheck(string path)
        {
            lock (files)
            {
                if (!files.Keys.Contains(path))
                    return false;
                return (files[path].status & FileStatus.Uncheck) > 0;
            }
        }

        public void setStatus(string path, int status, bool flag)
        {
            lock (files)
            {
                if (!files.Keys.Contains(path))
                    return;
                if (flag)
                    files[path].status |= status;
                else
                    files[path].status ^= status;
            }
        }

        public void setEditor(string path, string editor)
        {
            lock (files)
            {
                if (!files.Keys.Contains(path))
                    return;
                files[path].editor = editor;
            }
        }

        public string getEditor(string path)
        {
            lock (files)
            {
                if (!files.Keys.Contains(path))
                    return null;
                return files[path].editor;
            }
        }

        public List<string> EditingFiles()
        {
            List<string> filelist = new List<string>();
            foreach (File file in files.Values)
            {
                if ((file.status & FileStatus.Edit) > 0)
                    filelist.Add(file.path);
            }
            return filelist;
        }

        public void SocketClose(string user)
        {
            lock (files)
            {
                foreach (File file in files.Values)
                {
                    if ((file.status & FileStatus.EditByOther) > 0 && file.editor.Equals(user))
                    {
                        file.status ^= FileStatus.EditByOther;
                        file.editor = null;
                    }
                }
            }
        }

        public void ProcessOpen(int id, string path)
        {
            lock (files)
            {
                if (!files.Keys.Contains(path))
                    return;
                files[path].processes.Add(id);
            }
        }

        public void ProcessExit(int id)
        {
            lock (files)
            {
                foreach (File file in files.Values)
                {
                    if (file.processes.Contains(id))
                    {
                        file.processes.Remove(id);
                        if (file.processes.Count == 0)
                        {
                            file.status ^= FileStatus.Open;
                            if ((file.status & FileStatus.Edit) > 0)
                            {
                                file.status ^= FileStatus.Edit;
                                OnProcessExit(null, new FileEventArg(file.path));
                            }
                        }
                    }
                }
            }
        }

        public void FileEdit(string title)
        {
            lock (files)
            {
                foreach (string file in files.Keys)
                {
                    string name = file.Substring(file.LastIndexOf("\\") + 1);
                    if (title.IndexOf(name) != -1 && !((files[file].status & FileStatus.Edit) > 0))
                    {
                        if ((files[file].status & FileStatus.EditByOther) > 0)
                        {
                            System.Windows.MessageBox.Show("Warning: The file \"" + file + "\" is being edited by " + files[file].editor + "! Do not edit it.");
                        }
                        else
                        {
                            files[file].status |= FileStatus.Edit;
                            OnFileEdit(null, new FileEventArg(file));
                        }
                    }
                }
            }
        }

        public class FileEventArg
        {
            public string filename { get; set; }

            public FileEventArg(string filename)
            {
                this.filename = filename;
            }
        }

        public event EventHandler<FileEventArg> OnProcessExit;
        public event EventHandler<FileEventArg> OnFileEdit;
    }

    public class GlobalUtil
    {
        public string User;
        public string Email;
        public string Pwd;
        public string DropboxPath;
        public string DropboxBasePath;
        public string PathPerfix;

        public static string DefaultProxyHost = "166.111.80.96";
        public static int DefaultProxyPort = 20300;
        public string ProxyHost = DefaultProxyHost;
        public int ProxyPort = DefaultProxyPort;
        public int ProxyState = 1;

        public Dictionary<string, string> WebSocketSessionsMap = new Dictionary<string, string>();
        public List<SharedUser> CoWorkers = new List<SharedUser>();
        public Dictionary<string, List<SharedUser>> Folder_coWorkers = new Dictionary<string, List<SharedUser>>();

        public Files files = new Files();
        public List<int> ProcessesThatOpenFile = new List<int>();

        public bool setBasePath()
        {
            if (DropboxPath == null || DropboxPath.Length == 0 || !isDropboxFolder(DropboxPath))
                return false;

            string[] tempPath = DropboxPath.Split('\\');
            string tempPath_1 = tempPath[0];
            for (int i = 1; i < tempPath.Length; i++)
            {
                tempPath_1 += "\\" + tempPath[i];
                if (isDropboxFolder(tempPath_1))
                {
                    DropboxBasePath = tempPath_1;
                    return true;
                }
            }
            return false;
        }

        private bool isDropboxFolder(string path)
        {
            string[] fileList = Directory.GetFiles(path);
            bool isDropboxFolder = false;
            foreach (string file in fileList)
            {
                if (file.EndsWith(".dropbox"))
                {
                    isDropboxFolder = true;
                    break;
                }
            }
            return isDropboxFolder;
        }

        public void setPathPerfix()
        {
            if (DropboxBasePath.Equals(DropboxPath))
                PathPerfix = "/";
            else
            {
                PathPerfix = DropboxPath.Substring(DropboxBasePath.Length).Replace('\\', '/').ToLower();
            }
        }

        public bool checkIP(string ip)
        {
            try
            {
                int a = int.Parse(ip);
                if (a < 0 || a > 255)
                    return false;
            }
            catch
            {
                return false;
            }
            return true;
        }
        
        public int getUsablePort()
        {
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipsTCP = ipGlobalProperties.GetActiveTcpListeners();
            IPEndPoint[] ipsUDP = ipGlobalProperties.GetActiveUdpListeners();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

            HashSet<int> portSet = new HashSet<int>();
            foreach (IPEndPoint ip in ipsTCP)
            {
                portSet.Add(ip.Port);
            }
            foreach (IPEndPoint ip in ipsUDP)
            {
                portSet.Add(ip.Port);
            }
            foreach (TcpConnectionInformation info in tcpConnInfoArray)
            {
                portSet.Add(info.LocalEndPoint.Port);
            }

            int port = 9999;
            while (portSet.Contains(port))
            {
                port++;
            }
            return port;
        }

        public event EventHandler<EventArgs> OnRefresh;

        public void updateCoWorkers(string email, bool status)
        {
            foreach (SharedUser user in CoWorkers)
            {
                if (user.Email.Equals(email))
                {
                    user.setStatus(status);
                    OnRefresh(this, new EventArgs());
                    break;
                }
            }
        }
    }
}
