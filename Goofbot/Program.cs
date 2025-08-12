using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ImageMagick;
using System.Diagnostics;
using SpotifyAPI.Web;

namespace Goofbot
{
    class Program
    {
        public const string StuffFolder = "Stuff";
        public static readonly string TwitchAppCredentialsFile = Path.Combine(StuffFolder, "client_info.json");

        public const string GuysFolder = "Stuff\\Guys";
        public const string ColorNamesFile = "Stuff\\color_names.json";

        

        public const string TwitchAppRedirectUrl = "http://localhost:3000/";
        public const string TwitchAuthorizationCodeRequestUrlBase = "https://id.twitch.tv/oauth2/authorize";
        public const string TwitchTokenRequestUrl = "https://id.twitch.tv/oauth2/token";

        public const string ColorNamesRequestUrl = "https://api.color.pizza/v1/";

        private const string TwitchBotUsername = "goofbotthebot";
        private const string TwitchChannelUsername = "goofballthecat";

        private static readonly List<string> s_botScopes = new List<string> { "user:read:chat", "user:write:chat", "user:bot", "chat:read", "chat:edit" };
        private static readonly List<string> s_channelScopes = new List<string> { "channel:bot", "channel:read:redemptions" };
        private static readonly HttpClient s_httpClient = new();

        private static dynamic s_twitchAppCredentials;
        private static string s_twitchChannelAccessToken;
        private static string s_twitchBotAccessToken;

        public static string TwitchClientId { get { return s_twitchAppCredentials.client_id; } }
        public static string TwitchChannelAccessToken { get { return s_twitchChannelAccessToken; } }
        public static string TwitchBotAccessToken { get { return s_twitchBotAccessToken; } }

        public static async Task Main(string[] args)
        {
            s_twitchAppCredentials = ParseJsonFile(TwitchAppCredentialsFile);

            string code = await RequestTwitchAuthorizationCodeDefaultBrowser();
            string tokensString = await RequestTwitchTokens(code);
            dynamic tokensObject = JsonConvert.DeserializeObject(tokensString);
            s_twitchChannelAccessToken = Convert.ToString(tokensObject.access_token);

            code = await RequestTwitchAuthorizationCodeChrome();
            tokensString = await RequestTwitchTokens(code);
            dynamic tokensObject2 = JsonConvert.DeserializeObject(tokensString);
            s_twitchBotAccessToken = Convert.ToString(tokensObject2.access_token);

            MagickNET.Initialize();
            Directory.CreateDirectory(GuysFolder);

            /*string colorNamesString = await RequestColorNames();
            File.WriteAllText(ColorNamesFile, colorNamesString);*/

            Bot bot = new Bot(TwitchBotUsername, TwitchChannelUsername, s_twitchBotAccessToken);
            while(true)
            {
                Console.ReadLine();
            }
        }

        public static async Task<string> RequestTwitchAuthorizationCodeDefaultBrowser()
        {
            WebServer server = new WebServer(TwitchAppRedirectUrl);
            Process.Start(new ProcessStartInfo { FileName = GetTwitchAuthorizationCodeRequestUrl(s_channelScopes), UseShellExecute = true });
            string code = await server.Listen();
            server.Stop();
            return code;
        }

        public static async Task<string> RequestTwitchAuthorizationCodeChrome()
        {
            WebServer server = new WebServer(TwitchAppRedirectUrl);
            Process.Start("C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", GetTwitchAuthorizationCodeRequestUrl(s_botScopes));
            string code = await server.Listen();
            server.Stop();
            return code;
        }

        public static dynamic ParseJsonFile(string filename)
        {
            string jsonString = File.ReadAllText(filename);
            return JsonConvert.DeserializeObject(jsonString);
        }

        public static async Task<string> RequestColorNames()
        {
            var response = await s_httpClient.GetAsync(ColorNamesRequestUrl);
            return await response.Content.ReadAsStringAsync();
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

        public static async Task<string> RequestTwitchTokens(string twitchAuthorizationCode)
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

        public static string GetTwitchAuthorizationCodeRequestUrl(List<string> scopes)
        {
            return $"{TwitchAuthorizationCodeRequestUrlBase}?" +
                   $"client_id={s_twitchAppCredentials.client_id}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(TwitchAppRedirectUrl)}&" +
                   "response_type=code&" +
                   $"scope={String.Join('+', scopes)}";
        }
    }
}
