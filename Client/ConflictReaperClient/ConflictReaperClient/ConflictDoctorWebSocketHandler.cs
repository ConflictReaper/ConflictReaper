using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace ConflictReaperClient
{
    class ConflictReaperWebSocketHandler : WebSocketBehavior
    {
        private DropboxConnection client;
        private GlobalUtil global;
        private string user;

        public ConflictReaperWebSocketHandler(DropboxConnection dropboxclient, GlobalUtil g)
        {
            client = dropboxclient;
            global = g;
        }

        protected override void OnOpen()
        {
            string cookies = Context.Headers.Get("cookie");
            user = cookies.Substring(cookies.IndexOf("user") + 5).Split(';')[0];
            global.WebSocketSessionsMap.Add(user, ID);
            global.updateCoWorkers(user, true);

            foreach (string file in global.files.EditingFiles())
            {
                WatchDogMessage m = new WatchDogMessage(WatchDogMessageType.Lock, global.User, file, global.DropboxBasePath);
                Send(m.ToString());
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            OnLockChange(this, e);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            global.WebSocketSessionsMap.Remove(user);
            global.updateCoWorkers(user, false);
            global.files.SocketClose(user);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            global.WebSocketSessionsMap.Remove(user);
            global.updateCoWorkers(user, false);
            global.files.SocketClose(user);
        }

        public static event EventHandler<MessageEventArgs> OnLockChange;
    }
}
