using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dropbox.Api;
using System.Net.Http;
using Dropbox.Api.Sharing;
using Dropbox.Api.Users;
using Dropbox.Api.Files;
using System.IO;

namespace ConflictReaperClient
{
    public class DropboxConnectionArg
    {
        public string AccessToken { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public int ProxyState { get; set; }
        public string ProxyHost { get; set; }
        public int ProxyPort { get; set; }
        public string Root { get; set; }

        public DropboxConnectionArg(string accessToken, string userName, string email, int proxyState, string proxyHost, int proxyPort, string dropboxBasePath)
        {
            AccessToken = accessToken;
            UserName = userName;
            Email = email;
            ProxyState = proxyState;
            ProxyHost = proxyHost;
            ProxyPort = proxyPort;
            Root = dropboxBasePath;
        }
    }

    public class DropboxConnection
    {
        private string App_key = "xeas2344ekteeai";
        private string App_secret = "56lfpqiijpmqung";
        private const string RedirectUri = "http://localhost:8080/confdoctor/user/dropbox-auth-finish";
        private string oauth2State;

        public string AccessToken { get; private set; }
        public string Uid { get; private set; }
        public DropboxConnectionArg Arg { get; private set; }

        private DropboxClient client;
        public bool IsInitialized { get; set; }

        public DropboxConnection(DropboxConnectionArg arg)
        {
            this.AccessToken = arg.AccessToken;
            this.Arg = arg;
            Initialize();
        }

        private void Initialize()
        {
            HttpClientHandler handler = new HttpClientHandler();
            if (Arg.ProxyState != 0)
                handler.Proxy = new System.Net.WebProxy(Arg.ProxyHost, Arg.ProxyPort);
            DropboxClientConfig config = new DropboxClientConfig("ConfDoctor");
            config.HttpClient = new HttpClient(handler);
            DropboxClient testClient = new DropboxClient(AccessToken, config);
            FullAccount account = null;
            try
            {
                account = testClient.Users.GetCurrentAccountAsync().Result;
                if (!account.Email.Equals(Arg.Email))
                    IsInitialized = false;
                else
                {
                    client = testClient;
                    IsInitialized = true;
                }
            }
            catch (Exception e)
            {
                IsInitialized = false;
                System.Diagnostics.Debug.WriteLine(e.ToString());
            }
        }

        public Uri getAuthUri()
        {
            this.oauth2State = Guid.NewGuid().ToString("N");
            var authorizeUri = DropboxOAuth2Helper.GetAuthorizeUri(OAuthResponseType.Code, App_key, new Uri(RedirectUri), oauth2State);
            return authorizeUri;
        }

        public bool FinishFromUri(Uri uri)
        {
            try
            {
                string url = uri.ToString();
                string[] param = url.Substring(url.IndexOf('?') + 1).Split('&');
                string state, code;
                if (param[0].StartsWith("state"))
                {
                    state = param[0].Substring(param[0].IndexOf('=') + 1);
                    code  = param[1].Substring(param[1].IndexOf('=') + 1);
                }
                else
                {
                    code  = param[0].Substring(param[0].IndexOf('=') + 1);
                    state = param[1].Substring(param[1].IndexOf('=') + 1);
                }
                
                if (state != oauth2State)
                {
                    return false;
                }
                
                HttpClientHandler handler = new HttpClientHandler();
                if (Arg.ProxyState != 0)
                    handler.Proxy = new System.Net.WebProxy(Arg.ProxyHost, Arg.ProxyPort);
                OAuth2Response result = DropboxOAuth2Helper.ProcessCodeFlowAsync(code, App_key, App_secret, RedirectUri, new HttpClient(handler)).Result;

                this.AccessToken = result.AccessToken;
                Arg.AccessToken = result.AccessToken;
                this.Uid = result.Uid;
                
                DropboxClientConfig config = new DropboxClientConfig("ConfDoctor");
                config.HttpClient = new HttpClient(handler);
                client = new DropboxClient(AccessToken, config);

                FullAccount account = client.Users.GetCurrentAccountAsync().Result;
                Arg.UserName = account.Name.DisplayName;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IList<SharedFolderMetadata> ListSharedFolders()
        {
            ListFoldersResult result;
            try
            {
                result = client.Sharing.ListFoldersAsync().Result;
                return result.Entries;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public List<SharedUser> ListCoWorkers(string sharedFolderId)
        {
            SharedFolderMembers members;
            try
            {
                members = client.Sharing.ListFolderMembersAsync(sharedFolderId).Result;
                List<SharedUser> coWorkers = new List<SharedUser>();
                foreach (UserMembershipInfo user in members.Users)
                {
                    SharedUser s_user = getUser(user.User.AccountId);
                    if (s_user != null && !s_user.Email.Equals(Arg.Email))
                    {
                        coWorkers.Add(s_user);
                    }
                }
                return coWorkers;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                return null;
            }
        }

        private SharedUser getUser(string accountId)
        {
            Account account = client.Users.GetAccountAsync(accountId).Result;
            if (account.EmailVerified)
                return new SharedUser(account.Email, account.Name.DisplayName) ;
            else
                return null;
        }

        public bool IsUpdated(string LocalPath)
        {
            string path = LocalPath.Substring(Arg.Root.Length).Replace('\\', '/');
            FileMetadata file = client.Files.GetMetadataAsync(path).Result.AsFile;
            DateTime CloudTime = file.ClientModified;
            DateTime LocalTime = Directory.GetLastWriteTimeUtc(LocalPath);
            return DateTime.Equals(CloudTime, LocalTime);
        }

        public void Download(string LocalPath)
        {
            string path = LocalPath.Substring(Arg.Root.Length).Replace('\\', '/');
            FileMetadata filedata = client.Files.GetMetadataAsync(path).Result.AsFile;

            var response = client.Files.DownloadAsync(path).Result;
            var stream = response.GetContentAsStreamAsync().Result;
            FileStream file = new FileStream(LocalPath, FileMode.Create);
            stream.CopyTo(file);
            file.Flush();
            file.Close();
            Directory.SetLastWriteTimeUtc(LocalPath, filedata.ClientModified.ToLocalTime());
            Directory.SetLastAccessTimeUtc(LocalPath, DateTime.Now);
        }
    }
}
