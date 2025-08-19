using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ImageMagick;
using System.Diagnostics;
using TwitchLib.Api;
using System.Threading;
using WindowsAPICodePack.Dialogs;

namespace Goofbot
{
    class Program
    {
        public const string TwitchAppRedirectUrl = "http://localhost:3000/";
        public const string TwitchAuthorizationCodeRequestUrlBase = "https://id.twitch.tv/oauth2/authorize";
        public const string TwitchTokenRequestUrl = "https://id.twitch.tv/oauth2/token";

        private const string TwitchBotUsername = "goofbotthebot";
        private const string TwitchChannelUsername = "goofballthecat";

        private static readonly List<string> s_botScopes = ["user:read:chat", "user:write:chat", "user:bot", "chat:read", "chat:edit"];
        private static readonly List<string> s_channelScopes = ["channel:bot", "channel:read:redemptions"];
        private static readonly HttpClient s_httpClient = new();
        private static readonly WebServer s_server = new(TwitchAppRedirectUrl);
        private static readonly string s_goofbotAppDataFolder = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Goofbot");

        private static readonly SemaphoreSlim s_webServerSemaphore = new(1, 1);
        private static readonly SemaphoreSlim s_botTokensSemaphore = new(1, 1);
        private static readonly SemaphoreSlim s_channelTokensSemaphore = new(1, 1);

        private static dynamic s_twitchAppCredentials;
        private static string s_colorNamesFile;

        public static string TwitchChannelAccessToken { get; private set; }
        public static string TwitchBotAccessToken { get; private set; }
        public static ColorDictionary ColorDictionary { get; private set; }
        public static TwitchAPI TwitchAPI { get; private set; }
        public static string StuffFolder { get; private set; }

        public static async Task Main(string[] args)
        {
            // Get location of bot data folder
            string stuffLocationFile = Path.Join(s_goofbotAppDataFolder, "stufflocation.txt");
            StuffFolder = File.ReadAllText(stuffLocationFile).Trim();

            Console.WriteLine(StuffFolder);

            // Create color dictionary
            s_colorNamesFile = Path.Join(StuffFolder, "color_names.json");
            ColorDictionary = new(s_colorNamesFile);
            Task colorDictionaryTask = ColorDictionary.Initialize();

            // get access tokens using app credentials
            string twitchAppCredentialsFile = Path.Combine(StuffFolder, "twitch_credentials.json");
            s_twitchAppCredentials = ParseJsonFile(twitchAppCredentialsFile);

            Task twitchBotAccessTokenTask = RefreshTwitchAccessToken(true);
            Task twitchChannelAccessTokenTask = RefreshTwitchAccessToken(false);

            // instantiate twitch API
            TwitchAPI = new();
            TwitchAPI.Settings.ClientId = s_twitchAppCredentials.client_id;

            // initialize magick.net
            MagickNET.Initialize();

            await colorDictionaryTask;
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
            string tokensFile = botToken ? "bot_tokens.json" : "channel_tokens.json";
            tokensFile = Path.Join(StuffFolder, tokensFile);

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
                string tokensString = await RequestTwitchTokensWithAuthorizationCode(code);

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

        private static async Task<string> RequestTokensWithRefreshToken(string refreshToken)
        {
            var values = new Dictionary<string, string>
            {
                { "refresh_token", refreshToken },
                { "client_id", Convert.ToString(s_twitchAppCredentials.client_id) },
                { "client_secret", Convert.ToString(s_twitchAppCredentials.client_secret) },
                { "grant_type", "refresh_token" },
            };
            var content = new FormUrlEncodedContent(values);
            var response = await s_httpClient.PostAsync(TwitchTokenRequestUrl, content);
            return await response.Content.ReadAsStringAsync();
        }

        private static async Task<string> RequestTwitchTokensWithAuthorizationCode(string twitchAuthorizationCode)
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
