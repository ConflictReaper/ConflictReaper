using CefSharp;
using CefSharp.Wpf;
using Dropbox.Api.Sharing;
using EasyHook;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace ConflictReaperClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private GlobalUtil global = new GlobalUtil();
        private DropboxConnection dropboxclient;
        private ChromiumWebBrowser webView;
        private MessageWindow mswin = new MessageWindow();

        private WebSocketServer webServer = null;
        private WebSocketServiceHost webServerHost;
        private Dictionary<string, WebSocket> WebSocketPool = new Dictionary<string, WebSocket>();

        private FlowMeasureDevicePool flowMeasureDevicePool = null;
        private int[] DropBoxProcessIds;

        private IList<SharedFolderMetadata> SharedFolders;

        private KeyboardHook keyboardHook = null;
        private bool isShiftDown = false;
        private bool isCtrlDown = false;
        private bool isAltDown = false;

        private FileSystemWatcher FileSaveWatcher;
        private System.Windows.Forms.NotifyIcon nIcon;

        //private NetCpu netcputest = new NetCpu();

        [DllImport("User32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowThreadProcessId(IntPtr hwnd, out int ID);

        public MainWindow()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.AssemblyResolve += Resolver;
            initNotifyIcon();
            this.ResizeMode = ResizeMode.NoResize;
            this.Closing += (o, e) =>
            {
                exitProgram();
            };
            this.Loaded += (o, e) =>
            {
                mswin.Owner = this;
                Process[] procs = Process.GetProcessesByName("Dropbox");
                if (procs.Length == 0)
                {
                    MessageBox.Show("Dropbox is not running!", "ConflictReaper");
                    this.Hide();
                    exitProgram();
                }
                DropBoxProcessIds = new int[procs.Length];
                for (int i = 0; i < procs.Length; i++)
                {
                    DropBoxProcessIds[i] = procs[i].Id;
                }

                keyboardHook = new KeyboardHook();
                keyboardHook.InstallHook();
            };
            this.LocationChanged += (o, e) =>
            {
                mswin.Left = Left + (Width - mswin.Width) / 2;
                mswin.Top = Top + (Height - mswin.Height) / 2;
            };
            global.OnRefresh += RefreshDatagrid;
            global.files.OnProcessExit += (s, e) =>
            {
                WatchDogMessage message = new WatchDogMessage(WatchDogMessageType.UnLock, global.User, e.filename, 0, 0, global.DropboxBasePath);
                sendMessage(message);
            };
            global.files.OnFileEdit += (s, e) =>
            {
                WatchDogMessage message = new WatchDogMessage(WatchDogMessageType.Lock, global.User, e.filename, global.DropboxBasePath);
                sendMessage(message);
            };
            KeyboardHook.OnKeyboardInput += (s, e) =>
            {
                KeyboardHook.HookStruct data = e.hookData;
                int key = data.vkCode;

                if (e.wParam == 256)
                {
                    if (key == 162 || key == 163)
                    {
                        isCtrlDown = true;
                        return;
                    }
                        
                    if (key == 164 || key == 165)
                    {
                        isAltDown = true;
                        return;
                    }
                        
                    if (key == 160 || key == 161)
                    {
                        isShiftDown = true;
                        return;
                    }

                    if ((isAltDown == false && isShiftDown == false && isCtrlDown == false && 
                    ((key >= 48 && key <= 57) || (key >= 65 && key <= 90) || (key >= 186 && key <= 192) || (key >= 219 && key <= 222)
                    || key == 8 || key == 9 || key == 13 || key == 32 || key == 46))
                    || isCtrlDown == true && (key == 86 || key == 88))
                    {
                        int FocusedProcessId;
                        GetWindowThreadProcessId(GetForegroundWindow(), out FocusedProcessId);
                        //if (global.ProcessesThatOpenFile.Contains(FocusedProcessId))
                        //{
                            string title = Process.GetProcessById(FocusedProcessId).MainWindowTitle;
                            System.Diagnostics.Debug.WriteLine(title);
                            global.files.FileEdit(title);
                        //}
                    }
                }

                if (e.wParam == 257)
                {
                    if (key == 162 || key == 163)
                    {
                        isCtrlDown = false;
                        return;
                    }

                    if (key == 164 || key == 165)
                    {
                        isAltDown = false;
                        return;
                    }

                    if (key == 160 || key == 161)
                    {
                        isShiftDown = false;
                        return;
                    }
                }
            };

            FileOpenMonitorInterface.OnFileOpening += (object sender, FileOpeningEventArg e) =>
            {
                String Filename = null;
                int procId;
                if (e.filename.StartsWith("CreateFile"))
                {
                    procId = e.id;
                    Filename = e.filename.Substring(11);
                    if (!File.Exists(Filename))
                        return;
                }
                else
                {
                    string[] Args = e.filename.Split('|');
                    procId = int.Parse(Args[3]);
                    HookInjector injector = new HookInjector(procId, Args[4]);
                    injector.Inject();
                    int start = Args[2].IndexOf('\"', 1) + 2;
                    Filename = praseFilename(Args[2], start);

                    if (Filename != null && Filename.StartsWith(global.DropboxPath))
                    {
                        Process proc = Process.GetProcessById(procId);
                        if (global.ProcessesThatOpenFile.Contains(proc.Id))
                            proc.Exited += (s, earg) =>
                            {
                                Thread.Sleep(500);
                                global.files.ProcessExit(proc.Id);
                                global.ProcessesThatOpenFile.Remove(proc.Id);
                            };
                    }
                }

                if (Filename != null && Filename.StartsWith(global.DropboxPath))
                {
                    if (!global.ProcessesThatOpenFile.Contains(procId))
                        global.ProcessesThatOpenFile.Add(procId);

                    global.files.setStatus(Filename, FileStatus.Open, true);
                    global.files.ProcessOpen(procId, Filename);
                }
            };

            ConflictReaperWebSocketHandler.OnLockChange += OnWebSocketMessage;

            emailBox.Text = Properties.Settings.Default.email;
            passwordBox.Password = Properties.Settings.Default.pwd;
            pathBox.Text = Properties.Settings.Default.dropboxPath;
            global.User = Properties.Settings.Default.user;
        }

        private void Button_View(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                pathBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            exitProgram();
        }

        private void ClickNoProxy(object sender, RoutedEventArgs e)
        {
            global.ProxyState = 0;
            ProxyIP.IsEnabled = false;
            ProxyPort.IsEnabled = false;
        }

        private void ClickDefaultProxy(object sender, RoutedEventArgs e)
        {
            global.ProxyState = 1;
            global.ProxyHost = GlobalUtil.DefaultProxyHost;
            global.ProxyPort = GlobalUtil.DefaultProxyPort;
            ProxyIP.IsEnabled = false;
            ProxyPort.IsEnabled = false;
        }

        private void ClickCustomProxy(object sender, RoutedEventArgs e)
        {
            global.ProxyState = 2;
            ProxyIP.IsEnabled = true;
            ProxyPort.IsEnabled = true;
        }

        private void Button_Step1(object sender, RoutedEventArgs e)
        {
            global.Email = emailBox.Text;
            global.Pwd = passwordBox.Password;
            global.DropboxPath = pathBox.Text;

            if (global.Email.Length == 0)
            {
                MessageBox.Show("Email cannot be empty.", "ConflictReaper");
                return;
            }

            if (global.Pwd.Length == 0)
            {
                MessageBox.Show("Password cannot be empty.", "ConflictReaper");
                return;
            }

            if (global.DropboxPath.Length == 0)
            {
                MessageBox.Show("Work folder cannot be empty.", "ConflictReaper");
                return;
            }

            if (!global.setBasePath())
            {
                MessageBox.Show(global.DropboxPath + "is not a dropbox shared folder.", "ConflictReaper");
                return;
            }
            global.setPathPerfix();

            Properties.Settings.Default.email = global.Email;
            Properties.Settings.Default.pwd = global.Pwd;
            Properties.Settings.Default.dropboxPath = global.DropboxPath;
            Properties.Settings.Default.Save();

            if (global.ProxyState == 2)
            {
                if (ProxyIP.Text.Length == 0 || ProxyPort.Text.Length == 0)
                {
                    MessageBox.Show("Please Input the IP Address of your Proxy Server", "ConflictReaper");
                    return;
                }
                string[] ip = ProxyIP.Text.Split('.');
                if (ip.Length != 4)
                {
                    MessageBox.Show("Wrong IP Address", "ConflictReaper");
                    return;
                }
                if (!(global.checkIP(ip[0]) && global.checkIP(ip[1]) && global.checkIP(ip[2]) && global.checkIP(ip[3])))
                {
                    MessageBox.Show("Wrong IP Address", "ConflictReaper");
                    return;
                }
                try
                {
                    int port = int.Parse(ProxyPort.Text);
                    if (port < 0 || port > 65535)
                    {
                        MessageBox.Show("Wrong Proxy Port", "ConflictReaper");
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show("Wrong Proxy Port", "ConflictReaper");
                    return;
                }
                global.ProxyHost = ProxyIP.Text;
                Int32.TryParse(ProxyPort.Text, out global.ProxyPort);
            }

            mswin.confirm("Checking if you need to authorize...");

            flowMeasureDevicePool = new FlowMeasureDevicePool();
            scanFiles(global.DropboxPath);
            setFileSystemWatcher();
            //setOpeningFileHook();

            dropboxclient = new DropboxConnection(
                new DropboxConnectionArg(
                    Properties.Settings.Default.accessToken, global.User, global.Email, 
                    global.ProxyState, global.ProxyHost, global.ProxyPort, global.DropboxBasePath));

            new Thread(new ThreadStart(()=>
            {
                if (!dropboxclient.IsInitialized)
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        mswin.Hide();
                        if (global.ProxyState != 0)
                        {
                            var settings = new CefSettings();
                            settings.CefCommandLineArgs.Add("--proxy-server", global.ProxyHost + ":" + global.ProxyPort);
                            Cef.Initialize(settings, false, null);
                        }

                        var authorizeUri = dropboxclient.getAuthUri();

                        this.ResizeMode = ResizeMode.CanResize;
                        webView = new ChromiumWebBrowser();
                        Browser.Children.Add(webView);
                        Width = 1024;
                        Height = 768;
                        Top = (SystemParameters.WorkArea.Size.Height - Height) / 2;
                        Left = (SystemParameters.WorkArea.Size.Width - Width) / 2;
                        mswin.confirm("Connecting to " + authorizeUri.Host + "...");
                        webView.Address = authorizeUri.ToString();
                        webView.FrameLoadStart += WebView_FrameLoadStart;
                        webView.FrameLoadEnd += WebView_FrameLoadEnd;

                        FirstStepGrid.Visibility = System.Windows.Visibility.Collapsed;
                        Browser.Visibility = System.Windows.Visibility.Visible;
                        webView.Focus();
                    }));
                }
                else
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        Title = "ConflictReaper: " + global.User + "(" + global.Email + ")";
                        mswin.Hide();
                        FirstStepGrid.Visibility = System.Windows.Visibility.Collapsed;
                        AfterAuth();
                    }));
                }
            })).Start();
        }

        private void Button_Step2(object sender, RoutedEventArgs e)
        {
            this.Hide();
            nIcon.Visible = true;
            nIcon.Text = "ConflictReaper: " + global.User + "(" + global.Email + ")";
        }

        private void AfterAuth()
        {
            new Thread(new ThreadStart(() =>
            {
                this.Dispatcher.Invoke(new Action(() => { this.Hide(); }));
                ListCoWorkers();
                int port = InitWebSocketServer();
                this.Dispatcher.Invoke(new Action(() => { mswin.confirm("Getting Status of Shared Users..."); }));
                RegistrationServer.register(global.Email, port);
                var IAddressMap = RegistrationServer.getAddressMap(global.CoWorkers);
                foreach (SharedUser user in global.CoWorkers)
                {
                    if (IAddressMap.Keys.Contains(user.Email))
                        user.setStatus(true);
                    else
                        user.setStatus(false);
                }
                this.Dispatcher.Invoke(new Action(() => 
                {
                    SecondStepGrid.Visibility = System.Windows.Visibility.Visible;
                    userGrid.ItemsSource = global.CoWorkers;
                    mswin.Hide();
                    this.Show();
                }));
                ConnectCoWorkers(IAddressMap);
            })).Start();
        }

        private void scanFiles(string path)
        {
            string[] dirList = Directory.GetDirectories(path);
            string[] fileList = Directory.GetFiles(path);
            foreach (string dir in dirList)
            {
                if (dir.EndsWith(".dropbox.cache"))
                    continue;
                scanFiles(dir);
            }
            foreach (string file in fileList)
            {
                if (file.EndsWith(".dropbox") || file.EndsWith("desktop.ini"))
                    continue;
                global.files.Add(file);
            }
        }

        private void ListCoWorkers()
        {
            this.Dispatcher.Invoke(new Action(() => { mswin.confirm("Listing Shared Folders..."); }));
            SharedFolders = dropboxclient.ListSharedFolders();
            this.Dispatcher.Invoke(new Action(() => { mswin.confirm("Listing Shared Users..."); }));
            foreach (SharedFolderMetadata folder in SharedFolders)
            {
                if (folder.PathLower != null && folder.PathLower.StartsWith(global.PathPerfix))
                {
                    List<SharedUser> coWorkers = dropboxclient.ListCoWorkers(folder.SharedFolderId);
                    if (coWorkers != null && coWorkers.Count != 0)
                    {
                        global.Folder_coWorkers.Add(folder.SharedFolderId, coWorkers);
                        global.CoWorkers.AddRange(coWorkers);
                    }
                }
            }
            global.CoWorkers = global.CoWorkers.Distinct().ToList();
        }

        private int InitWebSocketServer()
        {
            int port = global.getUsablePort();
            webServer = new WebSocketServer(port);
            webServer.AddWebSocketService("/ConflictReaper", () => new ConflictReaperWebSocketHandler(dropboxclient, global));
            webServer.Start();
            webServer.WebSocketServices.TryGetServiceHost("/ConflictReaper", out webServerHost);
            return port;
        }

        private void ConnectCoWorkers(Dictionary<string, string> IAddressMap)
        {
            if (IAddressMap.Count == 0)
                return;
            foreach (string user in IAddressMap.Keys)
            {
                WebSocket websocket = new WebSocket("ws://" + IAddressMap[user] + "/ConflictReaper");
                websocket.OnOpen += OnWebSocketOpen;
                websocket.OnMessage += OnWebSocketMessage;
                websocket.OnClose += OnWebSocketClose;
                websocket.OnError += OnWebSocketError;
                websocket.SetCookie(new WebSocketSharp.Net.Cookie("user", global.Email));
                websocket.Connect();
                WebSocketPool.Add(user, websocket);
            }
        }

        private void setOpeningFileHook()
        {
            string ChannelName = null;
            RemoteHooking.IpcCreateServer<FileOpenMonitorInterface>(ref ChannelName, WellKnownObjectMode.SingleCall);

            Process[] procs = Process.GetProcesses();
            foreach (Process proc in procs)
            {
                if ((proc.MainWindowTitle.Length > 0 || proc.ProcessName.ToLower().Equals("explorer")) 
                    && !proc.ProcessName.ToLower().Equals("dropbox") && !proc.ProcessName.ToLower().Equals("ConflictReaperclient")
                    && !proc.ProcessName.ToLower().Equals("ConflictReaperclient.vshost")
                    && !proc.ProcessName.ToLower().Equals("shellexperiencehost") && !proc.ProcessName.ToLower().Equals("cmd")
                    && !proc.ProcessName.ToLower().Equals("taskmgr") && !proc.ProcessName.ToLower().Equals("regedit"))
                {
                    System.Diagnostics.Debug.WriteLine(proc.Id + " " + proc.ProcessName + " " + proc.MainWindowTitle);
                    HookInjector injector = new HookInjector(proc.Id, ChannelName);
                    injector.Inject();
                }
                if ((proc.MainWindowTitle.Length > 0 && !proc.ProcessName.ToLower().Equals("explorer"))
                    && !proc.ProcessName.ToLower().Equals("dropbox") && !proc.ProcessName.ToLower().Equals("ConflictReaperclient")
                    && !proc.ProcessName.ToLower().Equals("ConflictReaperclient.vshost")
                    && !proc.ProcessName.ToLower().Equals("shellexperiencehost") && !proc.ProcessName.ToLower().Equals("cmd")
                    && !proc.ProcessName.ToLower().Equals("taskmgr") && !proc.ProcessName.ToLower().Equals("regedit"))
                {
                    proc.Exited += (s, e) =>
                    {
                        Thread.Sleep(500);
                        global.files.ProcessExit(proc.Id);
                        if (global.ProcessesThatOpenFile.Contains(proc.Id))
                            global.ProcessesThatOpenFile.Remove(proc.Id);
                    };
                }
            }
        }

        private void setFileSystemWatcher()
        {
            FileSaveWatcher = new FileSystemWatcher();
            FileSaveWatcher.Path = global.DropboxPath;
            FileSaveWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.Size;

            FileSaveWatcher.Changed += (s, e) =>
            {
                new Thread(new ThreadStart(() => {
                    if (global.files.isEditing(e.FullPath))
                    {
                        global.files.setStatus(e.FullPath, FileStatus.Edit, false);
                        Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss"));
                        //netcputest.start();
                        int totalSendBytes = 0;
                        int time = 0;
                        int count = 0;
                        FlowMeasureDevice device = flowMeasureDevicePool.getDevice();
                        device.getPorts(DropBoxProcessIds);
                        device.Start();
                        bool end = false;
                        new Thread(new ThreadStart(() =>
                        {
                            while (!end)
                            {
                                device.getPorts(DropBoxProcessIds);
                                Thread.Sleep(300);
                            }
                        })).Start();
                        //do
                        //{
                        while (count < 3)
                        {
                            Thread.Sleep(5000);
                            Debug.WriteLine("loop " + device.NetSendBytes);
                            if (device.NetSendBytes < 10000)
                                count++;
                            else
                                count = 0;
                            time += 5;
                            totalSendBytes += device.NetSendBytes;
                            device.Refresh();
                        }
                        //} while (!dropboxclient.IsUpdated(e.FullPath));
                        end = true;
                        device.Stop();
                        WatchDogMessage message = new WatchDogMessage(WatchDogMessageType.UnLock, global.User, e.FullPath, time, totalSendBytes, global.DropboxBasePath);
                        sendMessage(message);
                        //netcputest.stop();
                    }
                })).Start();
            };

            FileSaveWatcher.IncludeSubdirectories = true;
            FileSaveWatcher.Filter = "";
            FileSaveWatcher.EnableRaisingEvents = true;
        }

        private void sendMessage(string user, string message)
        {
            if (WebSocketPool.Keys.Contains(user))
            {
                WebSocketPool[user].Send(message);
            }
            else if (global.WebSocketSessionsMap.Keys.Contains(user))
            {
                webServerHost.Sessions.SendTo(message, global.WebSocketSessionsMap[user]);
            }
        }

        private void sendMessage(WatchDogMessage message)
        {
            string sharedfolder = null;
            foreach (SharedFolderMetadata folder in SharedFolders)
            {
                if (folder.PathLower != null && message.Path.ToLower().StartsWith(folder.PathLower))
                {
                    sharedfolder = folder.SharedFolderId;
                    break;
                }
            }
            if (sharedfolder != null)
            {
                foreach (SharedUser user in global.Folder_coWorkers[sharedfolder])
                {
                    sendMessage(user.Email, message.ToString());
                }
            }
        }

        private void WebView_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            this.Dispatcher.Invoke(new Action(() => { mswin.Hide(); }));
            string jscode = "document.getElementsByClassName(\"text-input-input\")[0].value=\"" + global.Email + "\";document.getElementsByClassName(\"text-input-input\")[1].value=\"" + global.Pwd + "\";document.getElementsByClassName(\"login-button\")[0].click();";
            webView.GetBrowser().MainFrame.ExecuteJavaScriptAsync(jscode);
        }

        private void WebView_FrameLoadStart(object sender, FrameLoadStartEventArgs e)
        {
            if (e.Url.StartsWith("http://localhost:8080"))
            {
                webView.Stop();
                bool AuthResult = dropboxclient.FinishFromUri(new Uri(e.Url));
                if (AuthResult)
                {
                    MessageBox.Show("Authorization succeed!", "ConflictReaper");
                    Properties.Settings.Default.accessToken = dropboxclient.Arg.AccessToken;
                    Properties.Settings.Default.user = dropboxclient.Arg.UserName;
                    Properties.Settings.Default.Save();
                    global.User = dropboxclient.Arg.UserName;
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        Title = "ConflictReaper: " + global.User + "(" + global.Email + ")";
                        Browser.Visibility = System.Windows.Visibility.Collapsed;
                        Width = 408;
                        Height = 500;
                        Top = (SystemParameters.WorkArea.Size.Height - Height) / 2;
                        Left = (SystemParameters.WorkArea.Size.Width - Width) / 2;
                        this.ResizeMode = ResizeMode.NoResize;
                        AfterAuth();
                    }));
                }
                else
                {
                    MessageBox.Show("Authorization Failed!", "ConflictReaper");
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        Browser.Visibility = System.Windows.Visibility.Collapsed;
                        Width = 408;
                        Height = 500;
                        Top = (SystemParameters.WorkArea.Size.Height - Height) / 2;
                        Left = (SystemParameters.WorkArea.Size.Width - Width) / 2;
                        this.ResizeMode = ResizeMode.NoResize;
                        FirstStepGrid.Visibility = System.Windows.Visibility.Visible;
                    }));
                }
            }
        }

        private void OnWebSocketOpen(object s, EventArgs e)
        {
            
        }

        private void OnWebSocketMessage(object s, MessageEventArgs e)
        {
            if (e.IsText)
            {
                WatchDogMessage m = WatchDogMessage.FromString(e.Data, global.DropboxBasePath);
                if (m.Type == WatchDogMessageType.Lock)
                {
                    global.files.setStatus(m.getLocalPath(), FileStatus.EditByOther, true);
                    global.files.setEditor(m.getLocalPath(), m.User);
                    /*if (global.files.isOpening(m.getLocalPath()))
                    {
                        MessageBox.Show("Warning: " + m.User + " starts to edit the file \"" + m.Path + "\" that you are opening.", "ConflictReaper");
                    }*/
                }
                else
                {
                    /*if (global.files.isOpening(m.getLocalPath()))
                    {
                        MessageBox.Show("The file \"" + m.getLocalPath() + "\" that you are opening has been changed by "
                            + m.User + ".\r\nPlease close the file so that the Dropbox Client can update it.", "ConflictReaper");
                        global.files.setStatus(m.getLocalPath(), FileStatus.EditByOther, false);
                        global.files.setEditor(m.getLocalPath(), null);
                    }
                    else
                    {*/
                    //netcputest.start();
                    //测量Dropbox客户端下载流量
                    int totalRecvBytes = 0;
                    int time = 0;
                    FlowMeasureDevice device = flowMeasureDevicePool.getDevice();
                    device.getPorts(DropBoxProcessIds);
                    bool end = false;
                    new Thread(new ThreadStart(() =>
                    {
                        while (!end)
                        {
                            Thread.Sleep(300);
                            device.getPorts(DropBoxProcessIds);
                        }
                    })).Start();
                    device.Start();
                    while (time < m.Time)
                    {
                        Thread.Sleep(1000);
                        time++;
                        totalRecvBytes += device.NetRecvBytes;
                    }
                    device.Stop();
                    end = true;

                    global.files.setStatus(m.getLocalPath(), FileStatus.EditByOther, false);
                    global.files.setEditor(m.getLocalPath(), null);
                    bool End = dropboxclient.IsUpdated(m.getLocalPath());
                    if (totalRecvBytes < 10000 && !End)
                    {
                        //global.files.setStatus(m.getLocalPath(), FileStatus.Uncheck, true);
                        dropboxclient.Download(m.getLocalPath());
                    }
                    //netcputest.stop();
                    //}
                }
            }
        }

        private void OnWebSocketClose(object s, CloseEventArgs e)
        {
            var user = WebSocketPool.FirstOrDefault(q => q.Value.Equals((WebSocket)s)).Key;
            if (user != null)
            {
                WebSocketPool.Remove(user);
                global.updateCoWorkers(user, false);
                global.files.SocketClose(user);
            }
        }

        private void OnWebSocketError(object s, WebSocketSharp.ErrorEventArgs e)
        {
            var user = WebSocketPool.FirstOrDefault(q => q.Value.Equals((WebSocket)s)).Key;
            if (user != null)
            {
                WebSocketPool.Remove(user);
                global.updateCoWorkers(user, false);
                global.files.SocketClose(user);
            }
        }

        public void RefreshDatagrid(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                userGrid.Items.Refresh();
            }));
        }

        private void exitProgram()
        {
            RegistrationServer.close();
            if (webServer != null)
                webServer.Stop();
            for (int i = 0; i < WebSocketPool.Count; i++)
            {
                WebSocketPool.Values.ElementAt(i).Close();
            }
            if (keyboardHook != null)
                keyboardHook.UninstallHook();
            if (flowMeasureDevicePool != null)
                flowMeasureDevicePool.Dispose();
            Application.Current.Shutdown();
        }

        private void initNotifyIcon()
        {
            nIcon = new System.Windows.Forms.NotifyIcon();
            nIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            nIcon.MouseDoubleClick += (o, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    this.Show();
                    nIcon.Visible = false;
                }
            };
            System.Windows.Forms.MenuItem open = new System.Windows.Forms.MenuItem("Show");
            open.Click += (o, e) =>
            {
                this.Show();
                nIcon.Visible = false;
            };
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("Exit");
            exit.Click += (o, e) =>
            {
                exitProgram();
            };
            System.Windows.Forms.MenuItem[] childen = new System.Windows.Forms.MenuItem[] { open, exit };
            nIcon.ContextMenu = new System.Windows.Forms.ContextMenu(childen);
            nIcon.Visible = false;
        }

        private static Assembly Resolver(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("CefSharp"))
            {
                string assemblyName = args.Name.Split(new[] { ',' }, 2)[0] + ".dll";
                string archSpecificPath = System.IO.Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                                                       //Environment.Is64BitProcess ? "x64" : "x86",
                                                       "x86",
                                                       assemblyName);

                return File.Exists(archSpecificPath)
                           ? Assembly.LoadFile(archSpecificPath)
                           : null;
            }

            return null;
        }
        
        private string praseFilename(string src, int pos)
        {
            string file = src;
            int start = file.IndexOf(".exe");
            if (start == -1)
                start = file.IndexOf(".EXE");
            if (start == -1)
                return null;
            file = file.Substring(start);
            start = file.IndexOf(global.DropboxPath);
            if (start == -1)
                return null;
            int end = file.LastIndexOf('.', start);
            if (end == -1)
                return null;
            while (end < src.Length - 1 && char.IsLetterOrDigit(src[end + 1]))
                end++;
            return file.Substring(start, end - start + 1);
        }
    }
}
