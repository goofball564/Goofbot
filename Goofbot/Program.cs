using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ImageMagick;
using System.Diagnostics;
using TwitchLib.Api;
using SpotifyAPI.Web;
using System.Threading;

namespace Goofbot
{
    class Program
    {
        public const string TwitchAppRedirectUrl = "http://localhost:3000/";
        public const string TwitchAuthorizationCodeRequestUrlBase = "https://id.twitch.tv/oauth2/authorize";
        public const string TwitchTokenRequestUrl = "https://id.twitch.tv/oauth2/token";

        public const string ColorNamesRequestUrl = "https://api.color.pizza/v1/";

        private const string TwitchBotUsername = "goofbotthebot";
        private const string TwitchChannelUsername = "goofballthecat";

        private static readonly List<string> s_botScopes = ["user:read:chat", "user:write:chat", "user:bot", "chat:read", "chat:edit"];
        private static readonly List<string> s_channelScopes = ["channel:bot", "channel:read:redemptions"];
        private static readonly HttpClient s_httpClient = new();
        private static readonly WebServer s_server = new(TwitchAppRedirectUrl);

        private static readonly SemaphoreSlim s_webServerSemaphore = new(1, 1);
        private static readonly SemaphoreSlim s_botTokensSemaphore = new(1, 1);
        private static readonly SemaphoreSlim s_channelTokensSemaphore = new(1, 1);
        private static readonly SemaphoreSlim s_colorDictionarySemaphore = new(1, 1);

        private static dynamic s_twitchAppCredentials;
        private static string s_goofbotAppDataFolder;
        private static string s_colorNamesFile;

        public static string TwitchChannelAccessToken { get; private set; }
        public static string TwitchBotAccessToken { get; private set; }
        public static ColorDictionary ColorDictionary { get; private set; }
        public static TwitchAPI TwitchAPI { get; private set; }
        public static string StuffFolder { get; private set; }

        public static async Task Main(string[] args)
        {
            string localAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            s_goofbotAppDataFolder = Path.Combine(localAppDataFolder, "Goofbot");
            
            string stuffLocationFile = Path.Join(s_goofbotAppDataFolder, "stufflocation.txt");
            StuffFolder = File.ReadAllText(stuffLocationFile).Trim();

            s_colorNamesFile = Path.Combine(StuffFolder, "color_names.json");
            Task colorNamesTask = Task.CompletedTask;
            if (!File.Exists(s_colorNamesFile))
            {
                colorNamesTask = RefreshColorNames();
            }

            string twitchAppCredentialsFile = Path.Combine(StuffFolder, "twitch_credentials.json");
            s_twitchAppCredentials = ParseJsonFile(twitchAppCredentialsFile);

            Task twitchBotAccessTokenTask = RefreshTwitchAccessToken(true);
            Task twitchChannelAccessTokenTask = RefreshTwitchAccessToken(false);

            TwitchAPI = new();
            TwitchAPI.Settings.ClientId = s_twitchAppCredentials.client_id;

            MagickNET.Initialize();

            await colorNamesTask;
            await twitchBotAccessTokenTask;
            await twitchChannelAccessTokenTask;
            
            Bot bot = new Bot(TwitchBotUsername, TwitchChannelUsername, TwitchBotAccessToken);
            while(true)
            {
                Console.ReadLine();
            }
        }

        public static dynamic ParseJsonFile(string filename)
        {
            string jsonString = File.ReadAllText(filename);
            return JsonConvert.DeserializeObject(jsonString);
        }

        public static async Task RefreshTwitchAccessToken(bool botToken)
        {
            string code;
            await s_webServerSemaphore.WaitAsync();
            try
            {
                code = await RequestTwitchAuthorizationCode(botToken);
            }
            finally
            {
                s_webServerSemaphore.Release();
            }
            
            SemaphoreSlim semaphore = botToken ? s_botTokensSemaphore : s_channelTokensSemaphore;

            await semaphore.WaitAsync();
            try
            {
                string tokensString = await RequestTwitchTokens(code);

                string tokensFile = botToken ? "bot_tokens.json" : "channel_tokens.json";
                tokensFile = Path.Join(StuffFolder, tokensFile);
                CancellationToken cancellationToken = new();

                Task writeAllTextTask = File.WriteAllTextAsync(tokensFile, tokensString, cancellationToken);

                dynamic tokensObject = JsonConvert.DeserializeObject(tokensString);
                if (botToken)
                {
                    TwitchBotAccessToken = Convert.ToString(tokensObject.access_token);
                }
                else
                {
                    TwitchChannelAccessToken = Convert.ToString(tokensObject.access_token);
                    TwitchAPI.Settings.AccessToken = TwitchChannelAccessToken;
                }

                await writeAllTextTask;
            }
            finally
            {
                semaphore.Release();
            }
            
        }

        public static async Task RefreshColorNames()
        {
            var response = await s_httpClient.GetAsync(ColorNamesRequestUrl);
            string colorNamesString = await response.Content.ReadAsStringAsync();

            await s_colorDictionarySemaphore.WaitAsync();
            try
            {
                File.WriteAllText(s_colorNamesFile, colorNamesString);
                ColorDictionary = new ColorDictionary(s_colorNamesFile);
            }
            finally
            {
                s_colorDictionarySemaphore.Release();
            }
        }

        /*private static async Task<string> RequestTokensWithRefreshToken()
        {
            dynamic client = ParseJsonFile(ClientInfoFile);
            dynamic tokens = ParseJsonFile(TokensFile);

            var values = new Dictionary<string, string>
            {
                { "refresh_token", Convert.ToString(tokens.refresh_token) },
                { "client_id", Convert.ToString(client.client_id) },
                { "client_secret", Convert.ToString(client.client_secret) },
                { "grant_type", "refresh_token" },
            };
            var content = new FormUrlEncodedContent(values);
            var response = await Program.s_httpClient.PostAsync(TokenRequestUrl, content);
            return await response.Content.ReadAsStringAsync();
        }*/

        private static async Task<string> RequestTwitchTokens(string twitchAuthorizationCode)
        {
            var values = new Dictionary<string, string>
            {
                { "code", twitchAuthorizationCode },
                { "client_id", Convert.ToString(s_twitchAppCredentials.client_id) },
                { "client_secret", Convert.ToString(s_twitchAppCredentials.client_secret) },
                { "grant_type", "authorization_code" },
                { "redirect_uri", TwitchAppRedirectUrl }
            };
            var content = new FormUrlEncodedContent(values);
            var response = await s_httpClient.PostAsync(TwitchTokenRequestUrl, content);
            return await response.Content.ReadAsStringAsync();
        }

        private static string GetTwitchAuthorizationCodeRequestUrl(List<string> scopes)
        {
            return $"{TwitchAuthorizationCodeRequestUrlBase}?" +
                   $"client_id={s_twitchAppCredentials.client_id}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(TwitchAppRedirectUrl)}&" +
                   "response_type=code&" +
                   $"scope={String.Join('+', scopes)}";
        }

        private static async Task<string> RequestTwitchAuthorizationCode(bool useChrome)
        {
            if (useChrome)
            {
                Process.Start("C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", GetTwitchAuthorizationCodeRequestUrl(s_botScopes));
            }
            else
            {
                Process.Start(new ProcessStartInfo { FileName = GetTwitchAuthorizationCodeRequestUrl(s_channelScopes), UseShellExecute = true });
            }
            return await s_server.Listen();
        }
    }
}
